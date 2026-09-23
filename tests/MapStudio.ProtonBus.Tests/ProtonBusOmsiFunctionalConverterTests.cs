using System.Numerics;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Models;
using MapStudio.Core.Omsi.Scenery;
using MapStudio.Core.Omsi.Splines;
using MapStudio.Core.ProtonBus;
using Xunit;

namespace MapStudio.ProtonBus.Tests;

public sealed class ProtonBusOmsiFunctionalConverterTests
{
    [Fact]
    public void ConverterCreatesVehiclePathAndMarkersFromSpline()
    {
        var tile =
            new OmsiTileReference(
                1,
                -1,
                "tile.map");

        var spline =
            CreateSpline(
                id: 7,
                length: 10);

        var definition =
            new OmsiSplineDefinition(
                true,
                [],
                [])
            {
                Paths =
                [
                    new(
                        Type: 0,
                        X: 2,
                        Z: 0.1,
                        Width: 3,
                        Direction: 0)
                ]
            };

        var result =
            ProtonBusOmsiFunctionalConverter
                .Convert(
                    tile,
                    new OmsiTileContent(
                        new(
                            true,
                            ObjectCount: 0,
                            SplineCount: 1,
                            SplineAttachmentCount: 0),
                        [],
                        [
                            spline
                        ]),
                    new Dictionary<
                        string,
                        OmsiSplineDefinition>
                    {
                        [
                            spline.SplinePath
                        ] =
                            definition
                    },
                    new Dictionary<
                        string,
                        ProtonBusResolvedSceneryAsset>(),
                    new(
                        WaypointSpacing:
                            5));

        var path =
            Assert.Single(
                result.VehiclePaths);

        Assert.Equal(
            "pv_t1_-1_s7_p0_f",
            path.Prefix);

        Assert.False(
            path.IsSpawner);

        Assert.Equal(
            3,
            path.MaxPathsToCheck);

        Assert.Empty(
            result.PedestrianPaths);

        Assert.Empty(
            result.TrainPaths);

        Assert.Equal(
            3,
            result.MarkerScene.Meshes.Count);

        Assert.Equal(
            "pv_t1_-1_s7_p0_f.000",
            result
                .MarkerScene
                .Meshes[0]
                .Name);

        AssertVectorClose(
            new(
                302,
                0.1f,
                -300),
            result
                .MarkerScene
                .Meshes[0]
                .Vertices[0]
                .Position +
            new Vector3(
                0.015f,
                0,
                0.015f));
    }

    [Fact]
    public void BidirectionalPedestrianPathCreatesForwardAndReverseDefinitions()
    {
        var tile =
            new OmsiTileReference(
                0,
                0,
                "tile.map");

        var spline =
            CreateSpline(
                id: 3,
                length: 10);

        var definition =
            new OmsiSplineDefinition(
                true,
                [],
                [])
            {
                Paths =
                [
                    new(
                        Type: 1,
                        X: 0,
                        Z: 0.25,
                        Width: 2,
                        Direction: 2)
                ]
            };

        var result =
            ProtonBusOmsiFunctionalConverter
                .Convert(
                    tile,
                    new OmsiTileContent(
                        new(
                            true,
                            ObjectCount: 0,
                            SplineCount: 1,
                            SplineAttachmentCount: 0),
                        [],
                        [
                            spline
                        ]),
                    new Dictionary<
                        string,
                        OmsiSplineDefinition>
                    {
                        [
                            spline.SplinePath
                        ] =
                            definition
                    },
                    new Dictionary<
                        string,
                        ProtonBusResolvedSceneryAsset>(),
                    new(
                        WaypointSpacing:
                            10));

        Assert.Equal(
            2,
            result.PedestrianPaths.Count);

        Assert.Equal(
            [
                "pp_t0_0_s3_p0_f",
                "pp_t0_0_s3_p0_r"
            ],
            result.PedestrianPaths
                .Select(
                    item =>
                        item.Prefix)
                .ToArray());

        Assert.Equal(
            4,
            result.MarkerScene.Meshes.Count);

        var forwardStart =
            GetMarkerCenter(
                result
                    .MarkerScene
                    .Meshes[0]);

        var reverseStart =
            GetMarkerCenter(
                result
                    .MarkerScene
                    .Meshes[2]);

        AssertVectorClose(
            new(
                0,
                0.25f,
                0),
            forwardStart);

        AssertVectorClose(
            new(
                0,
                0.25f,
                10),
            reverseStart);
    }

