using MapStudio.Core.Omsi.Junctions;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Splines;
using MapStudio.Renderer.Picking;
using MapStudio.Renderer.Scene;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class MapStudioGeneratedJunctionRenderingTests
{
    [Fact]
    public async Task GeneratedJunctionUsesRoadKitAsphaltTexture()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-Junction-RoadKit-Texture-" +
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            Directory.CreateDirectory(
                root);

            await new MapStudioRoadKitGenerator()
                .InstallOrUpdateAsync(
                    root);

            var result =
                await new MapStudioJunctionAssetGenerator()
                    .GenerateAsync(
                        root,
                        new MapStudioJunctionSpec(
                            "TextureCross",
                            [
                                new(
                                    0,
                                    7,
                                    2),
                                new(
                                    90,
                                    7,
                                    2),
                                new(
                                    180,
                                    7,
                                    2),
                                new(
                                    270,
                                    7,
                                    2)
                            ]));

            var roadKitTexture =
                Path.Combine(
                    root,
                    "Splines",
                    MapStudioRoadKitGenerator
                        .PackFolderName,
                    "Texture",
                    "ms_asphalt.bmp");

            var junctionTexture =
                Path.Combine(
                    result.ObjectDirectory,
                    "Texture",
                    "ms_junction_asphalt.bmp");

            Assert.True(
                File.Exists(
                    roadKitTexture));

            Assert.True(
                File.Exists(
                    junctionTexture));

            Assert.Equal(
                await File.ReadAllBytesAsync(
                    roadKitTexture),
                await File.ReadAllBytesAsync(
                    junctionTexture));
        }
        finally
        {
            if (
                Directory.Exists(
                    root))
            {
                Directory.Delete(
                    root,
                    recursive:
                        true);
            }
        }
    }



    [Fact]
    public async Task GeneratedJunctionUsesRoadKitSidewalkTexture()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-Junction-RoadKit-Sidewalk-" +
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            Directory.CreateDirectory(
                root);

            await new MapStudioRoadKitGenerator()
                .InstallOrUpdateAsync(
                    root);

            var result =
                await new MapStudioJunctionAssetGenerator()
                    .GenerateAsync(
                        root,
                        new MapStudioJunctionSpec(
                            "SidewalkTextureCross",
                            [
                                new(
                                    0,
                                    11,
                                    2,
                                    false,
                                    3.5),
                                new(
                                    90,
                                    11,
                                    2,
                                    false,
                                    3.5),
                                new(
                                    180,
                                    11,
                                    2,
                                    false,
                                    3.5),
                                new(
                                    270,
                                    11,
                                    2,
                                    false,
                                    3.5)
                            ]));

            var roadKitTexture =
                Path.Combine(
                    root,
                    "Splines",
                    MapStudioRoadKitGenerator
                        .PackFolderName,
                    "Texture",
                    "ms_sidewalk.bmp");

            var junctionTexture =
                Path.Combine(
                    result.ObjectDirectory,
                    "Texture",
                    "ms_junction_sidewalk.bmp");

            Assert.True(
                File.Exists(
                    roadKitTexture));

            Assert.True(
                File.Exists(
                    junctionTexture));

            Assert.Equal(
                await File.ReadAllBytesAsync(
                    roadKitTexture),
                await File.ReadAllBytesAsync(
                    junctionTexture));
        }
        finally
        {
            if (
                Directory.Exists(
                    root))
            {
                Directory.Delete(
                    root,
                    recursive:
                        true);
            }
        }
    }


    [Fact]
    public async Task TerrainRelativeGeneratedJunctionIsNotDoubleRaised()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-Junction-Terrain-Relative-" +
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            Directory.CreateDirectory(
                root);

            await new MapStudioRoadKitGenerator()
                .InstallOrUpdateAsync(
                    root);

            var result =
                await new MapStudioJunctionAssetGenerator()
                    .GenerateAsync(
                        root,
                        new MapStudioJunctionSpec(
                            "TerrainCross",
                            [
                                new(
                                    0,
                                    7,
                                    2),
                                new(
                                    90,
                                    7,
                                    2),
                                new(
                                    180,
                                    7,
                                    2),
                                new(
                                    270,
                                    7,
                                    2)
                            ]));

            var sceneryRoot =
                Path.Combine(
                    root,
                    "Sceneryobjects");

            var relativePath =
                Path.GetRelativePath(
                    sceneryRoot,
                    result.SceneryObjectPath)
                    .Replace(
                        Path.DirectorySeparatorChar,
                        '\\');

            var asset =
                await new NativeSceneryAssetLoader()
                    .LoadAssetAsync(
                        root,
                        relativePath);

            Assert.True(
                asset.IsLoaded,
                asset.ErrorCode);

            Assert.False(
                asset.UsesAbsoluteHeight);

            var tile =
                new OmsiTileReference(
                    0,
                    0,
                    "tile_0_0.map");

            var terrain =
                new OmsiTerrainGrid(
                    1,
                    [
                        12,
                        12,
                        12,
                        12
                    ]);

            var placed =
                new OmsiPlacedObject(
                    "object",
                    relativePath,
                    1,
                    150,
                    150,
                    0,
                    0,
                    0,
                    0,
                    []);

            var entity =
                new NativeObjectEntity(
                    new PickingId(
                        PickingKind.Object,
                        1),
                    tile,
                    placed,
                    150,
                    0,
                    150);

            var scene =
                new NativeSceneSnapshot(
                    [
                        new NativeSceneTile(
                            tile,
                            new OmsiTileContent(
                                new OmsiTileSummary(
                                    true,
                                    1,
                                    0,
                                    0),
                                [
                                    placed
                                ],
                                [],
                                terrain))
                    ],
                    [
                        entity
                    ],
                    [],
                    [
                        new NativeTerrainEntity(
                            tile,
                            terrain)
                    ]);

            var geometry =
                new NativeObjectTriangleGeometryBuilder()
                    .Build(
                        scene,
                        new Dictionary<
                            string,
                            NativeSceneryAsset>(
                                StringComparer
                                    .OrdinalIgnoreCase)
                        {
                            [relativePath] =
                                asset
                        });

            Assert.True(
                geometry.Vertices.Length >
                0);

            var minimumHeight =
                geometry.Vertices
                    .Min(
                        vertex =>
                            vertex.Position.Y);

            var maximumHeight =
                geometry.Vertices
                    .Max(
                        vertex =>
                            vertex.Position.Y);

            Assert.InRange(
                minimumHeight,
                12.101f,
                12.103f);

            Assert.InRange(
                maximumHeight,
                12.105f,
                12.107f);

            Assert.All(
                geometry.Vertices,
                vertex =>
                    Assert.InRange(
                        vertex.Position.Y,
                        12.101f,
                        12.107f));
        }
        finally
        {
            if (
                Directory.Exists(
                    root))
            {
                Directory.Delete(
                    root,
                    recursive:
                        true);
            }
        }
    }

    [Fact]
    public async Task GeneratedJunctionLoadsItsOwnTextureAndMesh()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-GeneratedJunction-Render-" +
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            Directory.CreateDirectory(
                root);

            var result =
                await new MapStudioJunctionAssetGenerator()
                    .GenerateAsync(
                        root,
                        new MapStudioJunctionSpec(
                            "RenderCross",
                            [
                                new(
                                    0,
                                    11,
                                    2,
                                    false,
                                    3.5),
                                new(
                                    90,
                                    8.5,
                                    2,
                                    false,
                                    2.75),
                                new(
                                    180,
                                    11,
                                    2,
                                    false,
                                    3.5),
                                new(
                                    270,
                                    8.5,
                                    2,
                                    false,
                                    2.75)
                            ]));

            var sceneryRoot =
                Path.Combine(
                    root,
                    "Sceneryobjects");

            var relativePath =
                Path.GetRelativePath(
                    sceneryRoot,
                    result.SceneryObjectPath)
                    .Replace(
                        Path.DirectorySeparatorChar,
                        '\\');

            var asset =
                await new NativeSceneryAssetLoader()
                    .LoadAssetAsync(
                        root,
                        relativePath);

            Assert.True(
                asset.IsLoaded,
                asset.ErrorCode);

            var mesh =
                Assert.Single(
                    asset.Meshes);

            Assert.True(
                mesh.Geometry.IsLoaded);

            Assert.Equal(
                3,
                mesh.MaterialTexturePaths.Count);

            Assert.All(
                mesh.MaterialTexturePaths,
                texture =>
                {
                    Assert.False(
                        string.IsNullOrWhiteSpace(
                            texture));

                    Assert.True(
                        File.Exists(
                            texture!));
                });
        }
        finally
        {
            if (
                Directory.Exists(
                    root))
            {
                Directory.Delete(
                    root,
                    recursive:
                        true);
            }
        }
    }
}
