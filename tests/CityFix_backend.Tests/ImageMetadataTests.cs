using System.Buffers.Binary;
using System.Text;
using CityFix_backend.Services;

namespace CityFix_backend.Tests;

public class ImageMetadataTests
{
    private static byte[] Segment(byte marker, byte[] payload)
    {
        var length = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(length, (ushort)(payload.Length + 2));
        return [0xFF, marker, .. length, .. payload];
    }

    // Minimal little-endian TIFF block: IFD0 with Orientation and a Make tag that points at "SECRETCAM".
    private static byte[] Exif(ushort orientation)
    {
        var tiff = new List<byte> { (byte)'I', (byte)'I', 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00, 0x02, 0x00 };
        tiff.AddRange([0x12, 0x01, 0x03, 0x00, 0x01, 0x00, 0x00, 0x00, (byte)orientation, 0x00, 0x00, 0x00]);
        tiff.AddRange([0x0F, 0x01, 0x02, 0x00, 0x0A, 0x00, 0x00, 0x00, 0x26, 0x00, 0x00, 0x00]);
        tiff.AddRange([0x00, 0x00, 0x00, 0x00]);
        tiff.AddRange("SECRETCAM\0"u8.ToArray());
        return [.. "Exif\0\0"u8.ToArray(), .. tiff];
    }

    private static byte[] Jpeg(params byte[][] segments)
    {
        byte[] scan = [0x12, 0xFF, 0x00, 0x34, 0xFF, 0xD0, 0x56];
        var sos = Segment(0xDA, [0x01, 0x01, 0x00, 0x00, 0x3F, 0x00]);
        return [0xFF, 0xD8, .. segments.SelectMany(s => s), .. sos, .. scan, 0xFF, 0xD9, .. "TRAILERGPS"u8.ToArray()];
    }

    private static bool Contains(byte[] data, string text) => Encoding.ASCII.GetString(data).Contains(text);

    [Fact]
    public void Jpeg_drops_exif_comments_and_trailer_but_keeps_orientation_and_image_data()
    {
        var jfif = Segment(0xE0, "JFIF\0\u0001\u0001"u8.ToArray());
        var quant = Segment(0xDB, new byte[65]);
        var input = Jpeg(jfif, Segment(0xE1, Exif(6)), Segment(0xFE, "GPSCOMMENT"u8.ToArray()), quant);

        var output = ImageMetadata.Strip(input, "image/jpeg");

        Assert.False(Contains(output, "SECRETCAM"));
        Assert.False(Contains(output, "GPSCOMMENT"));
        Assert.False(Contains(output, "TRAILERGPS"));
        Assert.True(Contains(output, "JFIF"));
        Assert.Equal([0xFF, 0xD9], output[^2..]);

        // Orientation segment follows APP0 and holds value 6 (big-endian SHORT).
        var exifAt = Encoding.ASCII.GetString(output).IndexOf("Exif", StringComparison.Ordinal);
        Assert.True(exifAt > 0);
        Assert.Equal(6, output[exifAt + 6 + 8 + 2 + 8 + 1]);
    }

    [Fact]
    public void Jpeg_without_rotation_gets_no_exif_at_all()
    {
        var output = ImageMetadata.Strip(Jpeg(Segment(0xE1, Exif(1))), "image/jpeg");
        Assert.False(Contains(output, "Exif"));
    }

    [Fact]
    public void Truncated_jpeg_is_rejected()
    {
        Assert.Throws<ServiceValidationException>(() => ImageMetadata.Strip([0xFF, 0xD8, 0xFF, 0xE1, 0xFF, 0xFF], "image/jpeg"));
    }

    private static byte[] PngChunk(string type, string data)
    {
        var length = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, (uint)data.Length);
        return [.. length, .. Encoding.ASCII.GetBytes(type), .. Encoding.ASCII.GetBytes(data), 0, 0, 0, 0];
    }

    [Fact]
    public void Png_drops_text_and_exif_chunks()
    {
        byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        byte[] clean = [.. signature, .. PngChunk("IHDR", "0123456789abc"), .. PngChunk("IDAT", "pixels"), .. PngChunk("IEND", "")];
        byte[] input = [.. signature, .. PngChunk("IHDR", "0123456789abc"), .. PngChunk("tEXt", "Author\0Secret"),
            .. PngChunk("eXIf", "GPS"), .. PngChunk("IDAT", "pixels"), .. PngChunk("IEND", ""), .. "TRAILER"u8.ToArray()];

        Assert.Equal(clean, ImageMetadata.Strip(input, "image/png"));
    }

    private static byte[] RiffChunk(string type, byte[] data)
    {
        byte[] padding = data.Length % 2 == 1 ? [0] : [];
        return [.. Encoding.ASCII.GetBytes(type), .. BitConverter.GetBytes((uint)data.Length), .. data, .. padding];
    }

    [Fact]
    public void Webp_drops_exif_and_xmp_and_fixes_header()
    {
        byte[] body = [.. "WEBP"u8.ToArray(), .. RiffChunk("VP8X", [0x0C, 0, 0, 0, 0, 0, 0, 0, 0, 0]),
            .. RiffChunk("VP8L", [0x2F, 1, 2, 3, 4]), .. RiffChunk("EXIF", "GPSWEBP"u8.ToArray()), .. RiffChunk("XMP ", "<x/>"u8.ToArray())];
        byte[] input = [.. "RIFF"u8.ToArray(), .. BitConverter.GetBytes((uint)body.Length), .. body];

        var output = ImageMetadata.Strip(input, "image/webp");

        Assert.False(Contains(output, "GPSWEBP"));
        Assert.False(Contains(output, "XMP"));
        Assert.True(Contains(output, "VP8L"));
        Assert.Equal((uint)output.Length - 8, BitConverter.ToUInt32(output, 4));
        Assert.Equal(0, output[20] & 0x0C);
    }
}
