using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Vegetation;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Renderer.Scene;
using MapStudio.Renderer.Viewport;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeOsmVegetationPlacementBuilderTests
{
    [Fact]
    public void BuilderPlacesVegetationOnLoadedTerrain()
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
                    6,
                    6,
                    6,
                    6
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

        var points =
            new[]
            {
                new MapStudioProjectedVegetationPoint(
                    "tree-1",
                    new MapStudioRoadPoint(
                        25,
                        40),
                    MapStudioOsmVegetationKind.Tree,
                    null,
                    null,
                    null,
                    null)
            };

        var result =
            new NativeOsmVegetationPlacementBuilder()
                .Build(
                    scene,
                    points,
                    @"Sceneryobjects\Trees\tree.sco",
                    randomRotation:
                        false);

        var request =
            Assert.Single(
                result.Requests);

        Assert.Equal(
            0,
            result.SkippedPointCount);

        Assert.Equal(
            reference,
            request.Tile);

        Assert.Equal(
            25,
            request.X);

        Assert.Equal(
            40,
            request.Y);

        Assert.Equal(
            0,
            request.Z);

        Assert.Equal(
            0,
            request.Rotation);

        Assert.False(
            request.UsesAbsoluteHeight);

        Assert.InRange(
            request.WorldPoint.Y,
            5.999f,
            6.001f);
    }

    [Fact]
    public void BuilderSkipsPointWithoutLoadedTerrain()
    {
        var scene =
            new NativeSceneSnapshot(
                [],
                [],
                [],
                []);

        var points =
            new[]
            {
                new MapStudioProjectedVegetationPoint(
                    "tree-1",
                    new MapStudioRoadPoint(
                        25,
                        40),
                    MapStudioOsmVegetationKind.Tree,
                    null,
                    null,
                    null,
                    null)
            };

        var result =
            new NativeOsmVegetationPlacementBuilder()
                .Build(
                    scene,
                    points,
                    @"Sceneryobjects\Trees\tree.sco",
                    randomRotation:
                        true);

        Assert.Empty(
            result.Requests);

        Assert.Equal(
            1,
            result.SkippedPointCount);
    }

    [Fact]
    public void RandomRotationIsStableForSameOsmPoint()
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
                    0,
                    0,
                    0,
                    0
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
                "tree-stable",
                new MapStudioRoadPoint(
                    10,
                    10),
                MapStudioOsmVegetationKind.Tree,
                null,
                null,
                null,
                null);

        var builder =
            new NativeOsmVegetationPlacementBuilder();

        var first =
            Assert.Single(
                builder.Build(
                    scene,
                    [point],
                    "tree.sco",
                    randomRotation:
                        true)
                    .Requests);

        var second =
            Assert.Single(
                builder.Build(
                    scene,
                    [point],
                    "tree.sco",
                    randomRotation:
                        true)
                    .Requests);

        Assert.Equal(
            first.Rotation,
            second.Rotation);

        Assert.InRange(
            first.Rotation,
            0,
            359.99);
    }
}
