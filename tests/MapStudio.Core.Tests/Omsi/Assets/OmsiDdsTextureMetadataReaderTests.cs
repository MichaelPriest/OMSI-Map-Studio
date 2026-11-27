using System.Buffers.Binary;
using MapStudio.Core.Omsi.Textures;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiDdsTextureMetadataReaderTests
{
    [Fact]
    public void TryRead_RecognizesAlphaOnlyMask()
    {
        var bytes =
            CreateDdsHeader(
                width: 256,
                height: 256,
                pixelFlags: 0x00000002,
                rgbBits: 8,
                fourCc: null);

        var metadata =
            OmsiDdsTextureMetadataReader
                .TryRead(bytes);

        Assert.NotNull(metadata);
        Assert.Equal(
            256,
            metadata!.Width);
        Assert.Equal(
            256,
            metadata.Height);
        Assert.Equal(
            "A8",
            metadata.Format);
        Assert.True(
            metadata.AlphaOnly);
    }

    [Fact]
    public void TryRead_RecognizesCompressedFormat()
    {
        var bytes =
            CreateDdsHeader(
                width: 512,
                height: 512,
                pixelFlags: 0x00000004,
                rgbBits: 0,
                fourCc: "DXT5");

        var metadata =
            OmsiDdsTextureMetadataReader
                .TryRead(bytes);

        Assert.NotNull(metadata);
        Assert.Equal(
            "DXT5",
            metadata!.Format);
        Assert.False(
            metadata.AlphaOnly);
    }

    [Fact]
    public void TryRead_RejectsInvalidHeader()
    {
        var bytes =
            new byte[128];

        Assert.Null(
            OmsiDdsTextureMetadataReader
                .TryRead(bytes));
    }

    private static byte[] CreateDdsHeader(
        int width,
        int height,
        uint pixelFlags,
        uint rgbBits,
        string? fourCc)
    {
        var bytes =
            new byte[128];

        bytes[0] = (byte)'D';
        bytes[1] = (byte)'D';
        bytes[2] = (byte)'S';
        bytes[3] = (byte)' ';

        BinaryPrimitives
            .WriteUInt32LittleEndian(
                bytes.AsSpan(4, 4),
                124);

        BinaryPrimitives
            .WriteUInt32LittleEndian(
                bytes.AsSpan(12, 4),
                checked((uint)height));

        BinaryPrimitives
            .WriteUInt32LittleEndian(
                bytes.AsSpan(16, 4),
                checked((uint)width));

        BinaryPrimitives
            .WriteUInt32LittleEndian(
                bytes.AsSpan(76, 4),
                32);

        BinaryPrimitives
            .WriteUInt32LittleEndian(
                bytes.AsSpan(80, 4),
                pixelFlags);

        if (fourCc is not null)
        {
            var encoded =
                System.Text.Encoding
                    .ASCII.GetBytes(
                        fourCc);

            encoded.CopyTo(
                bytes,
                84);
        }

        BinaryPrimitives
            .WriteUInt32LittleEndian(
                bytes.AsSpan(88, 4),
                rgbBits);

        return bytes;
    }
}
