using System.Buffers.Binary;
using MapStudio.Core.ProtonBus;
using Xunit;

namespace MapStudio.ProtonBus.Tests;

public sealed class ProtonBusTextureMetadataReaderTests
{
    [Fact]
    public void ReaderReadsPngDimensionsWithoutDecodingImage()
    {
        var path =
            CreateTempFile(
                ".png",
                CreatePngHeader(
                    4096,
                    1024));

        try
        {
            Assert.True(
                ProtonBusTextureMetadataReader
                    .TryRead(
                        path,
                        out var metadata,
                        out var error),
                error);

            Assert.Equal(
                4096,
                metadata.Width);

            Assert.Equal(
                1024,
                metadata.Height);

            Assert.Equal(
                4096,
                metadata.MaxDimension);

            Assert.Equal(
                "PNG",
                metadata.Format);
        }
        finally
        {
            File.Delete(
                path);
        }
    }

    [Fact]
    public void ReaderReadsDdsDimensions()
    {
        var data =
            new byte[20];

        "DDS "u8.CopyTo(
            data);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                data.AsSpan(
                    12,
                    4),
                512);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                data.AsSpan(
                    16,
                    4),
                2048);

        var path =
            CreateTempFile(
                ".dds",
                data);

        try
        {
            Assert.True(
                ProtonBusTextureMetadataReader
                    .TryRead(
                        path,
                        out var metadata,
                        out var error),
                error);

            Assert.Equal(
                2048,
                metadata.Width);

            Assert.Equal(
                512,
                metadata.Height);

            Assert.Equal(
                "DDS",
                metadata.Format);
        }
        finally
        {
            File.Delete(
                path);
        }
    }

    [Fact]
    public void ReaderRejectsIncompletePng()
    {
        var path =
            CreateTempFile(
                ".png",
                [
                    137,
                    80,
                    78,
                    71,
                    13,
                    10,
                    26,
                    10
                ]);

        try
        {
            Assert.False(
                ProtonBusTextureMetadataReader
                    .TryRead(
                        path,
                        out _,
                        out var error));

            Assert.Contains(
                "incomplete",
                error,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(
                path);
        }
    }

    private static byte[] CreatePngHeader(
        int width,
        int height)
    {
        var data =
            new byte[24];

        new byte[]
        {
            137,
            80,
            78,
            71,
            13,
            10,
            26,
            10
        }
        .CopyTo(
            data,
            0);

        BinaryPrimitives
            .WriteInt32BigEndian(
                data.AsSpan(
                    16,
                    4),
                width);

        BinaryPrimitives
            .WriteInt32BigEndian(
                data.AsSpan(
                    20,
                    4),
                height);

        return data;
    }

    private static string CreateTempFile(
        string extension,
        byte[] data)
    {
        var path =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudioProtonBusTextureMetadataTests",
                Guid.NewGuid()
                    .ToString("N") +
                extension);

        Directory.CreateDirectory(
            Path.GetDirectoryName(
                path)!);

        File.WriteAllBytes(
            path,
            data);

        return path;
    }
}
