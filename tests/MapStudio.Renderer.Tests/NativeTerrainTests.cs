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

        var detailPath =
            Path.Combine(
                mapDirectory,
                "texture",
                "detail.bmp");

        try
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    texturePath)!);

            File.WriteAllBytes(
                texturePath,
                [1, 2, 3]);

            File.WriteAllBytes(
                detailPath,
                [4, 5, 6]);

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

            Assert.Equal(
                Path.GetFullPath(
                    detailPath),
                batch.DetailTexturePath);

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

            Assert.InRange(
                geometry.Vertices
                    .Max(
                        vertex =>
                            vertex.DetailTexCoord.X),
                59.999f,
                60.001f);

            Assert.InRange(
                geometry.Vertices
                    .Max(
                        vertex =>
                            vertex.DetailTexCoord.Y),
                59.999f,
                60.001f);
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
    public void TerrainBuilderAddsMaskedGroundLayerWithIndependentMaskUv()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-NativeTerrainMask",
                Guid.NewGuid()
                    .ToString("N"));

        var mapDirectory =
            Path.Combine(
                root,
                "maps",
                "TestMap");

        var textureDirectory =
            Path.Combine(
                mapDirectory,
                "texture");

        var mapTextureDirectory =
            Path.Combine(
                textureDirectory,
                "map");

        var baseTexture =
            Path.Combine(
                textureDirectory,
                "base.bmp");

        var layerTexture =
            Path.Combine(
                textureDirectory,
                "mud.bmp");

        var detailTexture =
            Path.Combine(
                textureDirectory,
                "detail.bmp");

        const string maskFileName =
            "tile_0_0.map.1.dds";

        var maskPath =
            Path.Combine(
                mapTextureDirectory,
                maskFileName);

        try
        {
            Directory.CreateDirectory(
                mapTextureDirectory);

            File.WriteAllBytes(
                baseTexture,
                [1]);

            File.WriteAllBytes(
                layerTexture,
                [2]);

            File.WriteAllBytes(
                detailTexture,
                [4]);

            File.WriteAllBytes(
                maskPath,
                [3]);

            var reference =
                new OmsiTileReference(
                    0,
                    0,
                    "tile_0_0.map");

            var tile =
                new NativeSceneTile(
                    reference,
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
                            [0, 0, 0, 0]),
                        TerrainTextureMasks:
                        [
                            new OmsiTerrainTextureMask(
                                1,
                                maskFileName,
                                1,
                                true,
                                2,
                                2,
                                false,
                                0,
                                0,
                                0,
                                null)
                        ]));

            var scene =
                new NativeSceneSnapshot(
                    [tile],
                    [],
                    [],
                    [
                        new NativeTerrainEntity(
                            reference,
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
                    [reference],
                    [
                        new OmsiGroundTexture(
                            @"texture\base.bmp",
                            @"texture\detail.bmp",
                            0,
                            1,
                            60),
                        new OmsiGroundTexture(
                            @"texture\mud.bmp",
                            @"texture\detail.bmp",
                            8,
                            4,
                            1)
                    ]);

            var geometry =
                new NativeTerrainTriangleGeometryBuilder()
                    .Build(
                        scene,
                        map,
                        root);

            Assert.Equal(
                4,
                geometry.TriangleCount);

            Assert.Equal(
                2,
                geometry.MaterialBatches.Count);

            Assert.Equal(
                1,
                geometry.MaskedLayerCount);

            var layerBatch =
                geometry.MaterialBatches[1];

            Assert.Equal(
                Path.GetFullPath(
                    layerTexture),
                layerBatch.TexturePath);

            Assert.Equal(
                Path.GetFullPath(
                    maskPath),
                layerBatch.MaskTexturePath);

            Assert.Equal(
                Path.GetFullPath(
                    detailTexture),
                layerBatch.DetailTexturePath);

            var overlayVertices =
                geometry.Vertices
                    .Skip(
                        layerBatch.StartVertex)
                    .Take(
                        layerBatch.VertexCount)
                    .ToArray();

            Assert.InRange(
                overlayVertices
                    .Max(
                        vertex =>
                            vertex.TexCoord.X),
                3.999f,
                4.001f);

            Assert.InRange(
                overlayVertices
                    .Max(
                        vertex =>
                            vertex.MaskTexCoord.X),
                0.999f,
                1.001f);

            Assert.InRange(
                overlayVertices
                    .Max(
                        vertex =>
                            vertex.MaskTexCoord.Y),
                0.999f,
                1.001f);

            Assert.InRange(
                overlayVertices
                    .Max(
                        vertex =>
                            vertex.DetailTexCoord.X),
                0.999f,
                1.001f);

            Assert.InRange(
                overlayVertices
                    .Max(
                        vertex =>
                            vertex.DetailTexCoord.Y),
                0.999f,
                1.001f);

            Assert.All(
                overlayVertices,
                vertex =>
                    Assert.InRange(
                        vertex.Position.Y,
                        0.0019f,
                        0.0021f));
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
    [Fact]
    public void TerrainBuilderAddsTileLightMapAsAdditiveLayer()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-TerrainLightMap",
                Guid.NewGuid().ToString("N"));

        var mapDirectory =
            Path.Combine(
                root,
                "maps",
                "TestMap");

        var textureDirectory =
            Path.Combine(
                mapDirectory,
                "texture");

        var baseTexture =
            Path.Combine(
                textureDirectory,
                "base.bmp");

        var lightMap =
            Path.Combine(
                mapDirectory,
                "tile_0_0.map.LM.bmp");

        try
        {
            Directory.CreateDirectory(
                textureDirectory);

            File.WriteAllBytes(
                baseTexture,
                [1]);

            File.WriteAllBytes(
                lightMap,
                [2]);

            var reference =
                new OmsiTileReference(
                    0,
                    0,
                    "tile_0_0.map");

            var tile =
                new NativeSceneTile(
                    reference,
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
                            reference,
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
                    [reference],
                    [
                        new OmsiGroundTexture(
                            @"texture\base.bmp",
                            @"texture\base.bmp",
                            0,
                            1,
                            1)
                    ]);

            var geometry =
                new NativeTerrainTriangleGeometryBuilder()
                    .Build(
                        scene,
                        map,
                        root);

            Assert.Equal(
                2,
                geometry.MaterialBatches.Count);

            var lightBatch =
                geometry.MaterialBatches[1];

            Assert.True(
                lightBatch.AdditiveLightMap);

            Assert.Equal(
                Path.GetFullPath(
                    lightMap),
                lightBatch.TexturePath);

            var lightVertices =
                geometry.Vertices
                    .Skip(
                        lightBatch.StartVertex)
                    .Take(
                        lightBatch.VertexCount)
                    .ToArray();

            Assert.InRange(
                lightVertices.Max(
                    vertex =>
                        vertex.TexCoord.X),
                0.999f,
                1.001f);

            Assert.All(
                lightVertices,
                vertex =>
                    Assert.InRange(
                        vertex.Position.Y,
                        0.039f,
                        0.041f));
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

}
