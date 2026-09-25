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
    public void BuilderConvertsSmoothThreePointBendIntoOmsiArc()
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
                            "curve",
                            [
                                new(20, 30),
                                new(60, 30),
                                new(90, 60)
                            ],
                            @"Splines\MapStudio_RoadKit\ms_road_2lane_7m.sli",
                            2,
                            false,
                            7)
                    ]);

        var result =
            new NativeProceduralRoadPlacementBuilder()
                .Build(
                    scene,
                    graph);

        var request =
            Assert.Single(
                result.Requests);

        Assert.True(
            request.IsCurved);

        Assert.True(
            Math.Abs(
                request.Radius) >
            4);

        Assert.Equal(
            1,
            result.CurvedRequestCount);

        Assert.InRange(
            request.EndWorld.X,
            89.99f,
            90.01f);

        Assert.InRange(
            request.EndWorld.Z,
            59.99f,
            60.01f);
    }

    [Fact]
    public void BuilderTrimsRoadEndsAwayFromJunctionCenter()
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
                            "horizontal",
                            [
                                new(10, 50),
                                new(90, 50)
                            ],
                            @"Splines\MapStudio_RoadKit\ms_road_2lane_7m.sli",
                            2,
                            false,
                            7),
                        new MapStudioRoadTrace(
                            "vertical",
                            [
                                new(50, 10),
                                new(50, 90)
                            ],
                            @"Splines\MapStudio_RoadKit\ms_road_2lane_7m.sli",
                            2,
                            false,
                            7)
                    ]);

        var result =
            new NativeProceduralRoadPlacementBuilder()
                .Build(
                    scene,
                    graph);

        Assert.Equal(
            4,
            result.Requests.Count);

        Assert.All(
            result.Requests,
            request =>
            {
                var startDistance =
                    Math.Sqrt(
                        Math.Pow(
                            request.StartWorld.X -
                            50,
                            2) +
                        Math.Pow(
                            request.StartWorld.Z -
                            50,
                            2));

                var endDistance =
                    Math.Sqrt(
                        Math.Pow(
                            request.EndWorld.X -
                            50,
                            2) +
                        Math.Pow(
                            request.EndWorld.Z -
                            50,
                            2));

                Assert.True(
                    startDistance >
                        2 ||
                    endDistance >
                        2);

                Assert.False(
                    startDistance <
                        0.5 ||
                    endDistance <
                        0.5);
            });
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
