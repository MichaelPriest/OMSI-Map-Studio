using System.Buffers.Binary;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Textures;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiTerrainTextureMaskTests
{
    [Fact]
    public void ReadTerrainTextureMasks_ValidatesA8DdsLayers()
    {
        var root =
            CreateRoot();

        try
        {
            var tilePath =
                Path.Combine(
                    root,
                    "tile_0_0.map");

            var maskDirectory =
                Path.Combine(
                    root,
                    "texture",
                    "map");

            Directory.CreateDirectory(
                maskDirectory);

            File.WriteAllText(
                tilePath,
                string.Empty);

            File.WriteAllBytes(
                Path.Combine(
                    maskDirectory,
                    "tile_0_0.map.2.dds"),
                CreateA8Dds(
                    2,
                    2,
                    [255, 255, 255, 255]));

            File.WriteAllBytes(
                Path.Combine(
                    maskDirectory,
                    "tile_0_0.map.1.dds"),
                CreateA8Dds(
                    2,
                    2,
                    [0, 255, 0, 255]));

            File.WriteAllBytes(
                Path.Combine(
                    maskDirectory,
                    "tile_0_0.map.bad.dds"),
                [1]);

            var masks =
                OmsiTileReader
                    .ReadTerrainTextureMasks(
                        tilePath);

            Assert.Equal(
                2,
                masks.Count);

            Assert.Equal(
                1,
                masks[0].LayerIndex);
            Assert.True(
                masks[0].IsValid);
            Assert.Equal(
                2,
                masks[0].Width);
            Assert.Equal(
                2,
                masks[0].Height);
            Assert.False(
                masks[0].HasPixelStatistics);
            Assert.Equal(
                0,
                masks[0].Coverage);
            Assert.Equal(
                (byte)0,
                masks[0].MinimumAlpha);
            Assert.Equal(
                (byte)0,
                masks[0].MaximumAlpha);
            Assert.Null(
                masks[0].ErrorCode);

            Assert.Equal(
                2,
                masks[1].LayerIndex);
            Assert.True(
                masks[1].IsValid);
            Assert.False(
                masks[1].HasPixelStatistics);
            Assert.Equal(
                2,
                masks[1].Width);
            Assert.Equal(
                2,
                masks[1].Height);
        }
        finally
        {
            Directory.Delete(
                root,
                recursive: true);
        }
    }

    [Fact]
    public void TerrainTextureMaskReader_RejectsUnsupportedDds()
    {
        var root =
            CreateRoot();

        try
        {
            var path =
                Path.Combine(
                    root,
                    "tile_0_0.map.1.dds");

            File.WriteAllBytes(
                path,
                new byte[128]);

            var mask =
                new OmsiTerrainTextureMaskReader()
                    .Read(
                        1,
                        path);

            Assert.False(mask.IsValid);
            Assert.Equal(
                "invalidSignature",
                mask.ErrorCode);
            Assert.Equal(
                0,
                mask.Coverage);
        }
        finally
        {
            Directory.Delete(
                root,
                recursive: true);
        }
    }

    [Fact]
    public void TerrainTextureMaskReader_HeaderMode_SkipsPixelStatistics()
    {
        var root =
            CreateRoot();

        try
        {
            var path =
                Path.Combine(
                    root,
                    "tile_0_0.map.1.dds");

            File.WriteAllBytes(
                path,
                CreateA8Dds(
                    2,
                    2,
                    [0, 255, 0, 255]));

            var mask =
                new OmsiTerrainTextureMaskReader()
                    .ReadHeader(
                        1,
                        path);

            Assert.True(mask.IsValid);
            Assert.False(
                mask.HasPixelStatistics);
            Assert.Equal(2, mask.Width);
            Assert.Equal(2, mask.Height);
            Assert.Equal(0, mask.Coverage);
            Assert.Equal(
                (byte)0,
                mask.MinimumAlpha);
            Assert.Equal(
                (byte)0,
                mask.MaximumAlpha);
        }
        finally
        {
            Directory.Delete(
                root,
                recursive: true);
        }
    }

    [Fact]
    public void TerrainTextureMaskReader_DetectsEmptyMask()
    {
        var root =
            CreateRoot();

        try
        {
            var path =
                Path.Combine(
                    root,
                    "tile_0_0.map.1.dds");

            File.WriteAllBytes(
                path,
                CreateA8Dds(
                    4,
                    4,
                    new byte[16]));

            var mask =
                new OmsiTerrainTextureMaskReader()
                    .Read(
                        1,
                        path);

            Assert.True(mask.IsValid);
            Assert.True(
                mask.HasPixelStatistics);
            Assert.Equal(
                0,
                mask.Coverage);
            Assert.Equal(
                (byte)0,
                mask.MaximumAlpha);
        }
        finally
        {
            Directory.Delete(
                root,
                recursive: true);
        }
    }

    [Fact]
    public void TerrainTextureMaskResolver_ResolvesKnownLayer()
    {
        var root =
            CreateRoot();

        try
        {
            var tilePath =
                Path.Combine(
                    root,
                    "tile_1_-2.map");

            var maskDirectory =
                Path.Combine(
                    root,
                    "texture",
                    "map");

            Directory.CreateDirectory(
                maskDirectory);

            File.WriteAllText(
                tilePath,
                string.Empty);

            var maskPath =
                Path.Combine(
                    maskDirectory,
                    "tile_1_-2.map.3.dds");

            File.WriteAllBytes(
                maskPath,
                [1, 2, 3]);

            Assert.True(
                OmsiTextureAssetPathResolver
                    .TryResolveTerrainTextureMask(
                        root,
                        "tile_1_-2.map",
                        3,
                        out var resolved));

            Assert.Equal(
                Path.GetFullPath(
                    maskPath),
                resolved);
        }
        finally
        {
            Directory.Delete(
                root,
                recursive: true);
        }
    }

    [Fact]
    public void TerrainTextureMaskResolver_RejectsInvalidLayerAndTraversal()
    {
        var root =
            CreateRoot();

        try
        {
            Directory.CreateDirectory(
                Path.Combine(
                    root,
                    "texture",
                    "map"));

            Assert.False(
                OmsiTextureAssetPathResolver
                    .TryResolveTerrainTextureMask(
                        root,
                        "tile_0_0.map",
                        0,
                        out _));

            Assert.False(
                OmsiTextureAssetPathResolver
                    .TryResolveTerrainTextureMask(
                        root,
                        @"..\outside.map",
                        1,
                        out _));
        }
        finally
        {
            Directory.Delete(
                root,
                recursive: true);
        }
    }

    private static byte[] CreateA8Dds(
        int width,
        int height,
        IReadOnlyList<byte> alpha)
    {
        Assert.Equal(
            width * height,
            alpha.Count);

        var bytes =
            new byte[
                128 +
                alpha.Count];

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
                bytes.AsSpan(8, 4),
                0x00001007);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                bytes.AsSpan(12, 4),
                height);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                bytes.AsSpan(16, 4),
                width);

        BinaryPrimitives
            .WriteUInt32LittleEndian(
                bytes.AsSpan(76, 4),
                32);

        BinaryPrimitives
            .WriteUInt32LittleEndian(
                bytes.AsSpan(80, 4),
                0x00000002);

        BinaryPrimitives
            .WriteUInt32LittleEndian(
                bytes.AsSpan(88, 4),
                8);

        BinaryPrimitives
            .WriteUInt32LittleEndian(
                bytes.AsSpan(104, 4),
                0xFF);

        BinaryPrimitives
            .WriteUInt32LittleEndian(
                bytes.AsSpan(108, 4),
                0x00001002);

        for (
            var index = 0;
            index < alpha.Count;
            index++)
        {
            bytes[
                128 +
                index] =
                alpha[index];
        }

        return bytes;
    }

    private static string CreateRoot()
    {
        var path =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-TerrainMaskTests",
                Guid.NewGuid()
                    .ToString("N"));

        Directory.CreateDirectory(path);
        return path;
    }
}
