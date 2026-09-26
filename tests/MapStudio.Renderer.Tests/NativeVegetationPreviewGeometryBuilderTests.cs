using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Vegetation;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Renderer.Scene;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeVegetationPreviewGeometryBuilderTests
{
    [Fact]
    public void BuilderDrawsMarkerAtTerrainHeight()
    {
        var reference =
            new OmsiTileReference(
                0,
                0,
                "tile_0_0.map");

        var terrain =
            new OmsiTerrainGrid(
                1,
                [
                    3,
                    3,
                    3,
                    3
                ]);

        var scene =
            new NativeSceneSnapshot(
                [
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
                            terrain))
                ],
                [],
                [],
                [
                    new NativeTerrainEntity(
                        reference,
                        terrain)
                ]);

        var point =
            new MapStudioProjectedVegetationPoint(
                "tree",
                new MapStudioRoadPoint(
                    20,
                    30),
                MapStudioOsmVegetationKind.Tree,
                null,
                null,
                null,
                null);

        var geometry =
            new NativeVegetationPreviewGeometryBuilder()
                .Build(
                    scene,
                    [point]);

        Assert.Equal(
            1,
            geometry.RenderedPointCount);

        Assert.Equal(
            0,
            geometry.SkippedPointCount);

        Assert.Equal(
            6,
            geometry.Vertices.Length);

        Assert.Contains(
            geometry.Vertices,
            vertex =>
                vertex.Position.Y >
                5.4f);
    }

    [Fact]
    public void BuilderSkipsPointOutsideLoadedTerrain()
    {
        var scene =
            new NativeSceneSnapshot(
                [],
                [],
                [],
                []);

        var point =
            new MapStudioProjectedVegetationPoint(
                "tree",
                new MapStudioRoadPoint(
                    20,
                    30),
                MapStudioOsmVegetationKind.Tree,
                null,
                null,
                null,
                null);

        var geometry =
            new NativeVegetationPreviewGeometryBuilder()
                .Build(
                    scene,
                    [point]);

        Assert.Equal(
            0,
            geometry.RenderedPointCount);

        Assert.Equal(
            1,
            geometry.SkippedPointCount);

        Assert.Empty(
            geometry.Vertices);
    }
}
