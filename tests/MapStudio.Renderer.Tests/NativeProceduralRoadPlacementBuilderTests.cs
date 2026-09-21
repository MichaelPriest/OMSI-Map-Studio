using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Renderer.Scene;
using MapStudio.Renderer.Viewport;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeProceduralRoadPlacementBuilderTests
{
    [Fact]
    public void BuilderCreatesOmsiRequestsFromGraphSegments()
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
                    10,
                    10,
                    10,
                    10
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
                            "road-a",
                            [
                                new(20, 30),
                                new(80, 30)
                            ],
                            @"Splines\MapStudio_RoadKit\ms_road_2lane_7m.sli")
                    ]);

        var result =
            new NativeProceduralRoadPlacementBuilder()
                .Build(
                    scene,
                    graph);

        var request =
            Assert.Single(
                result.Requests);

        Assert.Equal(
            0,
            result.SkippedSegments);

        Assert.Equal(
            reference,
            request.Tile);

        Assert.Equal(
            20,
            request.X,
            4);

        Assert.Equal(
            30,
            request.Y,
            4);

        Assert.Equal(
            10,
            request.Z,
            4);

        Assert.Equal(
            60,
            request.Length,
            4);

        Assert.Equal(
            90,
            request.Rotation,
            4);

        Assert.False(
            request.IsCurved);

        Assert.Equal(
            -1,
            request.PreviousSplineId);

        Assert.Equal(
            -1,
            request.NextSplineId);


        Assert.Empty(
            result.Links);
    }

    [Fact]
    public void BuilderPlansLinksOnlyAcrossLinearNodes()
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
                            "road-a",
                            [
                                new(20, 30),
                                new(80, 30),
                                new(120, 50)
                            ],
                            "road.sli"),
                        new MapStudioRoadTrace(
                            "cross",
                            [
                                new(80, 10),
                                new(80, 70)
                            ],
                            "road.sli")
                    ]);

        var result =
            new NativeProceduralRoadPlacementBuilder()
                .Build(
                    scene,
                    graph);

        Assert.DoesNotContain(
            result.Links,
            link =>
                result.Requests[
                    link.PreviousRequestIndex]
                    .EndWorld.X ==
                80 &&
                result.Requests[
                    link.PreviousRequestIndex]
                    .EndWorld.Z ==
                30);
    }

    [Fact]
    public void BuilderSkipsSegmentsOutsideLoadedTerrain()
    {
        var scene =
            new NativeSceneSnapshot(
                [],
                [],
                [],
                []);

        var graph =
            new MapStudioRoadGraphBuilder()
                .Build(
                    [
                        new MapStudioRoadTrace(
                            "road-a",
                            [
                                new(20, 30),
                                new(80, 30)
                            ],
                            "road.sli")
                    ]);

        var result =
            new NativeProceduralRoadPlacementBuilder()
                .Build(
                    scene,
                    graph);

        Assert.Empty(
            result.Requests);

        Assert.Equal(
            1,
            result.SkippedSegments);
    }
    [Fact]
    public void BuilderLinksSequentialDegreeTwoTraces()
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
                            "part-a",
                            [
                                new(20, 30),
                                new(80, 30)
                            ],
                            "road.sli"),
                        new MapStudioRoadTrace(
                            "part-b",
                            [
                                new(80, 30),
                                new(120, 30)
                            ],
                            "road.sli")
                    ]);

        Assert.Empty(
            graph.Junctions);

        var result =
            new NativeProceduralRoadPlacementBuilder()
                .Build(
                    scene,
                    graph);

        Assert.Equal(
            2,
            result.Requests.Count);

        var link =
            Assert.Single(
                result.Links);

        Assert.Equal(
            0,
            link.PreviousRequestIndex);

        Assert.Equal(
            1,
            link.NextRequestIndex);
    }

}
