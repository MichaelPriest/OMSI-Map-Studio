using MapStudio.Core.Omsi.Structures;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioGeneratedAssetTransactionTests
{
    [Fact]
    public async Task RestoreReinstatesPreexistingAssetsAndRemovesNewRoots()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-asset-transaction-" +
                Guid.NewGuid().ToString("N"));

        try
        {
            var roadKitRoot =
                Path.Combine(
                    root,
                    "Splines",
                    "MapStudio_RoadKit");

            var junctionRoot =
                Path.Combine(
                    root,
                    "Sceneryobjects",
                    "MapStudio_Junctions");

            Directory.CreateDirectory(roadKitRoot);

            var originalRoadAsset =
                Path.Combine(
                    roadKitRoot,
                    "existing.sli");

            await File.WriteAllTextAsync(
                originalRoadAsset,
                "before");

            var transaction =
                new MapStudioGeneratedAssetTransaction();

            var snapshot =
                await transaction.CaptureAsync(root);

            await File.WriteAllTextAsync(
                originalRoadAsset,
                "after");

            await File.WriteAllTextAsync(
                Path.Combine(
                    roadKitRoot,
                    "new.sli"),
                "generated");

            Directory.CreateDirectory(junctionRoot);

            await File.WriteAllTextAsync(
                Path.Combine(
                    junctionRoot,
                    "junction.sco"),
                "generated");

            transaction.Restore(snapshot);

            Assert.Equal(
                "before",
                await File.ReadAllTextAsync(
                    originalRoadAsset));

            Assert.False(
                File.Exists(
                    Path.Combine(
                        roadKitRoot,
                        "new.sli")));

            Assert.False(
                Directory.Exists(junctionRoot));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }

    [Fact]
    public async Task RestoreRemovesGeneratedBuildingAndInfrastructureRoots()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-asset-transaction-newroots-" +
                Guid.NewGuid().ToString("N"));

        try
        {
            var transaction =
                new MapStudioGeneratedAssetTransaction();

            var snapshot =
                await transaction.CaptureAsync(root);

            var buildingRoot =
                Path.Combine(
                    root,
                    "Sceneryobjects",
                    "MapStudio_Buildings");

            var infrastructureRoot =
                Path.Combine(
                    root,
                    "Sceneryobjects",
                    "MapStudio_Infrastructure");

            Directory.CreateDirectory(buildingRoot);
            Directory.CreateDirectory(infrastructureRoot);

            await File.WriteAllTextAsync(
                Path.Combine(
                    buildingRoot,
                    "generated.sco"),
                "building");

            await File.WriteAllTextAsync(
                Path.Combine(
                    infrastructureRoot,
                    "generated.sco"),
                "infra");

            transaction.Restore(snapshot);

            Assert.False(
                Directory.Exists(buildingRoot));

            Assert.False(
                Directory.Exists(infrastructureRoot));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }
}
