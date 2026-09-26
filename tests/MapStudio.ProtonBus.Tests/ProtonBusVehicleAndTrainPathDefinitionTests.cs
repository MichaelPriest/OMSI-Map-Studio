using MapStudio.Core.ProtonBus;
using Xunit;

namespace MapStudio.ProtonBus.Tests;

public sealed class ProtonBusVehicleAndTrainPathDefinitionTests
{
    [Fact]
    public void VehicleWriterSerializesConfirmedTrafficFields()
    {
        var text = ProtonBusVehiclePathDefinitionWriter.Serialize(
            new(
                Prefix: "roadTraffic",
                Reverse: true,
                Loop: false,
                MaxPathsToCheck: 500,
                IsSpawner: false,
                IsBusSpawner: true,
                RightBlinker: true,
                LeftBlinker: false,
                SpawnIntervalSeconds: 12.5,
                ApplySpeedMultiplier: true,
                SpeedMultiplier: 0.75));

        Assert.Contains("[automatic_setup]", text, StringComparison.Ordinal);
        Assert.Contains("reverse=1", text, StringComparison.Ordinal);
        Assert.Contains("[from_3d]", text, StringComparison.Ordinal);
        Assert.Contains("prefix=roadTraffic", text, StringComparison.Ordinal);
        Assert.Contains("maxPathsToCheck=500", text, StringComparison.Ordinal);
        Assert.Contains("[defaults]", text, StringComparison.Ordinal);
        Assert.Contains("isSpawner=0", text, StringComparison.Ordinal);
        Assert.Contains("isBusSpawner=1", text, StringComparison.Ordinal);
        Assert.Contains("rightBlinker=1", text, StringComparison.Ordinal);
        Assert.Contains("leftBlinker=0", text, StringComparison.Ordinal);
        Assert.Contains("spawnInterval=12.5", text, StringComparison.Ordinal);
        Assert.Contains("applySpeedMultiplier=1", text, StringComparison.Ordinal);
        Assert.Contains("speedMultiplier=0.75", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainWriterUsesSafeRecommendedDefaults()
    {
        var definition = new ProtonBusTrainPathDefinition("metroLine");
        var text = ProtonBusTrainPathDefinitionWriter.Serialize(definition);

        Assert.Contains("isSpawner=0", text, StringComparison.Ordinal);
        Assert.Contains("randomTimeToWaitAtStart=1", text, StringComparison.Ordinal);
        Assert.Contains("spawnTimeToWaitAtStart=20", text, StringComparison.Ordinal);
        Assert.Contains("spawnTimeInterval=120", text, StringComparison.Ordinal);
        Assert.Contains("trainType=0", text, StringComparison.Ordinal);
        Assert.Equal("metroLine.000", definition.GetWaypointObjectName(0));
        Assert.Equal("metroLine.txt", definition.SuggestedFileName);
    }

    [Fact]
    public void VehicleWriterRejectsInvalidSpeedMultiplier()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ProtonBusVehiclePathDefinitionWriter.Serialize(
                new("road", SpeedMultiplier: 0)));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => ProtonBusVehiclePathDefinitionWriter.Serialize(
                new("road", SpeedMultiplier: 2.1)));
    }

    [Fact]
    public void TrainWriterRejectsInvalidTiming()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ProtonBusTrainPathDefinitionWriter.Serialize(
                new("train", SpawnTimeIntervalSeconds: 0)));
    }

    [Fact]
    public void PackageWriterPlacesVehicleAndTrainPathsInCorrectFolders()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "MapStudioProtonBusMovingPaths",
            Guid.NewGuid().ToString("N"));

        try
        {
            var result = ProtonBusMapPackageWriter.Write(
                root,
                new(
                    new("Mapa", "Mapa", "Rota"),
                    [])
                {
                    VehiclePaths =
                    [
                        new("roadTraffic")
                    ],
                    TrainPaths =
                    [
                        new("metroLine")
                    ]
                });

            var vehicle = Assert.Single(result.VehiclePathPaths);
            var train = Assert.Single(result.TrainPathPaths);

            Assert.EndsWith(
                Path.Combine("maps", "Mapa", "tiles", "Rota", "aivehicles", "roadTraffic.txt"),
                vehicle,
                StringComparison.OrdinalIgnoreCase);

            Assert.EndsWith(
                Path.Combine("maps", "Mapa", "tiles", "Rota", "aitrains", "metroLine.txt"),
                train,
                StringComparison.OrdinalIgnoreCase);

            Assert.True(File.Exists(vehicle));
            Assert.True(File.Exists(train));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void PackageWriterRejectsMovingPathPrefixCollisionAcrossCategories()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "MapStudioProtonBusMovingPaths",
            Guid.NewGuid().ToString("N"));

        try
        {
            Assert.Throws<ArgumentException>(
                () => ProtonBusMapPackageWriter.Write(
                    root,
                    new(
                        new("Mapa", "Mapa", "Rota"),
                        [])
                    {
                        PedestrianPaths =
                        [
                            new("sharedPath")
                        ],
                        VehiclePaths =
                        [
                            new("SHAREDPATH")
                        ]
                    }));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
