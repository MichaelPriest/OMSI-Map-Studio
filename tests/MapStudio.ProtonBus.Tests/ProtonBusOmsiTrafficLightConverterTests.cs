using System.Numerics;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Scenery;
using MapStudio.Core.ProtonBus;
using Xunit;

namespace MapStudio.ProtonBus.Tests;

public sealed class ProtonBusOmsiTrafficLightConverterTests
{
    [Fact]
    public void ConverterBuildsExactSynchronizedTimelineAndTriggers()
    {
        var tile =
            new OmsiTileReference(
                0,
                0,
                "tile.map");

        var placed =
            CreateObject(
                50);

        var asset =
            new ProtonBusResolvedSceneryAsset(
                Meshes:
                    Array.Empty<
                        ProtonBusResolvedSceneryMesh>(),
                Paths:
                [
                    CreatePath(
                        x:
                            1,
                        trafficLightIndex:
                            0),
                    CreatePath(
                        x:
                            5,
                        trafficLightIndex:
                            1)
                ],
                TrafficLightControllers:
                [
                    new(
                        CycleDuration:
                            60,
                        Programs:
                        [
                            new(
                                "Main",
                                [
                                    new(
                                        3,
                                        2),
                                    new(
                                        6,
                                        25),
                                    new(
                                        9,
                                        3),
                                    new(
                                        0,
                                        30)
                                ]),
                            new(
                                "Side",
                                [
                                    new(
                                        0,
                                        30),
                                    new(
                                        3,
                                        2),
                                    new(
                                        6,
                                        25),
                                    new(
                                        9,
                                        3)
                                ])
                        ])
                ]);

        var result =
            ProtonBusOmsiTrafficLightConverter
                .Convert(
                    tile,
                    CreateContent(
                        placed),
                    new Dictionary<
                        string,
                        ProtonBusResolvedSceneryAsset>
                    {
                        [
                            placed
                                .SceneryObjectPath
                        ] =
                            asset
                    });

        var definition =
            Assert.Single(
                result.TrafficLights);

        Assert.Equal(
            "tl_t0_0_o50_c0",
            definition.Prefix);

        Assert.Equal(
            2,
            definition.PathCount);

        Assert.Equal(
            1,
            definition.TickInterval,
            precision:
                6);

        Assert.Equal(
            [
                2,
                25,
                3,
                2,
                25,
                3
            ],
            definition.Ticks
                .Select(
                    tick =>
                        tick.Repeat)
                .ToArray());

        var first =
            definition.Ticks[0];

        Assert.True(
            first.Paths[1]
                .Red);

        Assert.True(
            first.Paths[1]
                .Yellow);

        Assert.False(
            first.Paths[1]
                .Green);

        Assert.True(
            first.Paths[1]
                .Trigger);

        Assert.True(
            first.Paths[2]
                .Red);

        Assert.True(
            first.Paths[2]
                .Trigger);

        var second =
            definition.Ticks[1];

        Assert.True(
            second.Paths[1]
                .Green);

        Assert.False(
            second.Paths[1]
                .Trigger);

        Assert.True(
            second.Paths[2]
                .Red);

        Assert.Equal(
            2,
            result
                .MarkerScene
                .Meshes
                .Count);

        Assert.Equal(
            "_tl_t0_0_o50_c0_path1_trigger_",
            result
                .MarkerScene
                .Meshes[0]
                .Name);

        Assert.Equal(
            "_tl_t0_0_o50_c0_path2_trigger_",
            result
                .MarkerScene
                .Meshes[1]
                .Name);

        AssertVectorClose(
            new(
                1,
                0.1f,
                0),
            MarkerCenter(
                result
                    .MarkerScene
                    .Meshes[0]));

        AssertVectorClose(
            new(
                5,
                0.1f,
                0),
            MarkerCenter(
                result
                    .MarkerScene
                    .Meshes[1]));

        Assert.Empty(
            result.Issues);
    }

    [Fact]
    public void ConverterExtendsFinalProgramStateToGroupCycle()
    {
        var tile =
            new OmsiTileReference(
                0,
                0,
                "tile.map");

        var placed =
            CreateObject(
                51);

        var asset =
            new ProtonBusResolvedSceneryAsset(
                Meshes:
                    Array.Empty<
                        ProtonBusResolvedSceneryMesh>(),
                Paths:
                [
                    CreatePath(
                        x:
                            0,
                        trafficLightIndex:
                            0)
                ],
                TrafficLightControllers:
                [
                    new(
                        CycleDuration:
                            60,
                        Programs:
                        [
                            new(
                                "Ped",
                                [
                                    new(
                                        6,
                                        20),
                                    new(
                                        0,
                                        0)
                                ])
                        ])
                ]);

        var result =
            ProtonBusOmsiTrafficLightConverter
                .Convert(
                    tile,
                    CreateContent(
                        placed),
                    new Dictionary<
                        string,
                        ProtonBusResolvedSceneryAsset>
                    {
                        [
                            placed
                                .SceneryObjectPath
                        ] =
                            asset
                    });

        var definition =
            Assert.Single(
                result.TrafficLights);

        Assert.Equal(
            2,
            definition.Ticks.Count);

        Assert.Equal(
            20,
            definition
                .Ticks[0]
                .Repeat);

        Assert.True(
            definition
                .Ticks[0]
                .Paths[1]
                .Green);

        Assert.Equal(
            40,
            definition
                .Ticks[1]
                .Repeat);

        Assert.True(
            definition
                .Ticks[1]
                .Paths[1]
                .Red);

        Assert.True(
            definition
                .Ticks[1]
                .Paths[1]
                .Trigger);
    }