    [Fact]
    public void ConverterCreatesSceneryVehiclePathAtTerrainAdjustedObjectPosition()
    {
        var tile =
            new OmsiTileReference(
                0,
                0,
                "tile.map");

        var placed =
            CreateObject(
                id: 11,
                x: 100,
                y: 50,
                z: 2);

        var asset =
            new ProtonBusResolvedSceneryAsset(
                Meshes:
                    Array.Empty<
                        ProtonBusResolvedSceneryMesh>(),
                UsesAbsoluteHeight:
                    false,
                Paths:
                [
                    new(
                        X: 1,
                        Y: 2,
                        Z: 0.1,
                        Rotation: 0,
                        Radius: 0,
                        Length: 5,
                        GradientStart: 0,
                        GradientEnd: 0,
                        Type: 0,
                        Width: 3,
                        Direction: 0,
                        BlinkerCode: 0,
                        TrafficLightIndex: null,
                        SwitchDirection: null,
                        CrossingProblem: false)
                ]);

        var content =
            new OmsiTileContent(
                new(
                    true,
                    ObjectCount: 1,
                    SplineCount: 0,
                    SplineAttachmentCount: 0,
                    TerrainMarkerPresent:
                        true,
                    TerrainFileExists:
                        true),
                [
                    placed
                ],
                [],
                Terrain:
                    new(
                        1,
                        [
                            10,
                            10,
                            10,
                            10
                        ]));

        var result =
            ProtonBusOmsiFunctionalConverter
                .Convert(
                    tile,
                    content,
                    new Dictionary<
                        string,
                        OmsiSplineDefinition>(),
                    new Dictionary<
                        string,
                        ProtonBusResolvedSceneryAsset>
                    {
                        [
                            placed
                                .SceneryObjectPath
                        ] =
                            asset
                    },
                    new(
                        WaypointSpacing:
                            5));

        var path =
            Assert.Single(
                result.VehiclePaths);

        Assert.Equal(
            "pv_t0_0_o11_p0_f",
            path.Prefix);

        var center =
            GetMarkerCenter(
                result
                    .MarkerScene
                    .Meshes[0]);

        AssertVectorClose(
            new(
                101,
                12.1f,
                52),
            center);
    }

