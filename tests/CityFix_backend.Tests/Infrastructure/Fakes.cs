using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;
using CityFix_backend.Services;
using CityFix_backend.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using SkiaSharp;

namespace CityFix_backend.Tests.Infrastructure;

public record SentEmail(string To, EmailMessage Message)
{
    /// <summary>The first absolute link in the plain-text body (confirmation, reset, appeal link).</summary>
    public string Link => Regex.Match(Message.Text, @"https?://\S+").Value;
}

public sealed class FakeEmailSender : IEmailSender
{
    public ConcurrentQueue<SentEmail> Sent { get; } = new();
    public bool Fail { get; set; }

    public Task SendAsync(string to, EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (Fail) throw new IOException("SMTP is down");
        Sent.Enqueue(new SentEmail(to, message));
        return Task.CompletedTask;
    }

    public IReadOnlyList<SentEmail> To(string email) =>
        Sent.Where(m => string.Equals(m.To, email, StringComparison.OrdinalIgnoreCase)).ToList();

    public async Task<SentEmail> WaitForAsync(string email, Func<SentEmail, bool>? match = null, int timeoutMs = 10_000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < until)
        {
            var found = To(email).LastOrDefault(m => match?.Invoke(m) ?? true);
            if (found != null) return found;
            await Task.Delay(50);
        }
        throw new TimeoutException($"No e-mail to {email} within {timeoutMs} ms.");
    }
}

public sealed class FakeGeocoding : IGeocodingService
{
    public string? Address { get; set; } = "вулиця Проскурівська 1, Хмельницький";
    public int ReverseCalls;

    public Task<string?> ReverseAsync(double lat, double lng, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref ReverseCalls);
        return Task.FromResult(Address);
    }

    public Task<GeoPlace?> SearchAsync(string query, CancellationToken cancellationToken = default) =>
        Task.FromResult<GeoPlace?>(query.Length < 3 ? null : new GeoPlace(49.42, 26.98, $"{query}, Хмельницький"));
}

public sealed class FakeTranslator : ITranslator
{
    public bool Fail { get; set; }
    public ConcurrentQueue<(string Text, string From, string To)> Calls { get; } = new();

    public Task<string> TranslateAsync(string text, string from, string to, CancellationToken cancellationToken = default)
    {
        Calls.Enqueue((text, from, to));
        if (Fail) throw new HttpRequestException("translator is down");
        return Task.FromResult($"[{to}] {text}");
    }
}

/// <summary>An HttpMessageHandler that answers from a delegate and records every request.</summary>
public sealed class StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = new();
    public List<string?> Bodies { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        Bodies.Add(request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
        return respond(request);
    }

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
}

public sealed class TestWebHostEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
{
    public TestWebHostEnvironment(string root)
    {
        ContentRootPath = root;
        WebRootPath = Path.Combine(root, "wwwroot");
        ContentRootFileProvider = new NullFileProvider();
        WebRootFileProvider = new NullFileProvider();
    }

    public string EnvironmentName { get; set; } = "Development";
    public string ApplicationName { get; set; } = "CityFix_backend";
    public string WebRootPath { get; set; }
    public IFileProvider WebRootFileProvider { get; set; }
    public string ContentRootPath { get; set; }
    public IFileProvider ContentRootFileProvider { get; set; }
}

public static class TestFiles
{
    public static IFormFile FormFile(byte[] content, string name = "photo.png", string contentType = "image/png")
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "photos", name) { Headers = new HeaderDictionary(), ContentType = contentType };
    }

    public static byte[] Image(int width = 64, int height = 48, SKEncodedImageFormat format = SKEncodedImageFormat.Png)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap)) canvas.Clear(SKColors.OrangeRed);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }

    /// <summary>A real JPEG with an EXIF camera model and a comment that must not survive the upload.</summary>
    public static byte[] JpegWithSecrets()
    {
        var jpeg = Image(320, 200, SKEncodedImageFormat.Jpeg);
        byte[] tiff = [(byte)'I', (byte)'I', 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00, 0x01, 0x00,
            0x10, 0x01, 0x02, 0x00, 0x0A, 0x00, 0x00, 0x00, 0x1A, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            .. "SECRETCAM\0"u8.ToArray()];
        byte[] exif = [.. "Exif\0\0"u8.ToArray(), .. tiff];
        byte[] app1 = [0xFF, 0xE1, (byte)((exif.Length + 2) >> 8), (byte)(exif.Length + 2), .. exif];
        var comment = "GPS 49.42 26.98"u8.ToArray();
        byte[] com = [0xFF, 0xFE, (byte)((comment.Length + 2) >> 8), (byte)(comment.Length + 2), .. comment];
        return [.. jpeg[..2], .. app1, .. com, .. jpeg[2..]];
    }

    public static bool Contains(byte[] data, string ascii) =>
        System.Text.Encoding.ASCII.GetString(data).Contains(ascii, StringComparison.Ordinal);

    public static string TempFolder()
    {
        var path = Path.Combine(Path.GetTempPath(), "cityfix-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
