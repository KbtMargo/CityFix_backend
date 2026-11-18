using System.Net;
using System.Text;
using System.Web;
using CityFix_backend.Services;
using CityFix_backend.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CityFix_backend.Tests.Unit;

[Trait("Category", "Unit")]
public class TranslatorClientTests
{
    [Theory]
    [InlineData("abc:fx", "https://api-free.deepl.com/v2/translate")]
    [InlineData("abc", "https://api.deepl.com/v2/translate")]
    public async Task DeepL_uses_the_free_host_for_free_keys_and_sends_the_key(string key, string url)
    {
        var handler = new StubHttpHandler(_ => StubHttpHandler.Json("""{"translations":[{"text":"Pothole"}]}"""));
        var deepl = new DeepLTranslator(new HttpClient(handler), Options.Create(new TranslationOptions { DeepLApiKey = key }));

        Assert.Equal("Pothole", await deepl.TranslateAsync("Яма", "uk", "en"));

        var request = handler.Requests.Single();
        Assert.Equal(url, request.RequestUri!.ToString());
        Assert.Equal($"DeepL-Auth-Key {key}", request.Headers.GetValues("Authorization").Single());
        Assert.Contains("\"source_lang\":\"UK\"", handler.Bodies[0]);
        Assert.Contains("\"target_lang\":\"EN-US\"", handler.Bodies[0]);
    }

    [Fact]
    public async Task DeepL_errors_become_http_exceptions()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
        var deepl = new DeepLTranslator(new HttpClient(handler), Options.Create(new TranslationOptions { DeepLApiKey = "k" }));
        await Assert.ThrowsAsync<HttpRequestException>(() => deepl.TranslateAsync("Яма", "uk", "en"));
    }

    private static string MyMemoryAnswer(string text, object status) =>
        $$"""{"responseData":{"translatedText":"{{text}}"},"responseStatus":{{(status is string s ? $"\"{s}\"" : status)}}}""";

    [Fact]
    public async Task MyMemory_splits_long_text_into_chunks_within_the_byte_limit()
    {
        var handler = new StubHttpHandler(_ => StubHttpHandler.Json(MyMemoryAnswer("T", 200)));
        var translator = new MyMemoryTranslator(new HttpClient(handler), Options.Create(new TranslationOptions { MyMemoryEmail = "me@example.com" }));
        var text = string.Join(" ", Enumerable.Repeat("Біля будинку глибока яма, у яку вже потрапили кілька автомобілів.", 12));

        var result = await translator.TranslateAsync(text, "uk", "en");

        Assert.True(handler.Requests.Count > 1);
        Assert.Equal(string.Join(" ", Enumerable.Repeat("T", handler.Requests.Count)), result);
        foreach (var request in handler.Requests)
        {
            var query = HttpUtility.ParseQueryString(request.RequestUri!.Query);
            Assert.True(Encoding.UTF8.GetByteCount(query["q"]!) <= 450);
            Assert.Equal("uk|en", query["langpair"]);
            Assert.Equal("me@example.com", query["de"]);
        }
        var sent = string.Join(" ", handler.Requests.Select(r => HttpUtility.ParseQueryString(r.RequestUri!.Query)["q"]));
        Assert.Equal(text, sent);
    }

    [Theory]
    [InlineData("MYMEMORY WARNING: YOU USED ALL AVAILABLE FREE TRANSLATIONS", 200)]
    [InlineData("whatever", 403)]
    [InlineData("whatever", "429")]
    public async Task MyMemory_quota_warnings_and_errors_are_failures(string text, object status)
    {
        var handler = new StubHttpHandler(_ => StubHttpHandler.Json(MyMemoryAnswer(text, status)));
        var translator = new MyMemoryTranslator(new HttpClient(handler), Options.Create(new TranslationOptions()));
        await Assert.ThrowsAsync<HttpRequestException>(() => translator.TranslateAsync("Яма", "uk", "en"));
    }

    [Fact]
    public async Task MyMemory_accepts_status_sent_as_a_string()
    {
        var handler = new StubHttpHandler(_ => StubHttpHandler.Json(MyMemoryAnswer("Pothole", "200")));
        var translator = new MyMemoryTranslator(new HttpClient(handler), Options.Create(new TranslationOptions()));
        Assert.Equal("Pothole", await translator.TranslateAsync("Яма", "uk", "en"));
        Assert.DoesNotContain("&de=", handler.Requests[0].RequestUri!.Query);
    }
}

