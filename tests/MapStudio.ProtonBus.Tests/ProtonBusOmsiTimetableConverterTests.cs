using System.Numerics;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Models;
using MapStudio.Core.Omsi.Timetables;
using MapStudio.Core.ProtonBus;
using Xunit;

namespace MapStudio.ProtonBus.Tests;

public sealed class ProtonBusOmsiTimetableConverterTests
{
    [Fact]
    public void ConverterResolvesBusStopObjectAndCreatesEntrypoint()
    {
        var tile =
            new OmsiTileReference(
                0,
                0,
                "tile.map");

        var placedStop =
            new OmsiPlacedObject(
                HeaderValue:
                    "object",
                SceneryObjectPath:
                    @"Sceneryobjects\Test\stop.sco",
                ObjectId:
                    100,
                X:
                    10,
                Y:
                    20,
                Z:
                    2,
                Rotation:
                    90,
                Pitch:
                    0,
                Bank:
                    0,
                ExtraValues:
                    []);

        var content =
            new OmsiTileContent(
                new(
                    true,
                    ObjectCount:
                        1,
                    SplineCount:
                        0,
                    SplineAttachmentCount:
                        0,
                    TerrainMarkerPresent:
                        true,
                    TerrainFileExists:
                        true),
                [
                    placedStop
                ],
                [],
                Terrain:
                    new(
                        1,
                        [
                            5,
                            5,
                            5,
                            5
                        ]));

        var stop =
            new OmsiTimetableBusStop(
                Name:
                    "São José",
                TileIndex:
                    0,
                Id:
                    100,
                ExitingPassengers:
                    null,
                Line4:
                    string.Empty,
                Line5:
                    string.Empty,
                SubName:
                    string.Empty);

        var trip =
            new OmsiTimetableTrip(
                FilePath:
                    "trip.ttp",
                RelativePath:
                    "TTData/trip.ttp",
                Name:
                    "Trip Centro",
                Comment1:
                    string.Empty,
                Comment2:
                    string.Empty,
                TrackName:
                    "Track",
                Destination:
                    "Centro",
                Line:
                    "10",
                TrainReverse:
                    false,
                Stations:
                [
                    new OmsiTimetableTripStationType2(
                        100)
                ],
                ProfileLines:
                    []);

        var timetable =
            new OmsiTimetableCatalog(
                Tracks:
                    [],
                Trips:
                [
                    trip
                ])
            {
                BusStops =
                [
                    stop
                ]
            };

        var assets =
            new ProtonBusOmsiAssetResolutionResult(
                SplineDefinitions:
                    new Dictionary<
                        string,
                        MapStudio.Core.Omsi.Splines.OmsiSplineDefinition>(),
                SceneryAssets:
                    new Dictionary<
                        string,
                        ProtonBusResolvedSceneryAsset>
                    {
                        [
                            placedStop
                                .SceneryObjectPath
                        ] =
                            new(
                                Meshes:
                                    Array.Empty<
                                        ProtonBusResolvedSceneryMesh>(),
                                UsesAbsoluteHeight:
                                    false)
                    },
                Textures:
                    [],
                Issues:
                    []);

        var result =
            ProtonBusOmsiTimetableConverter
                .Convert(
                    [
                        tile
                    ],
                    timetable,
                    new Dictionary<
                        (int X, int Y),
                        OmsiTileContent>
                    {
                        [
                            (
                                0,
                                0
                            )
                        ] =
                            content
                    },
                    new Dictionary<
                        (int X, int Y),
                        ProtonBusOmsiAssetResolutionResult>
                    {
                        [
                            (
                                0,
                                0
                            )
                        ] =
                            assets
                    });

        var busStop =
            Assert.Single(
                result.BusStops);

        Assert.Equal(
            "bs_100",
            busStop.Prefix);

        Assert.Equal(
            "Sao Jose",
            busStop.FriendlyName);

        Assert.Equal(
            0,
            busStop.PassengerAmount);

        Assert.Equal(
            90,
            busStop.DefaultPassengerRotationY,
            precision:
                4);

        var entrypoint =
            Assert.Single(
                result.Entrypoints);

        Assert.Equal(
            "10 Centro",
            entrypoint.Name);

        AssertVectorClose(
            new(
                10,
                7,
                20),
            entrypoint.Position);

        AssertVectorClose(
            new(
                0,
                90,
                0),
            entrypoint.RotationDegrees);

        var marker =
            Assert.Single(
                result
                    .MarkerScene
                    .Meshes);

        Assert.Equal(
            "bs_100_trigger",
            marker.Name);

        AssertVectorClose(
            new(
                10,
                7,
                20),
            MarkerCenter(
                marker));

        Assert.Contains(
            result.Issues,
            issue =>
                issue.Code ==
                "busStopNameSanitized");
    }

