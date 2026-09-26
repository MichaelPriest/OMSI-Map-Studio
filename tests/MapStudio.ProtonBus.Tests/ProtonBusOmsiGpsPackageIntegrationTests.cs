using System.Text;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Timetables;
using MapStudio.Core.ProtonBus;
using Xunit;

namespace MapStudio.ProtonBus.Tests;

public sealed class ProtonBusOmsiGpsPackageIntegrationTests
{
    [Fact]
    public async Task ExporterAddsGpsRouteForTimetableTrip()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudioProtonBusGpsPackageTests",
                Guid.NewGuid()
                    .ToString("N"));

        var output =
            Path.Combine(
                root,
                "export");

        try
        {
            CreateSplineFixture(
                root);

            var sceneryDirectory =
                Path.Combine(
                    root,
                    "Sceneryobjects",
                    "Test");

            Directory.CreateDirectory(
                sceneryDirectory);

            await File.WriteAllTextAsync(
                Path.Combine(
                    sceneryDirectory,
                    "stop.sco"),
                string.Join(
                    Environment.NewLine,
                    [
                        "[friendlyname]",
                        "Stop"
                    ]));

            var tile =
                new OmsiTileReference(
                    0,
                    0,
                    "tile_0_0.map");

            var spline =
                new OmsiPlacedSpline(
                    HeaderValue:
                        "spline",
                    SplinePath:
                        @"Splines\Test\road.sli",
                    SplineId:
                        10,
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
                        20,
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

            var stopObject =
                new OmsiPlacedObject(
                    HeaderValue:
                        "object",
                    SceneryObjectPath:
                        @"Sceneryobjects\Test\stop.sco",
                    ObjectId:
                        100,
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

            var content =
                new OmsiTileContent(
                    new(
                        true,
                        ObjectCount:
                            1,
                        SplineCount:
                            1,
                        SplineAttachmentCount:
                            0),
                    [
                        stopObject
                    ],
                    [
                        spline
                    ]);

            var timetable =
                new OmsiTimetableCatalog(
                    [
                        new(
                            FilePath:
                                "TrackA.ttr",
                            RelativePath:
                                "TTData/TrackA.ttr",
                            Name:
                                "TrackA",
                            Comment1:
                                string.Empty,
                            Comment2:
                                string.Empty,
                            Entries:
                            [
                                new(
                                    Comment:
                                        string.Empty,
                                    Id:
                                        10,
                                    Line2:
                                        "0",
                                    TileIndex:
                                        0,
                                    Line4:
                                        string.Empty,
                                    Length:
                                        20,
                                    Line6:
                                        string.Empty,
                                    Line7:
                                        null)
                            ])
                    ],
                    [
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
                                "TrackA",
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
                                [])
                    ])
                {
                    BusStops =
                    [
                        new(
                            "Inicial",
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
                                string.Empty)
                    ]
                };

            var result =
                await new ProtonBusOmsiMapPackageExporter()
                    .ExportAsync(
                        root,
                        output,
                        new(
                            "Mapa",
                            "Mapa",
                            "Rota"),
                        [
                            new(
                                tile,
                                content)
                        ],
                        new()
                        {
                            Timetable =
                                new(
                                    [
                                        tile
                                    ],
                                    timetable),
                            GpsOptions =
                                new(
                                    MaximumSampleLength:
                                        5,
                                    MaximumPartLength:
                                        1000)
                        });

            Assert.True(
                result.IsExported);

            Assert.NotNull(
                result.GpsRoutes);

            var gpsRoute =
                Assert.Single(
                    result.GpsRoutes!
                        .Routes);

            Assert.True(
                gpsRoute.IsComplete);

            Assert.Equal(
                "10 Centro",
                gpsRoute.EntrypointName);

            var gpsModel =
                Assert.Single(
                    result.Package!
                        .ModelPaths,
                    path =>
                        path.EndsWith(
                            "gps_routes.3ds",
                            StringComparison
                                .OrdinalIgnoreCase));

            var bytes =
                File.ReadAllBytes(
                    gpsModel);

            Assert.True(
                bytes
                    .AsSpan()
                    .IndexOf(
                        Encoding.ASCII
                            .GetBytes(
                                "_gps_10 Centro_\0")) >=
                0);

            Assert.Contains(
                result.Package
                    .DestinationDirectories,
                path =>
                    path.EndsWith(
                        Path.Combine(
                            "dest",
                            "10 Centro"),
                        StringComparison
                            .OrdinalIgnoreCase));
        }
        finally
        {
            if (
                Directory.Exists(
                    root))
            {
                Directory.Delete(
                    root,
                    recursive:
                        true);
            }
        }
    }

    private static void CreateSplineFixture(
        string root)
    {
        var splineDirectory =
            Path.Combine(
                root,
                "Splines",
                "Test");

        Directory.CreateDirectory(
            Path.Combine(
                splineDirectory,
                "Texture"));

        File.WriteAllText(
            Path.Combine(
                splineDirectory,
                "road.sli"),
            string.Join(
                Environment.NewLine,
                [
                    "[texture]",
                    "road.png",
                    "[profile]",
                    "0",
                    "[profilepnt]",
                    "-3",
                    "0",
                    "0",
                    "1",
                    "[profilepnt]",
                    "3",
                    "0",
                    "1",
                    "1",
                    "[path]",
                    "0",
                    "0",
                    "0.1",
                    "3",
                    "0"
                ]));

        File.WriteAllBytes(
            Path.Combine(
                splineDirectory,
                "Texture",
                "road.png"),
            [
                137,
                80,
                78,
                71,
                13,
                10,
                26,
                10
            ]);
    }
}
