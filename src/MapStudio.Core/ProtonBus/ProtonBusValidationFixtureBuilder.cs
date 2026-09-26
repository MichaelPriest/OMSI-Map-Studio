using System.Numerics;

namespace MapStudio.Core.ProtonBus;

public sealed record ProtonBusValidationFixtureResult(
    ProtonBusMapPackageResult Package,
    string? ArchivePath,
    ProtonBusMapDefinition Definition);

public static class ProtonBusValidationFixtureBuilder
{
    public const string MapName =
        "MapStudioValidation";

    public const string BaseDirectory =
        "MapStudioValidation";

    public const string ModelsDirectory =
        "validation";

    public static ProtonBusValidationFixtureResult Build(
        string outputRoot,
        bool createZipArchive = true,
        string? archiveFileName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            outputRoot);

        var definition =
            new ProtonBusMapDefinition(
                MapName,
                BaseDirectory,
                ModelsDirectory,
                MapModVersion:
                    3);

        var vehicle =
            new ProtonBusVehiclePathDefinition(
                Prefix:
                    "validation_vehicle",
                Reverse:
                    false,
                Loop:
                    false,
                MaxPathsToCheck:
                    3,
                IsSpawner:
                    false);

        var pedestrian =
            new ProtonBusPedestrianPathDefinition(
                Prefix:
                    "validation_people",
                Reverse:
                    false,
                Loop:
                    false,
                MaxPathsToCheck:
                    3,
                IsSpawner:
                    false,
                AllowBicycle:
                    false);

        var train =
            new ProtonBusTrainPathDefinition(
                Prefix:
                    "validation_train",
                Reverse:
                    false,
                Loop:
                    false,
                MaxPathsToCheck:
                    3,
                IsSpawner:
                    false);

        var busStop =
            new ProtonBusBusStopDefinition(
                Prefix:
                    "validation_stop",
                FriendlyName:
                    "Validation Stop",
                PassengerAmount:
                    0,
                DefaultPassengerRotationY:
                    0,
                IsFirstStop:
                    true,
                IsLatestStop:
                    true,
                Radius:
                    2,
                MaxPathsToCheck:
                    1);

        var entrypoint =
            new ProtonBusEntrypointDefinition(
                Name:
                    "Validation Line",
                Position:
                    new(
                        0,
                        0.1f,
                        0),
                RotationDegrees:
                    Vector3.Zero);

        var traffic =
            new ProtonBusTrafficLightDefinition(
                Prefix:
                    "validation_signal",
                PathCount:
                    1,
                TickInterval:
                    1,
                TriggerRadius:
                    2,
                UseRealLights:
                    true,
                Ticks:
                [
                    new(
                        Repeat:
                            5,
                        Paths:
                            new Dictionary<
                                int,
                                ProtonBusTrafficLightPathState>
                            {
                                [1] =
                                    new(
                                        Red:
                                            true,
                                        Trigger:
                                            true)
                            }),
                    new(
                        Repeat:
                            5,
                        Paths:
                            new Dictionary<
                                int,
                                ProtonBusTrafficLightPathState>
                            {
                                [1] =
                                    new(
                                        Green:
                                            true)
                            }),
                    new(
                        Repeat:
                            2,
                        Paths:
                            new Dictionary<
                                int,
                                ProtonBusTrafficLightPathState>
                            {
                                [1] =
                                    new(
                                        Yellow:
                                            true,
                                        Trigger:
                                            true)
                            })
                ],
                RandomTimestampAtStart:
                    false,
                FirstTickToRun:
                    1,
                GreenLight:
                    new(
                        new(
                            0,
                            1,
                            0,
                            1),
                        Intensity:
                            1,
                        Range:
                            8),
                RedLight:
                    new(
                        new(
                            1,
                            0,
                            0,
                            1),
                        Intensity:
                            1,
                        Range:
                            8),
                YellowLight:
                    new(
                        new(
                            1,
                            0.8f,
                            0,
                            1),
                        Intensity:
                            1,
                        Range:
                            8));

        var streetLight =
            new ProtonBusStreetLightDefinition(
                Prefix:
                    "validation_light",
                AlwaysOn:
                    true,
                Real:
                    new(
                        Color:
                            new(
                                1,
                                0.85f,
                                0.65f),
                        Range:
                            20,
                        Intensity:
                            0.8));

        var scene =
            new ProtonBusExportScene(
                BuildMeshes(
                    vehicle,
                    pedestrian,
                    train,
                    busStop,
                    entrypoint,
                    traffic,
                    streetLight));

        var package =
            ProtonBusMapPackageWriter
                .Write(
                    outputRoot,
                    new(
                        definition,
                        [
                            new(
                                "validation.3ds",
                                scene)
                        ])
                    {
                        VehiclePaths =
                        [
                            vehicle
                        ],
                        PedestrianPaths =
                        [
                            pedestrian
                        ],
                        TrainPaths =
                        [
                            train
                        ],
                        BusStops =
                        [
                            busStop
                        ],
                        Entrypoints =
                        [
                            entrypoint
                        ],
                        TrafficLights =
                        [
                            traffic
                        ],
                        StreetLights =
                        [
                            streetLight
                        ]
                    });

        string? archivePath =
            null;

        if (
            createZipArchive)
        {
            var fileName =
                string.IsNullOrWhiteSpace(
                    archiveFileName)
                    ? "MapStudio-ProtonBus-Validation-Fixture.zip"
                    : archiveFileName!;

            if (
                !fileName.EndsWith(
                    ".zip",
                    StringComparison.OrdinalIgnoreCase))
            {
                fileName +=
                    ".zip";
            }

            archivePath =
                ProtonBusPackageArchiveWriter
                    .Write(
                        package,
                        Path.Combine(
                            outputRoot,
                            fileName));
        }

