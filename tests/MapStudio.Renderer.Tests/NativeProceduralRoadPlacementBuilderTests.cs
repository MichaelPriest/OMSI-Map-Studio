using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Splines;
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
    public void BuilderSelectsRoadKitBridgeAndTunnelSplineVariants()
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

        var profile =
            MapStudioStandardRoadCatalog
                .RoadTwoLaneWithSidewalk;

        var graph =
            new MapStudioRoadGraphBuilder()
                .Build(
                    [
                        new MapStudioRoadTrace(
                            "bridge",
                            [
                                new(20, 30),
                                new(80, 30)
                            ],
                            profile.RelativePath,
                            2,
                            false,
                            profile.TotalWidthMeters,
                            Layer:
                                1,
                            Bridge:
                                true,
                            SourceTopologyAuthoritative:
                                true),
                        new MapStudioRoadTrace(
                            "tunnel",
                            [
                                new(20, 90),
                                new(80, 90)
                            ],
                            profile.RelativePath,
                            2,
                            false,
                            profile.TotalWidthMeters,
                            Layer:
                                -1,
                            Tunnel:
                                true,
                            SourceTopologyAuthoritative:
                                true)
                    ]);

        var result =
            new NativeProceduralRoadPlacementBuilder()
                .Build(
                    scene,
                    graph);

        var bridge =
            Assert.Single(
                result.Requests,
                request =>
                    request.SourceBridge);

        var tunnel =
            Assert.Single(
                result.Requests,
                request =>
                    request.SourceTunnel);

        Assert.Equal(
            @"Splines\MapStudio_RoadKit\ms_road_2lane_7m_sidewalk_bridge.sli",
            bridge.SplinePath);

        Assert.Equal(
            @"Splines\MapStudio_RoadKit\ms_road_2lane_7m_sidewalk_tunnel.sli",
            tunnel.SplinePath);
    }

    [Fact]
    public void BuilderCarriesGradeSeparationMetadataIntoRequests()
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

        var graph =
            new MapStudioRoadGraphBuilder()
                .Build(
                    [
                        new MapStudioRoadTrace(
                            "bridge",
                            [
                                new(20, 30),
                                new(80, 30)
                            ],
                            "road.sli",
                            Layer:
                                1,
                            Bridge:
                                true,
                            SourceTopologyAuthoritative:
                                true),
                        new MapStudioRoadTrace(
                            "tunnel",
                            [
                                new(20, 90),
                                new(80, 90)
                            ],
                            "road.sli",
                            Layer:
                                -1,
                            Tunnel:
                                true,
                            SourceTopologyAuthoritative:
                                true),
                        new MapStudioRoadTrace(
                            "layered",
                            [
                                new(140, 30),
                                new(200, 30)
                            ],
                            "road.sli",
                            Layer:
                                2,
                            SourceTopologyAuthoritative:
                                true)
                    ]);

        var result =
            new NativeProceduralRoadPlacementBuilder()
                .Build(
                    scene,
                    graph);

        Assert.Equal(
            3,
            result.Requests.Count);

        Assert.Equal(
            1,
            result.BridgeRequestCount);

        Assert.Equal(
            1,
            result.TunnelRequestCount);

        Assert.Equal(
            3,
            result.LayeredRequestCount);

        var bridge =
            Assert.Single(
                result.Requests,
                request =>
                    request.SourceBridge);

        Assert.False(
            bridge.SourceTunnel);

        Assert.Equal(
            1,
            bridge.SourceLayer);

        var tunnel =
            Assert.Single(
                result.Requests,
                request =>
                    request.SourceTunnel);

        Assert.False(
            tunnel.SourceBridge);

        Assert.Equal(
            -1,
            tunnel.SourceLayer);

        var layered =
            Assert.Single(
                result.Requests,
                request =>
                    request.SourceLayer ==
                        2);

        Assert.False(
            layered.SourceBridge);

        Assert.False(
            layered.SourceTunnel);
    }

    [Fact]
    public void BuilderKeepsBridgeRunOnBoundaryGradeAcrossTerrainValley()
    {
        var reference =
            new OmsiTileReference(
                0,
                0,
                "tile_0_0.map");

        var heights =
            Enumerable
                .Range(
                    0,
                    5)
                .SelectMany(
                    _ =>
                        new float[]
                        {
                            10,
                            10,
                            0,
                            10,
                            10
                        })
                .ToArray();

        var terrain =
            new OmsiTerrainGrid(
                4,
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

        var graph =
            new MapStudioRoadGraphBuilder()
                .Build(
                    [
                        new MapStudioRoadTrace(
                            "bridge-run",
                            [
                                new(75, 75),
                                new(150, 75),
                                new(225, 75)
                            ],
                            "road.sli",
                            Layer:
                                1,
                            Bridge:
                                true,
                            SourceTopologyAuthoritative:
                                true)
                    ]);

        var result =
            new NativeProceduralRoadPlacementBuilder()
                .Build(
                    scene,
                    graph);

        Assert.Equal(
            2,
            result.Requests.Count);

        Assert.All(
            result.Requests,
            request =>
            {
                Assert.True(
                    request.SourceBridge);

                Assert.InRange(
                    request.StartWorld.Y,
                    9.999f,
                    10.001f);

                Assert.InRange(
                    request.EndWorld.Y,
                    9.999f,
                    10.001f);

                Assert.InRange(
                    Math.Abs(
                        request.GradientStart),
                    0,
                    0.001);

                Assert.InRange(
                    Math.Abs(
                        request.GradientEnd),
                    0,
                    0.001);
            });

        Assert.Equal(
            0f,
            terrain.Heights[2]);
    }


    [Fact]
    public void BuilderInfersLayerOnlyBridgeAndTunnelClearanceOnFlatTerrain()
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
                            "layer-bridge",
                            [
                                new(60, 75),
                                new(150, 75),
                                new(240, 75)
                            ],
                            profile.RelativePath,
                            profile.LaneCount,
                            profile.OneWay,
                            profile.TotalWidthMeters,
                            Layer:
                                1,
                            SourceTopologyAuthoritative:
                                true),
                        new MapStudioRoadTrace(
                            "layer-tunnel",
                            [
                                new(60, 225),
                                new(150, 225),
                                new(240, 225)
                            ],
                            profile.RelativePath,
                            profile.LaneCount,
                            profile.OneWay,
                            profile.TotalWidthMeters,
                            Layer:
                                -1,
                            SourceTopologyAuthoritative:
                                true)
                    ]);

        var result =
            new NativeProceduralRoadPlacementBuilder()
                .Build(
                    scene,
                    graph);

        Assert.Equal(
            4,
            result.Requests.Count);

        var bridgeRequests =
            result.Requests
                .Where(
                    request =>
                        request.SourceLayer ==
                            1)
                .OrderBy(
                    request =>
                        request.StartWorld.X)
                .ToArray();

        var tunnelRequests =
            result.Requests
                .Where(
                    request =>
                        request.SourceLayer ==
                            -1)
                .OrderBy(
                    request =>
                        request.StartWorld.X)
                .ToArray();

        Assert.Equal(
            2,
            bridgeRequests.Length);

        Assert.Equal(
            2,
            tunnelRequests.Length);

        Assert.All(
            bridgeRequests,
            request =>
            {
                Assert.False(
                    request.SourceBridge);

                Assert.EndsWith(
                    "_bridge.sli",
                    request.SplinePath,
                    StringComparison
                        .OrdinalIgnoreCase);
            });

        Assert.All(
            tunnelRequests,
            request =>
            {
                Assert.False(
                    request.SourceTunnel);

                Assert.EndsWith(
                    "_tunnel.sli",
                    request.SplinePath,
                    StringComparison
                        .OrdinalIgnoreCase);
            });

        Assert.InRange(
            bridgeRequests[0]
                .StartWorld.Y,
            -0.001f,
            0.001f);

        Assert.InRange(
            bridgeRequests[0]
                .EndWorld.Y,
            4.799f,
            4.801f);

        Assert.InRange(
            bridgeRequests[1]
                .StartWorld.Y,
            4.799f,
            4.801f);

        Assert.InRange(
            bridgeRequests[1]
                .EndWorld.Y,
            -0.001f,
            0.001f);

        Assert.InRange(
            tunnelRequests[0]
                .EndWorld.Y,
            -4.801f,
            -4.799f);

        Assert.InRange(
            tunnelRequests[1]
                .StartWorld.Y,
            -4.801f,
            -4.799f);

        Assert.All(
            terrain.Heights,
            height =>
                Assert.Equal(
                    0f,
                    height));
    }


    [Fact]
    public void BuilderKeepsLayerOnlyRoadEndsAtStructuralJunctionHeight()
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
                            "south",
                            [
                                new(150, 30),
                                new(150, 150)
                            ],
                            profile.RelativePath,
                            profile.LaneCount,
                            profile.OneWay,
                            profile.TotalWidthMeters,
                            Layer:
                                1,
                            SourceTopologyAuthoritative:
                                true),
                        new MapStudioRoadTrace(
                            "west",
                            [
                                new(30, 150),
                                new(150, 150)
                            ],
                            profile.RelativePath,
                            profile.LaneCount,
                            profile.OneWay,
                            profile.TotalWidthMeters,
                            Layer:
                                1,
                            SourceTopologyAuthoritative:
                                true),
                        new MapStudioRoadTrace(
                            "east",
                            [
                                new(270, 150),
                                new(150, 150)
                            ],
                            profile.RelativePath,
                            profile.LaneCount,
                            profile.OneWay,
                            profile.TotalWidthMeters,
                            Layer:
                                1,
                            SourceTopologyAuthoritative:
                                true)
                    ]);

        var result =
            new NativeProceduralRoadPlacementBuilder()
                .Build(
                    scene,
                    graph);

        Assert.Equal(
            3,
            result.Requests.Count);

        Assert.All(
            result.Requests,
            request =>
            {
                var elevatedEnd =
                    Math.Max(
                        request.StartWorld.Y,
                        request.EndWorld.Y);

                var groundEnd =
                    Math.Min(
                        request.StartWorld.Y,
                        request.EndWorld.Y);

                Assert.InRange(
                    elevatedEnd,
                    4.799f,
                    4.801f);

                Assert.InRange(
                    groundEnd,
                    -0.001f,
                    0.001f);
            });
    }

    [Fact]
    public void BuilderDoesNotMergeCurveAcrossGradeSeparationChange()
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

        var nodes =
            new[]
            {
                new MapStudioRoadGraphNode(
                    1,
                    new MapStudioRoadPoint(
                        20,
                        30),
                    1,
                    false,
                    new HashSet<string>
                    {
                        "mixed"
                    }),
                new MapStudioRoadGraphNode(
                    2,
                    new MapStudioRoadPoint(
                        60,
                        30),
                    2,
                    false,
                    new HashSet<string>
                    {
                        "mixed"
                    }),
                new MapStudioRoadGraphNode(
                    3,
                    new MapStudioRoadPoint(
                        90,
                        60),
                    1,
                    false,
                    new HashSet<string>
                    {
                        "mixed"
                    })
            };

        var graph =
            new MapStudioRoadGraph(
                nodes,
                [
                    new MapStudioRoadGraphSegment(
                        1,
                        1,
                        2,
                        "mixed",
                        "road.sli",
                        nodes[0].Position,
                        nodes[1].Position,
                        40,
                        2,
                        false,
                        7,
                        Layer:
                            0),
                    new MapStudioRoadGraphSegment(
                        2,
                        2,
                        3,
                        "mixed",
                        "road.sli",
                        nodes[1].Position,
                        nodes[2].Position,
                        Math.Sqrt(
                            1800),
                        2,
                        false,
                        7,
                        Layer:
                            1,
                        Bridge:
                            true)
                ],
                []);

        var result =
            new NativeProceduralRoadPlacementBuilder()
                .Build(
                    scene,
                    graph);

        Assert.Equal(
            2,
            result.Requests.Count);

        Assert.Equal(
            0,
            result.CurvedRequestCount);

        Assert.False(
            result.Requests[0]
                .SourceBridge);

        Assert.True(
            result.Requests[1]
                .SourceBridge);
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
