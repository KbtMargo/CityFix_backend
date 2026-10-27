using System.Buffers.Binary;

namespace CityFix_backend.Services
{
    /// <summary>
    /// Removes EXIF, XMP, IPTC and text metadata (GPS position, camera, author) from uploaded photos
    /// without re-encoding the pixels. JPEG orientation is kept so phone photos are not shown sideways.
    /// </summary>
    public static class ImageMetadata
    {
        private static readonly HashSet<string> PngMetadataChunks = new() { "eXIf", "tEXt", "zTXt", "iTXt", "tIME" };
        private static readonly HashSet<string> WebpMetadataChunks = new() { "EXIF", "XMP " };

        public static byte[] Strip(byte[] image, string contentType) => contentType switch
        {
            "image/jpeg" => StripJpeg(image),
            "image/png" => StripPng(image),
            "image/webp" => StripWebp(image),
            _ => image
        };

        private static byte[] StripJpeg(byte[] d)
        {
            if (d.Length < 4 || d[0] != 0xFF || d[1] != 0xD8) throw Malformed();

            var kept = new List<(int Start, int Length, byte Marker)>();
            ushort orientation = 0;
            var pos = 2;
            while (true)
            {
                if (pos + 2 > d.Length || d[pos] != 0xFF) throw Malformed();
                var marker = d[pos + 1];
                if (marker == 0xFF)
                {
                    pos++;
                    continue;
                }
                if (marker == 0xD9)
                {
                    kept.Add((pos, 2, marker));
                    break;
                }

                if (pos + 4 > d.Length) throw Malformed();
                var end = pos + 2 + BinaryPrimitives.ReadUInt16BigEndian(d.AsSpan(pos + 2));
                if (end < pos + 4 || end > d.Length) throw Malformed();

                if (orientation == 0 && marker == 0xE1 && end - pos >= 10 && d.AsSpan(pos + 4, 6).SequenceEqual("Exif\0\0"u8))
                {
                    orientation = ExifOrientation(d.AsSpan(pos + 10, end - pos - 10));
                }
                if (!IsJpegMetadata(marker))
                {
                    kept.Add((pos, end - pos, marker));
                }
                pos = end;

                if (marker == 0xDA)
                {
                    var scanEnd = JpegScanEnd(d, pos);
                    kept.Add((pos, scanEnd - pos, 0));
                    pos = scanEnd;
                }
            }

            using var output = new MemoryStream(d.Length);
            output.Write(d, 0, 2);
            var next = 0;
            if (kept[0].Marker == 0xE0)
            {
                output.Write(d, kept[0].Start, kept[0].Length);
                next = 1;
            }
            if (orientation is > 1 and <= 8)
            {
                output.Write(OrientationSegment(orientation));
            }
            foreach (var (start, length, _) in kept.Skip(next))
            {
                output.Write(d, start, length);
            }
            return output.ToArray();
        }

        // Keeps JFIF (APP0), ICC profile (APP2) and Adobe colour info (APP14); drops other APPn segments and comments.
        private static bool IsJpegMetadata(byte marker) =>
            marker == 0xFE || (marker is >= 0xE1 and <= 0xEF && marker is not 0xE2 and not 0xEE);

        private static int JpegScanEnd(byte[] d, int pos)
        {
            for (; pos + 1 < d.Length; pos++)
            {
                if (d[pos] == 0xFF && d[pos + 1] is not 0x00 and not 0xFF and not (>= 0xD0 and <= 0xD7))
                {
                    return pos;
                }
            }
            throw Malformed();
        }

        private static ushort ExifOrientation(ReadOnlySpan<byte> tiff)
        {
            if (tiff.Length < 8) return 0;
            var little = tiff[0] == 'I' && tiff[1] == 'I';
            if (!little && !(tiff[0] == 'M' && tiff[1] == 'M')) return 0;

            var ifd = U32(tiff[4..], little);
            if (ifd > (uint)tiff.Length - 2) return 0;
            var count = U16(tiff[(int)ifd..], little);
            for (var i = 0; i < count; i++)
            {
                var entry = (int)ifd + 2 + i * 12;
                if (entry + 12 > tiff.Length) return 0;
                if (U16(tiff[entry..], little) == 0x0112) return U16(tiff[(entry + 8)..], little);
            }
            return 0;
        }

        private static byte[] OrientationSegment(ushort orientation) =>
        [
            0xFF, 0xE1, 0x00, 0x22,
            (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0x00, 0x00,
            (byte)'M', (byte)'M', 0x00, 0x2A, 0x00, 0x00, 0x00, 0x08,
            0x00, 0x01,
            0x01, 0x12, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01, 0x00, (byte)orientation, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00
        ];

        private static byte[] StripPng(byte[] d)
        {
            if (d.Length < 8) throw Malformed();

            using var output = new MemoryStream(d.Length);
            output.Write(d, 0, 8);
            var pos = 8;
            while (true)
            {
                if (pos + 12 > d.Length) throw Malformed();
                var length = BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(pos));
                if (length > (uint)(d.Length - pos - 12)) throw Malformed();
                var size = 12 + (int)length;
                var type = System.Text.Encoding.ASCII.GetString(d, pos + 4, 4);

                if (!PngMetadataChunks.Contains(type))
                {
                    output.Write(d, pos, size);
                }
                pos += size;
                if (type == "IEND") return output.ToArray();
            }
        }

        private static byte[] StripWebp(byte[] d)
        {
            if (d.Length < 12) throw Malformed();
            var riffEnd = (long)BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(4)) + 8;
            if (riffEnd > d.Length) throw Malformed();

            using var output = new MemoryStream(d.Length);
            output.Write(d, 0, 12);
            var pos = 12;
            while (pos + 8 <= riffEnd)
            {
                var type = System.Text.Encoding.ASCII.GetString(d, pos, 4);
                var length = BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(pos + 4));
                var size = 8 + (long)length + (length & 1);
                if (pos + size > riffEnd) throw Malformed();

                if (!WebpMetadataChunks.Contains(type))
                {
                    var start = (int)output.Position;
                    output.Write(d, pos, (int)size);
                    if (type == "VP8X" && length > 0)
                    {
                        // Clear the "has EXIF" and "has XMP" flags of the extended header.
                        output.GetBuffer()[start + 8] &= unchecked((byte)~0x0C);
                    }
                }
                pos += (int)size;
            }

            var result = output.ToArray();
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4), (uint)(result.Length - 8));
            return result;
        }

        private static ushort U16(ReadOnlySpan<byte> s, bool little) =>
            little ? BinaryPrimitives.ReadUInt16LittleEndian(s) : BinaryPrimitives.ReadUInt16BigEndian(s);

        private static uint U32(ReadOnlySpan<byte> s, bool little) =>
            little ? BinaryPrimitives.ReadUInt32LittleEndian(s) : BinaryPrimitives.ReadUInt32BigEndian(s);

        private static ServiceValidationException Malformed() => new("Photos", "Validation.PhotoType");
    }
}
