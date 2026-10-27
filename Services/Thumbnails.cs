using SkiaSharp;

namespace CityFix_backend.Services
{
    /// <summary>
    /// Small JPEG copies of uploaded photos for lists and thumbnail strips, so a page of appeals does not
    /// download every full-size photo. The EXIF orientation is applied, since the copy carries no metadata.
    /// </summary>
    public static class Thumbnails
    {
        public const int MaxSide = 480;
        private const int Quality = 80;

        /// <returns>The JPEG bytes, or null when the image cannot be decoded.</returns>
        public static byte[]? Create(byte[] content)
        {
            using var data = SKData.CreateCopy(content);
            using var codec = SKCodec.Create(data);
            if (codec == null) return null;

            using var bitmap = SKBitmap.Decode(codec);
            if (bitmap == null || bitmap.Width == 0 || bitmap.Height == 0) return null;

            var origin = codec.EncodedOrigin;
            var sideways = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
            var (width, height) = sideways ? (bitmap.Height, bitmap.Width) : (bitmap.Width, bitmap.Height);
            var scale = Math.Min(1f, (float)MaxSide / Math.Max(width, height));
            var info = new SKImageInfo(Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));

            using var surface = SKSurface.Create(info);
            if (surface == null) return null;
            var canvas = surface.Canvas;
            // JPEG has no transparency; transparent PNG/GIF areas become white rather than black.
            canvas.Clear(SKColors.White);
            canvas.Scale(scale);
            canvas.Concat(Orientation(origin, bitmap.Width, bitmap.Height));
            using var image = SKImage.FromBitmap(bitmap);
            canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));

            using var snapshot = surface.Snapshot();
            using var encoded = snapshot.Encode(SKEncodedImageFormat.Jpeg, Quality);
            return encoded?.ToArray();
        }

        // Maps the stored pixels (w × h) onto the upright picture.
        private static SKMatrix Orientation(SKEncodedOrigin origin, float w, float h) => origin switch
        {
            SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),
            SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1),
            SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),
            _ => SKMatrix.Identity
        };
    }
}