        return new(
            package,
            archivePath,
            definition);
    }

    private static IReadOnlyList<
        ProtonBusExportMesh>
        BuildMeshes(
            ProtonBusVehiclePathDefinition vehicle,
            ProtonBusPedestrianPathDefinition pedestrian,
            ProtonBusTrainPathDefinition train,
            ProtonBusBusStopDefinition busStop,
            ProtonBusEntrypointDefinition entrypoint,
            ProtonBusTrafficLightDefinition traffic,
            ProtonBusStreetLightDefinition streetLight)
    {
        var meshes =
            new List<
                ProtonBusExportMesh>
            {
                CreateGroundMesh(),
                CreateGpsMesh(
                    new ProtonBusGpsRouteDefinition(
                        entrypoint.Name)
                        .ObjectName)
            };

        AddPathMarkers(
            meshes,
            vehicle.GetWaypointObjectName,
            [
                new(
                    0,
                    0.05f,
                    0),
                new(
                    0,
                    0.05f,
                    10),
                new(
                    0,
                    0.05f,
                    20)
            ]);

        AddPathMarkers(
            meshes,
            pedestrian.GetWaypointObjectName,
            [
                new(
                    3,
                    0.05f,
                    0),
                new(
                    3,
                    0.05f,
                    10),
                new(
                    3,
                    0.05f,
                    20)
            ]);

        AddPathMarkers(
            meshes,
            train.GetWaypointObjectName,
            [
                new(
                    -3,
                    0.05f,
                    0),
                new(
                    -3,
                    0.05f,
                    10),
                new(
                    -3,
                    0.05f,
                    20)
            ]);

        meshes.Add(
            ProtonBusMarkerMeshBuilder
                .Create(
                    busStop
                        .TriggerObjectName,
                    new(
                        2,
                        0.05f,
                        10)));

        meshes.Add(
            ProtonBusMarkerMeshBuilder
                .Create(
                    traffic
                        .GetTriggerObjectName(
                            1),
                    new(
                        0,
                        0.05f,
                        12)));

        meshes.Add(
            ProtonBusMarkerMeshBuilder
                .Create(
                    traffic
                        .GetLightObjectName(
                            1,
                            ProtonBusTrafficLightColor
                                .Red),
                    new(
                        1,
                        3,
                        12)));

        meshes.Add(
            ProtonBusMarkerMeshBuilder
                .Create(
                    traffic
                        .GetLightObjectName(
                            1,
                            ProtonBusTrafficLightColor
                                .Yellow),
                    new(
                        1,
                        3.4f,
                        12)));

        meshes.Add(
            ProtonBusMarkerMeshBuilder
                .Create(
                    traffic
                        .GetLightObjectName(
                            1,
                            ProtonBusTrafficLightColor
                                .Green),
                    new(
                        1,
                        3.8f,
                        12)));

        meshes.Add(
            ProtonBusMarkerMeshBuilder
                .Create(
                    streetLight
                        .RealObjectName,
                    new(
                        4,
                        4,
                        15)));

        return meshes;
    }

    private static void AddPathMarkers(
        ICollection<
            ProtonBusExportMesh> meshes,
        Func<int, string> nameFactory,
        IReadOnlyList<Vector3> positions)
    {
        for (
            var index = 0;
            index <
                positions.Count;
            index++)
        {
            meshes.Add(
                ProtonBusMarkerMeshBuilder
                    .Create(
                        nameFactory(
                            index),
                        positions[index]));
        }
    }

    private static ProtonBusExportMesh
        CreateGroundMesh()
    {
        const string materialName =
            "validation_ground_material";

        return new(
            "validation_ground_gencol_",
            [
                new(
                    new(
                        -8,
                        0,
                        -5),
                    new(
                        0,
                        0)),
                new(
                    new(
                        8,
                        0,
                        -5),
                    new(
                        1,
                        0)),
                new(
                    new(
                        8,
                        0,
                        25),
                    new(
                        1,
                        1)),
                new(
                    new(
                        -8,
                        0,
                        25),
                    new(
                        0,
                        1))
            ],
            [
                new(
                    0,
                    1,
                    2,
                    materialName),
                new(
                    0,
                    2,
                    3,
                    materialName)
            ],
            [
                new(
                    materialName,
                    DiffuseColor:
                        new(
                            0.25f,
                            0.25f,
                            0.25f))
            ]);
    }

    private static ProtonBusExportMesh
        CreateGpsMesh(
            string objectName)
    {
        const string materialName =
            "validation_gps_material";

        return new(
            objectName,
            [
                new(
                    new(
                        -0.35f,
                        0.03f,
                        0),
                    Vector2.Zero),
                new(
                    new(
                        0.35f,
                        0.03f,
                        0),
                    Vector2.UnitX),
                new(
                    new(
                        0.35f,
                        0.03f,
                        20),
                    Vector2.One),
                new(
                    new(
                        -0.35f,
                        0.03f,
                        20),
                    Vector2.UnitY)
            ],
            [
                new(
                    0,
                    1,
                    2,
                    materialName),
                new(
                    0,
                    2,
                    3,
                    materialName)
            ],
            [
                new(
                    materialName,
                    Emissive:
                        true,
                    DiffuseColor:
                        new(
                            1,
                            0.8f,
                            0.1f))
            ]);
    }
}
