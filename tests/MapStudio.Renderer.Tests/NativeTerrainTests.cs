using MapStudio.Core.Omsi.Maps;
using MapStudio.Renderer.Scene;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeTerrainTests
{
    [Fact]
    public void SamplerUsesBilinearTerrainInterpolation()
    {
        var tile =
            new NativeSceneTile(
                new OmsiTileReference(0, 0, "tile_0_0.map"),
                new OmsiTileContent(
                    new OmsiTileSummary(true, 0, 0, 0),
                    Array.Empty<OmsiPlacedObject>(),
                    Array.Empty<OmsiPlacedSpline>(),
                    new OmsiTerrainGrid(
                        1,
                        [0, 10, 20, 30])));

        var height =
            NativeTerrainSampler.GetHeightAtLocalPoint(
                tile,
                150,
                150);

        Assert.InRange(height, 14.999, 15.001);
    }

    [Fact]
    public void TerrainBuilderUsesResolvedGroundTextureAndRepeating()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-NativeTerrain",
                Guid.NewGuid()
                    .ToString("N"));

        var mapDirectory =
            Path.Combine(
                root,
                "maps",
                "TestMap");

        var texturePath =
            Path.Combine(
                mapDirectory,
                "texture",
                "grass.bmp");

        try
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    texturePath)!);

            File.WriteAllBytes(
                texturePath,
                [1, 2, 3]);

            var tile =
                new NativeSceneTile(
                    new OmsiTileReference(
                        0,
                        0,
                        "tile_0_0.map"),
                    new OmsiTileContent(
                        new OmsiTileSummary(
                            true,
                            0,
                            0,
                            0),
                        [],
                        [],
                        new OmsiTerrainGrid(
                            1,
                            [0, 0, 0, 0])));

            var scene =
                new NativeSceneSnapshot(
                    [tile],
                    [],
                    [],
                    [
                        new NativeTerrainEntity(
                            tile.Reference,
                            tile.Content.Terrain!)
                    ]);

            var map =
                new OmsiMapDescriptor(
                    "TestMap",
                    "Test Map",
                    mapDirectory,
                    Path.Combine(
                        mapDirectory,
                        "global.cfg"),
                    false,
                    [tile.Reference],
                    [
                        new OmsiGroundTexture(
                            @"texture\grass.bmp",
                            @"texture\detail.bmp",
                            0,
                            2,
                            60)
                    ]);

            var geometry =
                new NativeTerrainTriangleGeometryBuilder()
                    .Build(
                        scene,
                        map,
                        root);

            var batch =
                Assert.Single(
                    geometry.MaterialBatches);

            Assert.Equal(
                Path.GetFullPath(
                    texturePath),
                batch.TexturePath);

            Assert.InRange(
                geometry.Vertices
                    .Max(
                        vertex =>
                            vertex.TexCoord.X),
                1.999f,
                2.001f);

            Assert.InRange(
                geometry.Vertices
                    .Max(
                        vertex =>
                            vertex.TexCoord.Y),
                1.999f,
                2.001f);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }

    [Fact]
    public void TerrainBuilderCreatesTwoTrianglesPerCell()
    {
        var tile =
            new NativeSceneTile(
                new OmsiTileReference(0, 0, "tile_0_0.map"),
                new OmsiTileContent(
                    new OmsiTileSummary(true, 0, 0, 0),
                    Array.Empty<OmsiPlacedObject>(),
                    Array.Empty<OmsiPlacedSpline>(),
                    new OmsiTerrainGrid(
                        1,
                        [0, 10, 20, 30])));

        var scene =
            new NativeSceneSnapshot(
                [tile],
                Array.Empty<NativeObjectEntity>(),
                Array.Empty<NativeSplineEntity>(),
                [
                    new NativeTerrainEntity(
                        tile.Reference,
                        tile.Content.Terrain!)
                ]);

        var geometry =
            new NativeTerrainTriangleGeometryBuilder().Build(scene);

        Assert.Equal(2, geometry.TriangleCount);
        Assert.Equal(6, geometry.Vertices.Length);

        Assert.Contains(
            geometry.Vertices,
            vertex =>
                Math.Abs(
                    vertex.Position.Y -
                    30.0f) <
                0.001f);
    }
}
