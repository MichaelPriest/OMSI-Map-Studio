using System.Text;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Timetables;
using MapStudio.Core.ProtonBus;
using Xunit;

namespace MapStudio.ProtonBus.Tests;

public sealed class ProtonBusOmsiMapPackageExporterTests
{
    [Fact]
    public async Task ExporterWritesTwoTilesAndDeduplicatesSharedTexture()
    {
        var root =
            CreateRoot();

        var output =
            Path.Combine(
                root,
                "export");

        try
        {
            CreateSplineFixture(
                root,
                "Test",
                "road.png",
                ValidPngSignature());

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
                                new(
                                    0,
                                    0,
                                    "tile_0_0.map"),
                                CreateContent(
                                    1,
                                    @"Splines\Test\road.sli")),
                            new(
                                new(
                                    1,
                                    0,
                                    "tile_1_0.map"),
                                CreateContent(
                                    2,
                                    @"Splines\Test\road.sli"))
                        ],
                        new(
                            new()
                            {
                                FunctionalOptions =
                                    new(
                                        WaypointSpacing:
                                            10)
                            }));

            Assert.True(
                result.IsExported);

            Assert.NotNull(
                result.Package);

            Assert.Equal(
                2,
                result.Tiles.Count);

            Assert.Equal(
                2,
                result.Package!
                    .ModelPaths
                    .Count);

            Assert.Single(
                result.Package
                    .TexturePaths);

            Assert.Equal(
                2,
                result.Package
                    .VehiclePathPaths
                    .Count);

            Assert.Contains(
                result.Package
                    .ModelPaths,
                path =>
                    path.EndsWith(
                        "tile_0_0.3ds",
                        StringComparison.OrdinalIgnoreCase));

            Assert.Contains(
                result.Package
                    .ModelPaths,
                path =>
                    path.EndsWith(
                        "tile_1_0.3ds",
                        StringComparison.OrdinalIgnoreCase));

            Assert.Contains(
                result.Package
                    .VehiclePathPaths,
                path =>
                    Path.GetFileName(path)
                        .StartsWith(
                            "pv_t0_0_s1_p0_f",
                            StringComparison.Ordinal));

            Assert.Contains(
                result.Package
                    .VehiclePathPaths,
                path =>
                    Path.GetFileName(path)
                        .StartsWith(
                            "pv_t1_0_s2_p0_f",
                            StringComparison.Ordinal));

            Assert.All(
                result.Tiles,
                tile =>
                {
                    Assert.NotNull(
                        tile.Geometry);

                    Assert.NotNull(
                        tile.Functional);

                    Assert.Single(
                        tile.Functional!
                            .VehiclePaths);
                });

            Assert.DoesNotContain(
                result.Issues,
                issue =>
                    issue.Code ==
                    "textureTargetCollisionAcrossTiles");
        }
        finally
        {
            DeleteRoot(
                root);
        }
    }

    [Fact]
    public async Task ExporterBlocksCrossTileTextureNameCollision()
    {
        var root =
            CreateRoot();

        var output =
            Path.Combine(
                root,
                "export");

        try
        {
            CreateSplineFixture(
                root,
                "PackA",
                "road.png",
                ValidPngSignature());

            CreateSplineFixture(
                root,
                "PackB",
                "road.png",
                ValidPngSignature());

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
                                new(
                                    0,
                                    0,
                                    "tile_0_0.map"),
                                CreateContent(
                                    1,
                                    @"Splines\PackA\road.sli")),
                            new(
                                new(
                                    1,
                                    0,
                                    "tile_1_0.map"),
                                CreateContent(
                                    2,
                                    @"Splines\PackB\road.sli"))
                        ]);

            Assert.False(
                result.IsExported);

            Assert.Null(
                result.Package);

            Assert.Contains(
                result.Issues,
                issue =>
                    issue.Code ==
                    "textureTargetCollisionAcrossTiles");

            Assert.False(
                File.Exists(
                    Path.Combine(
                        output,
                        "maps",
                        "Mapa.map.txt")));
        }
        finally
        {
            DeleteRoot(
                root);
        }
    }

    [Fact]
    public async Task ExporterIntegratesTimetableStopsEntrypointsAndMarkers()
    {
        var root =
            CreateRoot();

        var output =
            Path.Combine(
                root,
                "export");

        try
        {
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
                        "Test Stop"
                    ]));

            var tile =
                new OmsiTileReference(
                    0,
                    0,
                    "tile_0_0.map");

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

            var timetable =
                new OmsiTimetableCatalog(
                    [],
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
                                [])
                    ])
                {
                    BusStops =
                    [
                        new(
                            "Sao Jose",
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
                                    timetable)
                        });

            Assert.True(
                result.IsExported);

            Assert.NotNull(
                result.Package);

            Assert.NotNull(
                result.Timetable);

            var busStop =
                Assert.Single(
                    result.Timetable!
                        .BusStops);

            Assert.Equal(
                "bs_100",
                busStop.Prefix);

            var busStopPath =
                Assert.Single(
                    result.Package!
                        .BusStopPaths);

            Assert.EndsWith(
                Path.Combine(
                    "busstops",
                    "bs_100.txt"),
                busStopPath,
                StringComparison
                    .OrdinalIgnoreCase);

            Assert.True(
                File.Exists(
                    busStopPath));

            Assert.NotNull(
                result.Package
                    .EntrypointsPath);

            Assert.Contains(
                "name=10 Centro",
                File.ReadAllText(
                    result.Package
                        .EntrypointsPath!),
                StringComparison.Ordinal);

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

            var markerModel =
                Assert.Single(
                    result.Package
                        .ModelPaths,
                    path =>
                        path.EndsWith(
                            "timetable_markers.3ds",
                            StringComparison
                                .OrdinalIgnoreCase));

            var markerBytes =
                File.ReadAllBytes(
                    markerModel);

            Assert.True(
                markerBytes
                    .AsSpan()
                    .IndexOf(
                        Encoding.ASCII
                            .GetBytes(
                                "bs_100_trigger\0")) >=
                0);
        }
        finally
        {
            DeleteRoot(
                root);
        }
    }

    [Fact]
    public async Task ExporterRejectsDuplicateTileCoordinates()
    {
        var root =
            CreateRoot();

        try
        {
            var content =
                new OmsiTileContent(
                    new(
                        true,
                        ObjectCount: 0,
                        SplineCount: 0,
                        SplineAttachmentCount: 0),
                    [],
                    []);

            var result =
                await new ProtonBusOmsiMapPackageExporter()
                    .ExportAsync(
                        root,
                        Path.Combine(
                            root,
                            "export"),
                        new(
                            "Mapa",
                            "Mapa",
                            "Rota"),
                        [
                            new(
                                new(
                                    0,
                                    0,
                                    "a.map"),
                                content),
                            new(
                                new(
                                    0,
                                    0,
                                    "b.map"),
                                content)
                        ]);

            Assert.False(
                result.IsExported);

            Assert.Contains(
                result.Issues,
                issue =>
                    issue.Code ==
                    "duplicateTileCoordinates");
        }
        finally
        {
            DeleteRoot(
                root);
        }
    }

    private static OmsiTileContent CreateContent(
        int splineId,
        string splinePath) =>
        new(
            new(
                true,
                ObjectCount: 0,
                SplineCount: 1,
                SplineAttachmentCount: 0),
            [],
            [
                new(
                    HeaderValue:
                        "spline",
                    SplinePath:
                        splinePath,
                    SplineId:
                        splineId,
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
                        10,
                    Radius:
                        0,
                    GradientStart:
                        0,
                    GradientEnd:
                        0,
                    IsHeightSpline:
                        false,
                    ExtraValues:
                        [])
            ]);

    private static void CreateSplineFixture(
        string root,
        string pack,
        string textureName,
        byte[] textureBytes)
    {
        var splinePath =
            Path.Combine(
                root,
                "Splines",
                pack,
                "road.sli");

        Directory.CreateDirectory(
            Path.GetDirectoryName(
                splinePath)!);

        File.WriteAllText(
            splinePath,
            string.Join(
                Environment.NewLine,
                [
                    "[texture]",
                    textureName,
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

        var texturePath =
            Path.Combine(
                root,
                "Splines",
                pack,
                "Texture",
                textureName);

        Directory.CreateDirectory(
            Path.GetDirectoryName(
                texturePath)!);

        File.WriteAllBytes(
            texturePath,
            textureBytes);
    }

    private static byte[] ValidPngSignature() =>
    [
        137,
        80,
        78,
        71,
        13,
        10,
        26,
        10
    ];

    private static string CreateRoot()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudioProtonBusMapExporterTests",
                Guid.NewGuid()
                    .ToString("N"));

        Directory.CreateDirectory(
            root);

        return root;
    }

    private static void DeleteRoot(
        string root)
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