    [Fact]
    public void ConverterRejectsProgramLongerThanGroupCycle()
    {
        var tile =
            new OmsiTileReference(
                0,
                0,
                "tile.map");

        var placed =
            CreateObject(
                52);

        var asset =
            new ProtonBusResolvedSceneryAsset(
                Meshes:
                    Array.Empty<
                        ProtonBusResolvedSceneryMesh>(),
                TrafficLightControllers:
                [
                    new(
                        CycleDuration:
                            10,
                        Programs:
                        [
                            new(
                                "Broken",
                                [
                                    new(
                                        6,
                                        12)
                                ])
                        ])
                ]);

        var result =
            ProtonBusOmsiTrafficLightConverter
                .Convert(
                    tile,
                    CreateContent(
                        placed),
                    new Dictionary<
                        string,
                        ProtonBusResolvedSceneryAsset>
                    {
                        [
                            placed
                                .SceneryObjectPath
                        ] =
                            asset
                    });

        Assert.Empty(
            result.TrafficLights);

        Assert.Contains(
            result.Issues,
            issue =>
                issue.Code ==
                "trafficLightProgramExceedsCycle");
    }

    [Fact]
    public void ConverterDoesNotGuessControllerWhenSceneryHasMultipleGroups()
    {
        var tile =
            new OmsiTileReference(
                0,
                0,
                "tile.map");

        var placed =
            CreateObject(
                53);

        var controller =
            new OmsiTrafficLightController(
                30,
                [
                    new(
                        "Main",
                        [
                            new(
                                6,
                                15),
                            new(
                                0,
                                15)
                        ])
                ]);

        var asset =
            new ProtonBusResolvedSceneryAsset(
                Meshes:
                    Array.Empty<
                        ProtonBusResolvedSceneryMesh>(),
                TrafficLightControllers:
                [
                    controller,
                    controller
                ]);

        var result =
            ProtonBusOmsiTrafficLightConverter
                .Convert(
                    tile,
                    CreateContent(
                        placed),
                    new Dictionary<
                        string,
                        ProtonBusResolvedSceneryAsset>
                    {
                        [
                            placed
                                .SceneryObjectPath
                        ] =
                            asset
                    });

        Assert.Empty(
            result.TrafficLights);

        Assert.Contains(
            result.Issues,
            issue =>
                issue.Code ==
                "trafficLightControllerAmbiguous");
    }

    private static OmsiPlacedObject CreateObject(
        int id) =>
        new(
            HeaderValue:
                "object",
            SceneryObjectPath:
                @"Sceneryobjects\Test\traffic.sco",
            ObjectId:
                id,
            X:
                0,
            Y:
                0,
            Z:
                0,
            Rotation:
                0,
            Pitch:
                0,
            Bank:
                0,
            ExtraValues:
                []);

    private static OmsiSceneryPathDefinition CreatePath(
        double x,
        int trafficLightIndex) =>
        new(
            X:
                x,
            Y:
                0,
            Z:
                0.1,
            Rotation:
                0,
            Radius:
                0,
            Length:
                5,
            GradientStart:
                0,
            GradientEnd:
                0,
            Type:
                0,
            Width:
                3,
            Direction:
                0,
            BlinkerCode:
                0,
            TrafficLightIndex:
                trafficLightIndex,
            SwitchDirection:
                null,
            CrossingProblem:
                false);

    private static OmsiTileContent CreateContent(
        OmsiPlacedObject placed) =>
        new(
            new(
                true,
                ObjectCount:
                    1,
                SplineCount:
                    0,
                SplineAttachmentCount:
                    0),
            [
                placed
            ],
            []);

    private static Vector3 MarkerCenter(
        ProtonBusExportMesh marker) =>
        (
            marker
                .Vertices[0]
                .Position +
            marker
                .Vertices[1]
                .Position +
            marker
                .Vertices[2]
                .Position
        ) /
        3.0f;

    private static void AssertVectorClose(
        Vector3 expected,
        Vector3 actual)
    {
        Assert.Equal(
            expected.X,
            actual.X,
            precision:
                3);

        Assert.Equal(
            expected.Y,
            actual.Y,
            precision:
                3);

        Assert.Equal(
            expected.Z,
            actual.Z,
            precision:
                3);
    }
}
