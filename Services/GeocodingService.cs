using System.Globalization;
using System.Text.Json;
using CityFix_backend.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace CityFix_backend.Services
{
    public class NominatimGeocodingService : IGeocodingService
    {
        private static readonly SemaphoreSlim Gate = new(1, 1);
        private static DateTimeOffset _lastRequest = DateTimeOffset.MinValue;

        private readonly HttpClient _http;
        private readonly IMemoryCache _cache;
        private readonly GeocodingOptions _options;
        private readonly TimeProvider _clock;
        private readonly ILogger<NominatimGeocodingService> _logger;

        public NominatimGeocodingService(HttpClient http, IMemoryCache cache, IOptions<GeocodingOptions> options,
            TimeProvider clock, ILogger<NominatimGeocodingService> logger)
        {
            _http = http;
            _cache = cache;
            _options = options.Value;
            _clock = clock;
            _logger = logger;
        }

        public async Task<string?> ReverseAsync(double lat, double lng, CancellationToken cancellationToken = default)
        {
            var la = Math.Round(lat, 5).ToString(CultureInfo.InvariantCulture);
            var ln = Math.Round(lng, 5).ToString(CultureInfo.InvariantCulture);
            var language = Language();

            return await CachedAsync($"geo:rev:{language}:{la},{ln}", async () =>
            {
                using var json = await GetAsync($"reverse?format=jsonv2&zoom=18&addressdetails=1&lat={la}&lon={ln}&accept-language={language}", cancellationToken);
                return json == null ? null : FormatAddress(json.RootElement);
            });
        }

        public async Task<GeoPlace?> SearchAsync(string query, CancellationToken cancellationToken = default)
        {
            var q = query.Trim();
            if (q.Length < 3) return null;
            var language = Language();

            return await CachedAsync($"geo:search:{language}:{q.ToLowerInvariant()}", async () =>
            {
                var url = $"search?format=jsonv2&limit=1&addressdetails=1&bounded=1&viewbox={_options.ViewBox}"
                    + $"&accept-language={language}&q={Uri.EscapeDataString(q)}";
                using var json = await GetAsync(url, cancellationToken);
                if (json == null || json.RootElement.ValueKind != JsonValueKind.Array || json.RootElement.GetArrayLength() == 0)
                {
                    return null;
                }

                var hit = json.RootElement[0];
                if (!double.TryParse(hit.GetProperty("lat").GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
                    || !double.TryParse(hit.GetProperty("lon").GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lng))
                {
                    return null;
                }
                return new GeoPlace(lat, lng, FormatAddress(hit) ?? q);
            });
        }

        private async Task<T?> CachedAsync<T>(string key, Func<Task<T?>> load) where T : class
        {
            if (_cache.TryGetValue(key, out T? cached)) return cached;
            var value = await load();
            if (value != null)
            {
                _cache.Set(key, value, _options.CacheDuration);
            }
            return value;
        }

        private async Task<JsonDocument?> GetAsync(string relativeUrl, CancellationToken cancellationToken)
        {
            await Gate.WaitAsync(cancellationToken);
            try
            {
                var wait = _lastRequest + _options.MinInterval - _clock.GetUtcNow();
                if (wait > TimeSpan.Zero)
                {
                    await Task.Delay(wait, cancellationToken);
                }
                _lastRequest = _clock.GetUtcNow();

                using var response = await _http.GetAsync(relativeUrl, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Nominatim answered {Status} for {Url}", (int)response.StatusCode, relativeUrl);
                    return null;
                }
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                _logger.LogWarning(ex, "Nominatim is unavailable");
                return null;
            }
            finally
            {
                Gate.Release();
            }
        }

        private static string? FormatAddress(JsonElement place)
        {
            if (place.TryGetProperty("address", out var a))
            {
                string? Get(params string[] keys) => keys.Select(k => a.TryGetProperty(k, out var v) ? v.GetString() : null).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
                var street = Get("road", "pedestrian", "footway", "square", "neighbourhood");
                var house = Get("house_number");
                var city = Get("city", "town", "village");
                var line = string.Join(" ", new[] { street, house }.Where(s => s != null));
                var parts = new[] { line, city }.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                if (parts.Count > 0) return string.Join(", ", parts);
            }
            return place.TryGetProperty("display_name", out var name) ? name.GetString() : null;
        }

        private static string Language() => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en" ? "en" : "uk";
    }
}