    [Fact]
    public void ConverterCreatesStreetLightAndRealMarkerFromEnhancedLight()
    {
        var tile =
            new OmsiTileReference(
                0,
                0,
                "tile.map");

        var placed =
            CreateObject(
                id: 22,
                x: 20,
                y: 30,
                z: 1);

        var asset =
            new ProtonBusResolvedSceneryAsset(
                Meshes:
                    Array.Empty<
                        ProtonBusResolvedSceneryMesh>(),
                UsesAbsoluteHeight:
                    true,
                LightPoints:
                [
                    new(
                        Keyword:
                            "[light_enh_2]",
                        PositionX:
                            2,
                        PositionY:
                            3,
                        PositionZ:
                            4,
                        DirectionX:
                            null,
                        DirectionY:
                            null,
                        DirectionZ:
                            null,
                        Red:
                            255,
                        Green:
                            128,
                        Blue:
                            0,
                        Size:
                            1,
                        InnerAngle:
                            null,
                        OuterAngle:
                            null,
                        ActivationVariable:
                            null,
                        BrightnessVariable:
                            null,
                        MultiplicationFactor:
                            "0.75",
                        EffectTexture:
                            null,
                        RawValues:
                            Array.Empty<string>(),
                        Range:
                            25,
                        IsMapLight:
                            true)
                ]);

        var result =
            ProtonBusOmsiFunctionalConverter
                .Convert(
                    tile,
                    new OmsiTileContent(
                        new(
                            true,
                            ObjectCount: 1,
                            SplineCount: 0,
                            SplineAttachmentCount: 0),
                        [
                            placed
                        ],
                        []),
                    new Dictionary<
                        string,
                        OmsiSplineDefinition>(),
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

        var light =
            Assert.Single(
                result.StreetLights);

        Assert.Equal(
            "pl_t0_0_o22_l0",
            light.Prefix);

        Assert.NotNull(
            light.Real);

        Assert.Equal(
            25,
            light.Real!
                .Range,
            precision: 4);

        Assert.Equal(
            0.75,
            light.Real
                .Intensity,
            precision: 4);

        AssertVectorClose(
            new(
                1,
                128f / 255f,
                0),
            light.Real
                .Color);

        var marker =
            Assert.Single(
                result
                    .MarkerScene
                    .Meshes);

        Assert.Equal(
            "_pl_t0_0_o22_l0_real_",
            marker.Name);

        AssertVectorClose(
            new(
                22,
                5,
                33),
            GetMarkerCenter(
                marker));
    }

    [Fact]
    public void ConverterReportsTrafficLightAndBlinkerSemanticsWithoutGuessing()
    {
        var tile =
            new OmsiTileReference(
                0,
                0,
                "tile.map");

        var placed =
            CreateObject(
                id: 33,
                x: 0,
                y: 0,
                z: 0);

        var asset =
            new ProtonBusResolvedSceneryAsset(
                Meshes:
                    Array.Empty<
                        ProtonBusResolvedSceneryMesh>(),
                Paths:
                [
                    new(
                        X: 0,
                        Y: 0,
                        Z: 0.1,
                        Rotation: 0,
                        Radius: 0,
                        Length: 5,
                        GradientStart: 0,
                        GradientEnd: 0,
                        Type: 0,
                        Width: 3,
                        Direction: 0,
                        BlinkerCode: 2,
                        TrafficLightIndex: 1,
                        SwitchDirection: null,
                        CrossingProblem: false)
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
                                        0,
                                        30),
                                    new(
                                        6,
                                        30)
                                ])
                        ])
                ]);

        var result =
            ProtonBusOmsiFunctionalConverter
                .Convert(
                    tile,
                    new OmsiTileContent(
                        new(
                            true,
                            ObjectCount: 1,
                            SplineCount: 0,
                            SplineAttachmentCount: 0),
                        [
                            placed
                        ],
                        []),
                    new Dictionary<
                        string,
                        OmsiSplineDefinition>(),
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

        Assert.Contains(
            result.Issues,
            issue =>
                issue.Code ==
                "blinkerMappingPending");

        Assert.Contains(
            result.Issues,
            issue =>
                issue.Code ==
                "trafficLightPathLinkPending");

        Assert.Contains(
            result.Issues,
            issue =>
                issue.Code ==
                "trafficLightControllerConversionPending");
    }

    private static OmsiPlacedSpline CreateSpline(
        int id,
        double length) =>
        new(
            HeaderValue:
                "spline",
            SplinePath:
                @"Splines\Test\road.sli",
            SplineId:
                id,
            PreviousSplineId:
                -1,
            NextSplineId:
                -1,
            X:
                0,
            Z:
                0,
            Y:
                0,
            Rotation:
                0,
            Length:
                length,
            Radius:
                0,
            GradientStart:
                0,
            GradientEnd:
                0,
            IsHeightSpline:
                false,
            ExtraValues:
                []);

    private static OmsiPlacedObject CreateObject(
        int id,
        double x,
        double y,
        double z) =>
        new(
            HeaderValue:
                "object",
            SceneryObjectPath:
                @"Sceneryobjects\Test\object.sco",
            ObjectId:
                id,
            X:
                x,
            Y:
                y,
            Z:
                z,
            Rotation:
                0,
            Pitch:
                0,
            Bank:
                0,
            ExtraValues:
                []);

    private static Vector3 GetMarkerCenter(
        ProtonBusExportMesh marker)
    {
        var a =
            marker
                .Vertices[0]
                .Position;

        var b =
            marker
                .Vertices[1]
                .Position;

        var c =
            marker
                .Vertices[2]
                .Position;

        return
            (
                a +
                b +
                c
            ) /
            3.0f;
    }

    private static void AssertVectorClose(
        Vector3 expected,
        Vector3 actual)
    {
        Assert.Equal(
            expected.X,
            actual.X,
            precision: 3);

        Assert.Equal(
            expected.Y,
            actual.Y,
            precision: 3);

        Assert.Equal(
            expected.Z,
            actual.Z,
            precision: 3);
    }
}
