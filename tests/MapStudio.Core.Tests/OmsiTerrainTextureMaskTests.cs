using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Textures;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiTerrainTextureMaskTests
{
    [Fact]
    public void ReadTerrainTextureMasks_FindsNumberedDdsLayers()
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
                [1, 2]);

            File.WriteAllBytes(
                Path.Combine(
                    maskDirectory,
                    "tile_0_0.map.1.dds"),
                [1]);

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
            Assert.Equal(
                1,
                masks[0].FileSize);

            Assert.Equal(
                2,
                masks[1].LayerIndex);
            Assert.Equal(
                2,
                masks[1].FileSize);
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