[Trait("Category", "Unit")]
[Collection(nameof(GeocodingCollection))]
public class GeocodingClientTests
{
    private static (NominatimGeocodingService Service, StubHttpHandler Handler) Create(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new StubHttpHandler(respond);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://nominatim.test/") };
        var options = Options.Create(new GeocodingOptions { MinInterval = TimeSpan.Zero });
        return (new NominatimGeocodingService(http, new GeocodingCache(), options, TimeProvider.System, NullLogger<NominatimGeocodingService>.Instance), handler);
    }

    [Fact]
    public async Task Reverse_builds_a_short_address_from_the_parts()
    {
        var (service, handler) = Create(_ => StubHttpHandler.Json("""
            {"display_name":"1, Проскурівська, Хмельницький, Україна","address":{"road":"Проскурівська вулиця","house_number":"1","city":"Хмельницький"}}
            """));
        using var _ = new UiCulture("uk");

        Assert.Equal("Проскурівська вулиця 1, Хмельницький", await service.ReverseAsync(49.4216123, 26.9965311));

        var query = HttpUtility.ParseQueryString(handler.Requests[0].RequestUri!.Query);
        Assert.Equal(("49.42161", "26.99653", "uk"), (query["lat"], query["lon"], query["accept-language"]));
    }

    [Fact]
    public async Task Reverse_falls_back_to_the_display_name()
    {
        var (service, _) = Create(_ => StubHttpHandler.Json("""{"display_name":"Somewhere far","address":{}}"""));
        Assert.Equal("Somewhere far", await service.ReverseAsync(10.5, 20.5));
    }

    [Fact]
    public async Task Answers_are_cached_per_point_and_language()
    {
        var (service, handler) = Create(_ => StubHttpHandler.Json("""{"display_name":"X"}"""));
        using (new UiCulture("uk"))
        {
            await service.ReverseAsync(1, 2);
            await service.ReverseAsync(1.000001, 2.000001);
        }
        using (new UiCulture("en")) await service.ReverseAsync(1, 2);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Service_failures_give_no_address()
    {
        var (service, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        Assert.Null(await service.ReverseAsync(3, 4));
        var (broken, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>") });
        Assert.Null(await broken.ReverseAsync(5, 6));
        var (offline, _) = Create(_ => throw new HttpRequestException("offline"));
        Assert.Null(await offline.SearchAsync("Проскурівська"));
    }

    [Fact]
    public async Task Search_is_limited_to_the_city_and_parses_coordinates()
    {
        var (service, handler) = Create(_ => StubHttpHandler.Json("""
            [{"lat":"49.4230","lon":"26.9871","display_name":"Майдан Незалежності","address":{"square":"Майдан Незалежності","city":"Хмельницький"}}]
            """));

        var place = await service.SearchAsync("  майдан  ");

        Assert.Equal(new GeoPlace(49.423, 26.9871, "Майдан Незалежності, Хмельницький"), place);
        var query = HttpUtility.ParseQueryString(handler.Requests[0].RequestUri!.Query);
        Assert.Equal(("1", "майдан", new GeocodingOptions().ViewBox), (query["bounded"], query["q"], query["viewbox"]));
    }

    [Fact]
    public async Task Too_short_or_unknown_searches_return_nothing()
    {
        var (service, handler) = Create(_ => StubHttpHandler.Json("[]"));
        Assert.Null(await service.SearchAsync("ab"));
        Assert.Empty(handler.Requests);
        Assert.Null(await service.SearchAsync("nowhere-street"));
    }
}

/// <summary>The geocoder throttles through a static gate, so its tests do not run in parallel with each other.</summary>
[CollectionDefinition(nameof(GeocodingCollection), DisableParallelization = true)]
public class GeocodingCollection;