    [Fact]
    public void ConverterHonorsAbsoluteHeightSceneryMetadata()
    {
        var tile =
            new OmsiTileReference(
                0,
                0,
                "tile.map");

        var placedStop =
            CreatePlacedStop(
                id:
                    101,
                z:
                    3);

        var content =
            new OmsiTileContent(
                new(
                    true,
                    ObjectCount:
                        1,
                    SplineCount:
                        0,
                    SplineAttachmentCount:
                        0),
                [
                    placedStop
                ],
                [],
                Terrain:
                    new(
                        1,
                        [
                            20,
                            20,
                            20,
                            20
                        ]));

        var timetable =
            CreateCatalogWithStop(
                id:
                    101,
                tileIndex:
                    0,
                name:
                    "Absolute");

        var assets =
            new ProtonBusOmsiAssetResolutionResult(
                new Dictionary<
                    string,
                    MapStudio.Core.Omsi.Splines.OmsiSplineDefinition>(),
                new Dictionary<
                    string,
                    ProtonBusResolvedSceneryAsset>
                {
                    [
                        placedStop
                            .SceneryObjectPath
                    ] =
                        new(
                            Array.Empty<
                                ProtonBusResolvedSceneryMesh>(),
                            UsesAbsoluteHeight:
                                true)
                },
                [],
                []);

        var result =
            ProtonBusOmsiTimetableConverter
                .Convert(
                    [
                        tile
                    ],
                    timetable,
                    new Dictionary<
                        (int X, int Y),
                        OmsiTileContent>
                    {
                        [
                            (
                                0,
                                0
                            )
                        ] =
                            content
                    },
                    new Dictionary<
                        (int X, int Y),
                        ProtonBusOmsiAssetResolutionResult>
                    {
                        [
                            (
                                0,
                                0
                            )
                        ] =
                            assets
                    });

        AssertVectorClose(
            new(
                0,
                3,
                0),
            MarkerCenter(
                Assert.Single(
                    result
                        .MarkerScene
                        .Meshes)));
    }

    [Fact]
    public void ConverterReportsInvalidTileIndexAndMissingObject()
    {
        var tile =
            new OmsiTileReference(
                0,
                0,
                "tile.map");

        var timetable =
            new OmsiTimetableCatalog(
                [],
                [])
            {
                BusStops =
                [
                    new(
                        "Bad Tile",
                        5,
                        1,
                        null,
                        string.Empty,
                        string.Empty,
                        string.Empty),
                    new(
                        "Missing Object",
                        0,
                        999,
                        null,
                        string.Empty,
                        string.Empty,
                        string.Empty)
                ]
            };

        var result =
            ProtonBusOmsiTimetableConverter
                .Convert(
                    [
                        tile
                    ],
                    timetable,
                    new Dictionary<
                        (int X, int Y),
                        OmsiTileContent>
                    {
                        [
                            (
                                0,
                                0
                            )
                        ] =
                            new(
                                new(
                                    true,
                                    0,
                                    0,
                                    0),
                                [],
                                [])
                    });

        Assert.Empty(
            result.BusStops);

        Assert.Contains(
            result.Issues,
            issue =>
                issue.Code ==
                "busStopTileIndexOutOfRange");

        Assert.Contains(
            result.Issues,
            issue =>
                issue.Code ==
                "busStopObjectMissing");
    }

    private static OmsiPlacedObject CreatePlacedStop(
        int id,
        double z) =>
        new(
            HeaderValue:
                "object",
            SceneryObjectPath:
                @"Sceneryobjects\Test\stop.sco",
            ObjectId:
                id,
            X:
                0,
            Y:
                0,
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

    private static OmsiTimetableCatalog CreateCatalogWithStop(
        int id,
        int tileIndex,
        string name) =>
        new(
            [],
            [])
        {
            BusStops =
            [
                new(
                    name,
                    tileIndex,
                    id,
                    null,
                    string.Empty,
                    string.Empty,
                    string.Empty)
            ]
        };

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
