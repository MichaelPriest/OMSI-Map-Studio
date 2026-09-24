using System.Buffers.Binary;
using System.IO.Compression;
using MapStudio.Renderer.Graphics;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativePngDecoderTests
{
    [Fact]
    public void DecodesNonInterlacedRgbaPngWithoutWic()
    {
        var png =
            BuildRgbaPng(
                2,
                1,
                [
                    255, 0, 0, 255,
                    0, 255, 0, 128
                ]);

        var decoded =
            NativePngDecoder.TryDecode(
                png);

        Assert.NotNull(
            decoded);

        Assert.Equal(
            2,
            decoded!.Width);

        Assert.Equal(
            1,
            decoded.Height);

        Assert.Equal(
            new byte[]
            {
                255, 0, 0, 255,
                0, 255, 0, 128
            },
            decoded.Rgba);
    }

    [Fact]
    public void RejectsNonPngPayload()
    {
        Assert.Null(
            NativePngDecoder.TryDecode(
                "API KEY REQUIRED"u8));
    }

    private static byte[] BuildRgbaPng(
        int width,
        int height,
        byte[] rgba)
    {
        var rowBytes =
            checked(
                width *
                4);

        Assert.Equal(
            checked(
                rowBytes *
                height),
            rgba.Length);

        using var filtered =
            new MemoryStream();

        for (
            var row = 0;
            row < height;
            row++)
        {
            filtered.WriteByte(
                0);

            filtered.Write(
                rgba,
                row *
                    rowBytes,
                rowBytes);
        }

        using var compressed =
            new MemoryStream();

        using (
            var zlib =
                new ZLibStream(
                    compressed,
                    CompressionLevel
                        .Optimal,
                    leaveOpen: true))
        {
            filtered.Position =
                0;

            filtered.CopyTo(
                zlib);
        }

        using var output =
            new MemoryStream();

        output.Write(
            new byte[]
            {
                137, 80, 78, 71,
                13, 10, 26, 10
            });

        var ihdr =
            new byte[13];

        BinaryPrimitives
            .WriteUInt32BigEndian(
                ihdr.AsSpan(
                    0,
                    4),
                checked(
                    (uint)width));

        BinaryPrimitives
            .WriteUInt32BigEndian(
                ihdr.AsSpan(
                    4,
                    4),
                checked(
                    (uint)height));

        ihdr[8] =
            8;

        ihdr[9] =
            6;

        WriteChunk(
            output,
            "IHDR",
            ihdr);

        WriteChunk(
            output,
            "IDAT",
            compressed.ToArray());

        WriteChunk(
            output,
            "IEND",
            []);

        return output.ToArray();
    }

    private static void WriteChunk(
        Stream output,
        string type,
        byte[] data)
    {
        Span<byte> length =
            stackalloc byte[4];

        BinaryPrimitives
            .WriteUInt32BigEndian(
                length,
                checked(
                    (uint)data.Length));

        output.Write(
            length);

        var typeBytes =
            System.Text.Encoding
                .ASCII
                .GetBytes(type);

        output.Write(
            typeBytes);

        output.Write(
            data);

        // NativePngDecoder intentionally does not require CRC because
        // downloaded references are already validated before this path.
        output.Write(
            new byte[4]);
    }
}
