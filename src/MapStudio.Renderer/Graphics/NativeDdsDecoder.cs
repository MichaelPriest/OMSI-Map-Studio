using System.Buffers.Binary;

namespace MapStudio.Renderer.Graphics;

internal sealed record NativeDdsImage(byte[] Rgba, int Width, int Height);

/// <summary>
/// Small CPU decoder for the block-compressed DDS formats common in OMSI
/// scenery packs. Decodes the top mip level to RGBA for the native viewport.
/// </summary>
internal static class NativeDdsDecoder
{
    public static NativeDdsImage? TryDecodeFile(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length < 128)
                return null;

            Span<byte> header = stackalloc byte[128];
            stream.ReadExactly(header);
            if (header[0] != (byte)'D' || header[1] != (byte)'D' ||
                header[2] != (byte)'S' || header[3] != (byte)' ')
                return null;

            var height = BinaryPrimitives.ReadInt32LittleEndian(header.Slice(12, 4));
            var width = BinaryPrimitives.ReadInt32LittleEndian(header.Slice(16, 4));
            if (width <= 0 || height <= 0 || width > 16384 || height > 16384)
                return null;

            var fourCc = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(84, 4));
            var rgba = new byte[checked(width * height * 4)];
            if (fourCc is 0x31545844 or 0x33545844 or 0x35545844) // DXT1/3/5
            {
                DecodeBlocks(stream, rgba, width, height, fourCc);
                return new NativeDdsImage(rgba, width, height);
            }

            // Uncompressed 24/32-bit RGB DDS files.
            var pixelFlags = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(80, 4));
            var bits = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(88, 4));
            if (fourCc != 0 || (pixelFlags & 0x40) == 0 || bits is not (24 or 32))
                return null;

            var redMask = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(92, 4));
            var greenMask = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(96, 4));
            var blueMask = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(100, 4));
            var alphaMask = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(104, 4));
            var bytesPerPixel = (int)bits / 8;
            var pitch = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(20, 4));
            var rowPitch = pitch >= (uint)(width * bytesPerPixel) ? (int)pitch : width * bytesPerPixel;
            if (stream.Length < 128L + (long)rowPitch * height)
                return null;

            var row = new byte[rowPitch];
            for (var y = 0; y < height; y++)
            {
                stream.ReadExactly(row);
                for (var x = 0; x < width; x++)
                {
                    var packed = bytesPerPixel == 4
                        ? BinaryPrimitives.ReadUInt32LittleEndian(row.AsSpan(x * 4, 4))
                        : (uint)(row[x * 3] | row[x * 3 + 1] << 8 | row[x * 3 + 2] << 16);
                    var offset = (y * width + x) * 4;
                    rgba[offset] = ExtractChannel(packed, redMask, 0);
                    rgba[offset + 1] = ExtractChannel(packed, greenMask, 0);
                    rgba[offset + 2] = ExtractChannel(packed, blueMask, 0);
                    rgba[offset + 3] = alphaMask == 0 ? (byte)255 : ExtractChannel(packed, alphaMask, 255);
                }
            }
            return new NativeDdsImage(rgba, width, height);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            ArgumentException or NotSupportedException or OverflowException)
        {
            return null;
        }
    }

    private static byte ExtractChannel(uint value, uint mask, byte fallback)
    {
        if (mask == 0) return fallback;
        var shift = System.Numerics.BitOperations.TrailingZeroCount(mask);
        var max = mask >> shift;
        var channel = (value & mask) >> shift;
        return (byte)((channel * 255u + max / 2u) / max);
    }

    private static void DecodeBlocks(Stream stream, byte[] output, int width, int height, uint format)
    {
        var blockBytes = format == 0x31545844 ? 8 : 16;
        var blocksX = (width + 3) / 4;
        var blocksY = (height + 3) / 4;
        var block = new byte[blockBytes];
        Span<byte> colors = stackalloc byte[16];
        for (var by = 0; by < blocksY; by++)
        for (var bx = 0; bx < blocksX; bx++)
        {
            stream.ReadExactly(block);
            var colorOffset = format == 0x31545844 ? 0 : 8;
            var c0 = BinaryPrimitives.ReadUInt16LittleEndian(block.AsSpan(colorOffset, 2));
            var c1 = BinaryPrimitives.ReadUInt16LittleEndian(block.AsSpan(colorOffset + 2, 2));
            BuildColors(c0, c1, colors, format == 0x31545844);

            var colorBits = BinaryPrimitives.ReadUInt32LittleEndian(block.AsSpan(colorOffset + 4, 4));
            ulong alphaBits = 0;
            byte[]? alphaTable = null;
            if (format == 0x33545844)
            {
                alphaBits = BinaryPrimitives.ReadUInt64LittleEndian(block.AsSpan(0, 8));
            }
            else if (format == 0x35545844)
            {
                alphaTable = BuildAlphaTable(block[0], block[1]);
                for (var i = 0; i < 6; i++) alphaBits |= (ulong)block[2 + i] << (8 * i);
            }

            for (var py = 0; py < 4; py++)
            for (var px = 0; px < 4; px++)
            {
                var x = bx * 4 + px;
                var y = by * 4 + py;
                if (x >= width || y >= height) continue;
                var pixel = py * 4 + px;
                var ci = (int)((colorBits >> (pixel * 2)) & 3);
                var dst = (y * width + x) * 4;
                output[dst] = colors[ci * 4];
                output[dst + 1] = colors[ci * 4 + 1];
                output[dst + 2] = colors[ci * 4 + 2];
                output[dst + 3] = format switch
                {
                    0x33545844 => (byte)(((alphaBits >> (pixel * 4)) & 0xF) * 17),
                    0x35545844 => alphaTable![(int)((alphaBits >> (pixel * 3)) & 7)],
                    0x31545844 when c0 <= c1 && ci == 3 => (byte)0,
                    _ => (byte)255
                };
            }
        }
    }

    private static void BuildColors(ushort c0, ushort c1, Span<byte> colors, bool dxt1)
    {
        Decode565(c0, out var r0, out var g0, out var b0);
        Decode565(c1, out var r1, out var g1, out var b1);
        SetColor(colors, 0, r0, g0, b0, 255);
        SetColor(colors, 1, r1, g1, b1, 255);
        if (dxt1 && c0 <= c1)
        {
            SetColor(colors, 2, (byte)((r0 + r1) / 2), (byte)((g0 + g1) / 2), (byte)((b0 + b1) / 2), 255);
            SetColor(colors, 3, 0, 0, 0, 0);
        }
        else
        {
            SetColor(colors, 2, (byte)((2 * r0 + r1) / 3), (byte)((2 * g0 + g1) / 3), (byte)((2 * b0 + b1) / 3), 255);
            SetColor(colors, 3, (byte)((r0 + 2 * r1) / 3), (byte)((g0 + 2 * g1) / 3), (byte)((b0 + 2 * b1) / 3), 255);
        }
    }

    private static void Decode565(ushort c, out byte r, out byte g, out byte b)
    {
        r = (byte)((((c >> 11) & 31) * 255 + 15) / 31);
        g = (byte)((((c >> 5) & 63) * 255 + 31) / 63);
        b = (byte)(((c & 31) * 255 + 15) / 31);
    }

    private static void SetColor(Span<byte> colors, int index, byte r, byte g, byte b, byte a)
    {
        var i = index * 4;
        colors[i] = r; colors[i + 1] = g; colors[i + 2] = b; colors[i + 3] = a;
    }

    private static byte[] BuildAlphaTable(byte a0, byte a1)
    {
        var table = new byte[8];
        table[0] = a0; table[1] = a1;
        if (a0 > a1)
        {
            for (var i = 1; i <= 6; i++) table[i + 1] = (byte)(((7 - i) * a0 + i * a1) / 7);
        }
        else
        {
            for (var i = 1; i <= 4; i++) table[i + 1] = (byte)(((5 - i) * a0 + i * a1) / 5);
            table[6] = 0; table[7] = 255;
        }
        return table;
    }
}
