using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Splines;
using MapStudio.Renderer.Scene;
using MapStudio.Renderer.Viewport;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeProceduralJunctionPlanBuilderTests
{
    [Fact]
    public void RotatedCrossesReuseSameGeneratedAsset()
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

        var graph =
            new MapStudioRoadGraphBuilder()
                .Build(
                    [
                        new MapStudioRoadTrace(
                            "h1",
                            [
                                new(20, 50),
                                new(80, 50)
                            ],
                            "road",
                            2,
                            false,
                            7),
                        new MapStudioRoadTrace(
                            "v1",
                            [
                                new(50, 20),
                                new(50, 80)
                            ],
                            "road",
                            2,
                            false,
                            7),
                        new MapStudioRoadTrace(
                            "d1",
                            [
                                new(140, 130),
                                new(180, 170)
                            ],
                            "road",
                            2,
                            false,
                            7),
                        new MapStudioRoadTrace(
                            "d2",
                            [
                                new(140, 170),
                                new(180, 130)
                            ],
                            "road",
                            2,
                            false,
                            7)
                    ]);

        var plan =
            new NativeProceduralJunctionPlanBuilder()
                .Build(
                    scene,
                    graph);

        Assert.Equal(
            2,
            plan.Items.Count);

        Assert.Equal(
            1,
            plan.UniqueAssetCount);

        Assert.Equal(
            0,
            plan.SkippedJunctions);

        Assert.NotEqual(
            plan.Items[0]
                .Rotation,
            plan.Items[1]
                .Rotation);
    }

    [Fact]
    public void JunctionPlanCarriesTerrainHeightIntoWorldPlacement()
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
                    12,
                    12,
                    12,
                    12
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

        var graph =
            new MapStudioRoadGraphBuilder()
                .Build(
                    [
                        new MapStudioRoadTrace(
                            "horizontal",
                            [
                                new(20, 50),
                                new(80, 50)
                            ],
                            "road",
                            2,
                            false,
                            7),
                        new MapStudioRoadTrace(
                            "vertical",
                            [
                                new(50, 20),
                                new(50, 80)
                            ],
                            "road",
                            2,
                            false,
                            7)
                    ]);

        var item =
            Assert.Single(
                new NativeProceduralJunctionPlanBuilder()
                    .Build(
                        scene,
                        graph)
                    .Items);

        Assert.Equal(
            12,
            item.WorldPoint.Y,
            3);
    }

    [Fact]
    public void JunctionArmUsesPhysicalRoadKitWidthButKeepsLaneWidth()
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

        var profile =
            MapStudioStandardRoadCatalog
                .LocalWithSidewalk;

        var graph =
            new MapStudioRoadGraphBuilder()
                .Build(
                    [
                        new MapStudioRoadTrace(
                            "horizontal",
                            [
                                new(20, 50),
                                new(80, 50)
                            ],
                            profile.RelativePath,
                            profile.LaneCount,
                            profile.OneWay,
                            profile.TotalWidthMeters),
                        new MapStudioRoadTrace(
                            "vertical",
                            [
                                new(50, 20),
                                new(50, 80)
                            ],
                            profile.RelativePath,
                            profile.LaneCount,
                            profile.OneWay,
                            profile.TotalWidthMeters)
                    ]);

        var item =
            Assert.Single(
                new NativeProceduralJunctionPlanBuilder()
                    .Build(
                        scene,
                        graph)
                    .Items);

        Assert.All(
            item.Spec.Arms,
            arm =>
            {
                Assert.Equal(
                    8.5,
                    arm.WidthMeters,
                    3);

                Assert.Equal(
                    2.75,
                    arm.LaneWidthMeters,
                    3);
            });
    }

    [Fact]
    public void JunctionOutsideLoadedTerrainIsSkipped()
    {
        var graph =
            new MapStudioRoadGraphBuilder()
                .Build(
                    [
                        new MapStudioRoadTrace(
                            "a",
                            [
                                new(-10, 0),
                                new(10, 0)
                            ],
                            "road"),
                        new MapStudioRoadTrace(
                            "b",
                            [
                                new(0, -10),
                                new(0, 10)
                            ],
                            "road")
                    ]);

        var plan =
            new NativeProceduralJunctionPlanBuilder()
                .Build(
                    new NativeSceneSnapshot(
                        [],
                        [],
                        [],
                        []),
                    graph);

        Assert.Empty(
            plan.Items);

        Assert.Equal(
            1,
            plan.SkippedJunctions);
    }
}
