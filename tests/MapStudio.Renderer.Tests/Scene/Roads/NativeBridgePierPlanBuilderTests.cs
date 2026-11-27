using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Splines;
using MapStudio.Renderer.Scene;
using MapStudio.Renderer.Viewport;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeBridgePierPlanBuilderTests
{
    [Fact]
    public void BuilderCreatesTerrainAdaptivePiersForLayerOnlyBridgeRun()
    {
        var reference =
            new OmsiTileReference(
                0,
                0,
                "tile_0_0.map");
        var terrain =
            new OmsiTerrainGrid(
                4,
                Enumerable
                    .Repeat(
                        0f,
                        25)
                    .ToArray());
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
                .RoadTwoLaneWithSidewalk;
        var graph =
            new MapStudioRoadGraphBuilder()
                .Build(
                    [
                        new MapStudioRoadTrace(
                            "bridge-run",
                            [
                                new(30, 90),
                                new(90, 90),
                                new(150, 90),
                                new(210, 90),
                                new(270, 90)
                            ],
                            profile.RelativePath,
                            profile.LaneCount,
                            profile.OneWay,
                            profile.TotalWidthMeters,
                            Layer: 1,
                            SourceTopologyAuthoritative:
                                true)
                    ]);

        var placement =
            new NativeProceduralRoadPlacementBuilder()
                .Build(
                    scene,
                    graph);
        var plan =
            new NativeBridgePierPlanBuilder()
                .Build(
                    scene,
                    placement);

        Assert.True(
            plan.Items.Count >= 3);
        Assert.Equal(
            0,
            plan.SkippedSupportCount);

        Assert.All(
            plan.Items,
            item =>
            {
                Assert.True(
                    item.Spec.HeightMeters >=
                    1.25);
                Assert.InRange(
                    item.Spec.HeightMeters %
                        0.25,
                    0,
                    0.000001);
                Assert.True(
                    item.Spec.CapWidthMeters >=
                    4.0);
                Assert.InRange(
                    item.WorldPoint.Y,
                    -0.001f,
                    0.001f);
            });

        Assert.True(
            plan.UniqueAssetCount >= 1);
        Assert.All(
            terrain.Heights,
            height =>
                Assert.Equal(
                    0f,
                    height));
    }

    [Fact]
    public void BuilderDoesNotCreatePiersForTunnelRuns()
    {
        var reference =
            new OmsiTileReference(
                0,
                0,
                "tile_0_0.map");
        var terrain =
            new OmsiTerrainGrid(
                4,
                Enumerable
                    .Repeat(
                        0f,
                        25)
                    .ToArray());
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
                .RoadTwoLane;
        var graph =
            new MapStudioRoadGraphBuilder()
                .Build(
                    [
                        new MapStudioRoadTrace(
                            "tunnel-run",
                            [
                                new(30, 180),
                                new(150, 180),
                                new(270, 180)
                            ],
                            profile.RelativePath,
                            profile.LaneCount,
                            profile.OneWay,
                            profile.TotalWidthMeters,
                            Layer: -1,
                            SourceTopologyAuthoritative:
                                true)
                    ]);

        var placement =
            new NativeProceduralRoadPlacementBuilder()
                .Build(
                    scene,
                    graph);
        var plan =
            new NativeBridgePierPlanBuilder()
                .Build(
                    scene,
                    placement);

        Assert.Empty(
            plan.Items);
        Assert.Equal(
            0,
            plan.SkippedSupportCount);
    }
}
