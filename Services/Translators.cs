using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CityFix_backend.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace CityFix_backend.Services
{
    public static class TextLanguage
    {
        public static string Detect(string? text)
        {
            int cyrillic = 0, latin = 0;
            foreach (var c in text ?? "")
            {
                if (c is >= 'Ѐ' and <= 'ӿ') cyrillic++;
                else if (char.IsAsciiLetter(c)) latin++;
            }
            return latin > cyrillic ? "en" : "uk";
        }

        public static string Other(string language) => language == "en" ? "uk" : "en";
    }

    public class DeepLTranslator : ITranslator
    {
        private readonly HttpClient _http;
        private readonly TranslationOptions _options;

        public DeepLTranslator(HttpClient http, IOptions<TranslationOptions> options)
        {
            _http = http;
            _options = options.Value;
        }

        public async Task<string> TranslateAsync(string text, string from, string to, CancellationToken cancellationToken = default)
        {
            var key = _options.DeepLApiKey!;
            var host = key.EndsWith(":fx", StringComparison.Ordinal) ? "https://api-free.deepl.com" : "https://api.deepl.com";
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{host}/v2/translate")
            {
                Content = JsonContent.Create(new
                {
                    text = new[] { text },
                    source_lang = from == "en" ? "EN" : "UK",
                    target_lang = to == "en" ? "EN-US" : "UK"
                })
            };
            request.Headers.TryAddWithoutValidation("Authorization", $"DeepL-Auth-Key {key}");

            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"DeepL refused the translation ({(int)response.StatusCode}).");
            }
            using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
            return json.RootElement.GetProperty("translations")[0].GetProperty("text").GetString() ?? "";
        }
    }

    public class MyMemoryTranslator : ITranslator
    {
        private const int MaxBytes = 450;

        private readonly HttpClient _http;
        private readonly TranslationOptions _options;

        public MyMemoryTranslator(HttpClient http, IOptions<TranslationOptions> options)
        {
            _http = http;
            _options = options.Value;
        }

        public async Task<string> TranslateAsync(string text, string from, string to, CancellationToken cancellationToken = default)
        {
            var parts = new List<string>();
            foreach (var chunk in Chunks(text))
            {
                parts.Add(await TranslateChunkAsync(chunk, from, to, cancellationToken));
            }
            return string.Join(" ", parts);
        }

        private async Task<string> TranslateChunkAsync(string text, string from, string to, CancellationToken cancellationToken)
        {
            var url = $"https://api.mymemory.translated.net/get?q={Uri.EscapeDataString(text)}&langpair={from}|{to}";
            if (!string.IsNullOrWhiteSpace(_options.MyMemoryEmail))
            {
                url += $"&de={Uri.EscapeDataString(_options.MyMemoryEmail)}";
            }

            using var response = await _http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"MyMemory refused the translation ({(int)response.StatusCode}).");
            }
            using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
            var root = json.RootElement;
            var status = root.GetProperty("responseStatus");
            var ok = status.ValueKind == JsonValueKind.Number ? status.GetInt32() == 200 : status.GetString() == "200";
            var translated = root.GetProperty("responseData").GetProperty("translatedText").GetString() ?? "";

            if (!ok || translated.Contains("MYMEMORY WARNING", StringComparison.OrdinalIgnoreCase))
            {
                throw new HttpRequestException($"MyMemory did not translate: {translated}");
            }
            return translated;
        }

        private static IEnumerable<string> Chunks(string text)
        {
            var rest = text.Trim();
            while (Encoding.UTF8.GetByteCount(rest) > MaxBytes)
            {
                var length = rest.Length;
                while (Encoding.UTF8.GetByteCount(rest.AsSpan(0, length)) > MaxBytes) length = length * 9 / 10;

                var cut = rest.LastIndexOfAny(new[] { '.', '!', '?', '\n' }, length - 1);
                if (cut < length / 2) cut = rest.LastIndexOf(' ', length - 1);
                if (cut < length / 2) cut = length - 1;

                yield return rest[..(cut + 1)].Trim();
                rest = rest[(cut + 1)..].Trim();
            }
            if (rest.Length > 0) yield return rest;
        }
    }
}
