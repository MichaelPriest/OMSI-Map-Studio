using Xunit;
using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Splines;
using MapStudio.Renderer.Scene;
using MapStudio.Renderer.Viewport;

namespace MapStudio.Renderer.Tests;

public sealed class NativeTunnelPortalPlanBuilderTests
{
    [Fact]
    public void BuilderCreatesOnePortalAtEachEndOfLayerOnlyTunnelRun()
    {
        var reference = new OmsiTileReference(
            0,
            0,
            "tile_0_0.map");

        var terrain = new OmsiTerrainGrid(
            4,
            Enumerable.Repeat(0f, 25).ToArray());

        var scene = new NativeSceneSnapshot(
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

        var profile = MapStudioStandardRoadCatalog.RoadTwoLane;
        var graph = new MapStudioRoadGraphBuilder().Build(
            [
                new MapStudioRoadTrace(
                    "layer-tunnel",
                    [
                        new(60, 150),
                        new(150, 150),
                        new(240, 150)
                    ],
                    profile.RelativePath,
                    profile.LaneCount,
                    profile.OneWay,
                    profile.TotalWidthMeters,
                    Layer: -1,
                    SourceTopologyAuthoritative: true)
            ]);

        var placement =
            new NativeProceduralRoadPlacementBuilder()
                .Build(scene, graph);

        var plan =
            new NativeTunnelPortalPlanBuilder()
                .Build(scene, placement);

        Assert.Equal(2, plan.Items.Count);
        Assert.Equal(1, plan.UniqueAssetCount);
        Assert.Equal(0, plan.SkippedPortalCount);

        Assert.Contains(
            plan.Items,
            item => item.IsRunStart);

        Assert.Contains(
            plan.Items,
            item => !item.IsRunStart);

        Assert.All(
            plan.Items,
            item =>
            {
                Assert.StartsWith(
                    "MS_TunnelPortal_W",
                    item.AssetName,
                    StringComparison.Ordinal);
                Assert.True(
                    item.Spec.RoadWidthMeters >=
                    profile.TotalWidthMeters);
                Assert.Equal(
                    reference,
                    item.Tile);
            });

        Assert.All(
            terrain.Heights,
            height => Assert.Equal(0f, height));
    }

    [Fact]
    public void BuilderSamplesTerrainForIndependentWingHeights()
    {
        var reference =
            new OmsiTileReference(
                0,
                0,
                "tile_0_0.map");

        const int cellCount =
            60;

        var heights =
            new float[
                (cellCount + 1) *
                (cellCount + 1)];

        for (
            var row = 0;
            row <= cellCount;
            row++)
        {
            for (
                var column = 0;
                column <= cellCount;
                column++)
            {
                heights[
                    row *
                    (cellCount + 1) +
                    column] =
                    column;
            }
        }

        var terrain =
            new OmsiTerrainGrid(
                cellCount,
                heights);

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

        var request =
            new NativeSplinePlacementRequest(
                reference,
                @"Splines\MapStudio_RoadKit\ms_road_2lane_7m_tunnel.sli",
                -1,
                150,
                150,
                0,
                0,
                40,
                0,
                0,
                0,
                false,
                new System.Numerics.Vector3(
                    150,
                    30,
                    150),
                new System.Numerics.Vector3(
                    150,
                    30,
                    190),
                SourceLayer:
                    -1);

        var placement =
            new NativeProceduralRoadPlacementBuildResult(
                [request],
                [],
                0);

        var plan =
            new NativeTunnelPortalPlanBuilder()
                .Build(
                    scene,
                    placement);

        Assert.Equal(
            2,
            plan.Items.Count);

        Assert.Contains(
            plan.Items,
            item =>
                item.Spec.LeftWingHeightMeters !=
                item.Spec.RightWingHeightMeters);

        Assert.All(
            terrain.Heights,
            (height, index) =>
                Assert.Equal(
                    heights[index],
                    height));
    }

    [Fact]
    public void BuilderDoesNotCreatePortalsForBridgeRun()
    {
        var reference = new OmsiTileReference(
            0,
            0,
            "tile_0_0.map");

        var terrain = new OmsiTerrainGrid(
            4,
            Enumerable.Repeat(0f, 25).ToArray());

        var scene = new NativeSceneSnapshot(
            [
                new NativeSceneTile(
                    reference,
                    new OmsiTileContent(
                        new OmsiTileSummary(true, 0, 0, 0),
                        [],
                        [],
                        terrain))
            ],
            [],
            [],
            [
                new NativeTerrainEntity(reference, terrain)
            ]);

        var profile = MapStudioStandardRoadCatalog.RoadTwoLane;
        var graph = new MapStudioRoadGraphBuilder().Build(
            [
                new MapStudioRoadTrace(
                    "layer-bridge",
                    [
                        new(60, 150),
                        new(240, 150)
                    ],
                    profile.RelativePath,
                    profile.LaneCount,
                    profile.OneWay,
                    profile.TotalWidthMeters,
                    Layer: 1,
                    SourceTopologyAuthoritative: true)
            ]);

        var placement =
            new NativeProceduralRoadPlacementBuilder()
                .Build(scene, graph);

        var plan =
            new NativeTunnelPortalPlanBuilder()
                .Build(scene, placement);

        Assert.Empty(plan.Items);
        Assert.Equal(0, plan.SkippedPortalCount);
    }
}
