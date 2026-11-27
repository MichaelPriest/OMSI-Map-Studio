using System.Buffers.Binary;
using MapStudio.Renderer.Scene;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeTextureThumbnailGeneratorTests
{
    [Fact]
    public void RendersUncompressedTgaAsBmpThumbnail()
    {
        var path =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-texture-" +
                Guid.NewGuid()
                    .ToString("N") +
                ".tga");

        try
        {
            var bytes =
                new byte[
                    18 +
                    2 *
                    2 *
                    3];

            bytes[2] =
                2;

            BinaryPrimitives
                .WriteUInt16LittleEndian(
                    bytes.AsSpan(
                        12,
                        2),
                    2);

            BinaryPrimitives
                .WriteUInt16LittleEndian(
                    bytes.AsSpan(
                        14,
                        2),
                    2);

            bytes[16] =
                24;

            bytes[17] =
                0x20;

            // TGA stores BGR.
            var pixel = 18;

            WriteBgr(
                bytes,
                ref pixel,
                255,
                0,
                0);

            WriteBgr(
                bytes,
                ref pixel,
                0,
                255,
                0);

            WriteBgr(
                bytes,
                ref pixel,
                0,
                0,
                255);

            WriteBgr(
                bytes,
                ref pixel,
                255,
                255,
                255);

            File.WriteAllBytes(
                path,
                bytes);

            var bmp =
                new NativeTextureThumbnailGenerator()
                    .RenderBmp(
                        path,
                        64,
                        64);

            Assert.True(
                bmp.Length >
                54);

            Assert.Equal(
                (byte)'B',
                bmp[0]);

            Assert.Equal(
                (byte)'M',
                bmp[1]);

            Assert.Equal(
                64,
                BinaryPrimitives
                    .ReadInt32LittleEndian(
                        bmp.AsSpan(
                            18,
                            4)));

            Assert.Equal(
                64,
                BinaryPrimitives
                    .ReadInt32LittleEndian(
                        bmp.AsSpan(
                            22,
                            4)));
        }
        finally
        {
            File.Delete(
                path);
        }
    }

    [Fact]
    public void RendersA8DdsMaskWithoutWicCodec()
    {
        var path =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-mask-" +
                Guid.NewGuid()
                    .ToString("N") +
                ".dds");

        try
        {
            var bytes =
                new byte[
                    128 +
                    4];

            bytes[0] =
                (byte)'D';
            bytes[1] =
                (byte)'D';
            bytes[2] =
                (byte)'S';
            bytes[3] =
                (byte)' ';

            BinaryPrimitives
                .WriteUInt32LittleEndian(
                    bytes.AsSpan(
                        4,
                        4),
                    124);

            BinaryPrimitives
                .WriteUInt32LittleEndian(
                    bytes.AsSpan(
                        12,
                        4),
                    2);

            BinaryPrimitives
                .WriteUInt32LittleEndian(
                    bytes.AsSpan(
                        16,
                        4),
                    2);

            BinaryPrimitives
                .WriteUInt32LittleEndian(
                    bytes.AsSpan(
                        76,
                        4),
                    32);

            BinaryPrimitives
                .WriteUInt32LittleEndian(
                    bytes.AsSpan(
                        80,
                        4),
                    0x00000002);

            BinaryPrimitives
                .WriteUInt32LittleEndian(
                    bytes.AsSpan(
                        88,
                        4),
                    8);

            BinaryPrimitives
                .WriteUInt32LittleEndian(
                    bytes.AsSpan(
                        104,
                        4),
                    0xFF);

            bytes[128] =
                0;
            bytes[129] =
                64;
            bytes[130] =
                128;
            bytes[131] =
                255;

            File.WriteAllBytes(
                path,
                bytes);

            var bmp =
                new NativeTextureThumbnailGenerator()
                    .RenderBmp(
                        path,
                        64,
                        64);

            Assert.True(
                bmp.Length >
                54);

            Assert.Equal(
                (byte)'B',
                bmp[0]);

            Assert.Equal(
                (byte)'M',
                bmp[1]);
        }
        finally
        {
            File.Delete(
                path);
        }
    }

    private static void WriteBgr(
        byte[] bytes,
        ref int offset,
        byte r,
        byte g,
        byte b)
    {
        bytes[offset++] =
            b;
        bytes[offset++] =
            g;
        bytes[offset++] =
            r;
    }
}
