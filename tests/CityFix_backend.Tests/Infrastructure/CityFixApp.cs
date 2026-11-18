using System.Net;
using System.Text.RegularExpressions;
using CityFix_backend.Data;
using CityFix_backend.Services.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CityFix_backend.Tests.Infrastructure;

/// <summary>
/// The whole web application (Program.cs) running in memory against a throw-away database, with e-mail and geocoding faked.
/// </summary>
public sealed class CityFixApp : IAsyncLifetime
{
    public const string AdminEmail = "admin@cityfix.test";
    public const string AdminPassword = "AdminPassw0rd!";
    private const string ClientIpHeader = "X-Test-Client-IP";
    private static int _nextIp;

    public PostgresDatabase Database { get; } = new();
    public FakeEmailSender Email { get; } = new();
    public FakeGeocoding Geocoding { get; } = new();
    public string AppData { get; } = TestFiles.TempFolder();

    internal WebApplicationFactory<Program> Factory { get; private set; } = null!;

    /// <summary>Extra settings for one fixture, e.g. a different environment.</summary>
    public Dictionary<string, string?> Settings { get; } = new();

    public string Environment { get; set; } = "Development";

    public static string ProjectDirectory
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "CityFix_backend.csproj"))) dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("CityFix_backend.csproj not found above the test output folder.");
        }
    }

    public async Task InitializeAsync()
    {
        // Startup moves photos from wwwroot/uploads into the upload folder; never let a test run touch real photos.
        if (Directory.Exists(Path.Combine(ProjectDirectory, "wwwroot", "uploads")))
        {
            throw new InvalidOperationException("wwwroot/uploads exists; start the app once normally so it moves those photos before running web tests.");
        }

        await Database.InitializeAsync();
        Factory = CreateFactory();
        if (RealServer)
        {
            Factory.UseKestrel(0);
            Factory.StartServer();
            BaseUrl = Factory.ClientOptions.BaseAddress.ToString().TrimEnd('/');
        }
        else
        {
            using var _ = Factory.CreateClient();
        }
    }

    /// <summary>Run on Kestrel with a real port, so a real browser can reach it.</summary>
    public bool RealServer { get; init; }

    public string BaseUrl { get; private set; } = "http://localhost";

    internal WebApplicationFactory<Program> CreateFactory() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment(Environment);
        builder.UseContentRoot(ProjectDirectory);
        builder.UseSetting("ConnectionStrings:DefaultConnection", Database.ConnectionString);
        builder.UseSetting("DataProtection:KeysPath", Path.Combine(AppData, "keys"));
        builder.UseSetting("Uploads:Path", AppData);
        builder.UseSetting("Seed:AdminEmail", AdminEmail);
        builder.UseSetting("Seed:AdminPassword", AdminPassword);
        builder.UseSetting("Translation:Enabled", "false");
        builder.UseSetting("Notifications:DispatchInterval", "00:00:00.200");
        builder.UseSetting("Notifications:PublicUrl", "http://localhost");
        builder.UseSetting("Logging:LogLevel:Default", "Warning");
        foreach (var (key, value) in Settings) builder.UseSetting(key, value);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Email);
            services.RemoveAll<IGeocodingService>();
            services.AddSingleton<IGeocodingService>(Geocoding);
            services.AddSingleton<IStartupFilter, ClientIpStartupFilter>();
        });
    });

    /// <summary>A browser-like client: keeps cookies, does not follow redirects, and has its own IP for rate limiting.</summary>
    public HttpClient Client(string? ip = null, bool https = false)
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri(https ? "https://localhost" : "http://localhost")
        });
        client.DefaultRequestHeaders.Add(ClientIpHeader, ip ?? $"10.{Interlocked.Increment(ref _nextIp) / 250 % 250}.{_nextIp % 250}.1");
        return client;
    }

    public async Task<T> DbAsync<T>(Func<ApplicationDbContext, TestData, Task<T>> work)
    {
        await using var db = Database.CreateContext();
        return await work(db, new TestData(db, DateTime.UtcNow));
    }

    public Task<T> DataAsync<T>(Func<TestData, Task<T>> create) => DbAsync((_, data) => create(data));

    /// <summary>Signs in through the real login form and returns the client holding the auth cookie.</summary>
    public async Task<HttpClient> SignInAsync(string email, string password = TestData.Password)
    {
        var client = Client();
        var response = await client.PostFormAsync("/Account/Login", new() { ["Email"] = email, ["Password"] = password });
        Assert.True(response.StatusCode == HttpStatusCode.Redirect, $"Sign-in of {email} failed: {(int)response.StatusCode}");
        return client;
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await Database.DisposeAsync();
        try { Directory.Delete(AppData, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private sealed class ClientIpStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                if (IPAddress.TryParse(context.Request.Headers[ClientIpHeader], out var ip)) context.Connection.RemoteIpAddress = ip;
                return nextMiddleware(context);
            });
            next(app);
        };
    }
}

[CollectionDefinition(nameof(WebCollection))]
public class WebCollection : ICollectionFixture<CityFixApp>;

public static partial class HttpExtensions
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenPattern();

    public static string Token(string html) =>
        TokenPattern().Match(html) is { Success: true } match ? match.Groups[1].Value : throw new InvalidOperationException("The page has no antiforgery token.");

    /// <summary>Posts a form the way a browser would: first loads a page for a fresh antiforgery token.</summary>
    public static async Task<HttpResponseMessage> PostFormAsync(this HttpClient client, string url, Dictionary<string, string> fields, string? tokenFrom = null)
    {
        var page = await client.GetStringAsync(tokenFrom ?? url);
        fields["__RequestVerificationToken"] = Token(page);
        return await client.PostAsync(url, new FormUrlEncodedContent(fields));
    }

    /// <summary>The page text with HTML entities decoded (Razor encodes Cyrillic).</summary>
    public static async Task<string> TextAsync(this HttpResponseMessage response) =>
        WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

    public static async Task<string> PageAsync(this HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"GET {url} returned {(int)response.StatusCode} {response.Headers.Location}");
        return await response.TextAsync();
    }

    public static string? Location(this HttpResponseMessage response) => response.Headers.Location?.OriginalString;
}
