using System.Text;
using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Omsi.Structures;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioInfrastructureTilePlacerTests
{
    [Fact]
    public async Task PlaceAsyncUsesCorrectTileAndGlobalNextId()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-placement-" +
                Guid.NewGuid().ToString("N"));

        try
        {
            var mapDirectory =
                Path.Combine(
                    root,
                    "maps",
                    "PlacementTest");

            Directory.CreateDirectory(
                mapDirectory);

            await File.WriteAllTextAsync(
                Path.Combine(
                    mapDirectory,
                    "global.cfg"),
                "[name]\r\nPlacement Test\r\n" +
                "[map]\r\n0\r\n0\r\ntile_0_0.map\r\n" +
                "[map]\r\n1\r\n0\r\ntile_1_0.map\r\n",
                Encoding.UTF8);

            var firstTile =
                Path.Combine(
                    mapDirectory,
                    "tile_0_0.map");

            var secondTile =
                Path.Combine(
                    mapDirectory,
                    "tile_1_0.map");

            await File.WriteAllTextAsync(
                firstTile,
                BuildTile(
                    9,
                    @"Sceneryobjects\Pack\ExistingA.sco"),
                Encoding.UTF8);

            await File.WriteAllTextAsync(
                secondTile,
                BuildTile(
                    40,
                    @"Sceneryobjects\Pack\ExistingB.sco"),
                Encoding.UTF8);

            var objectDirectory =
                Path.Combine(
                    root,
                    "Sceneryobjects",
                    MapStudioInfrastructureAssetGenerator
                        .RootFolderName,
                    "Wall_wall-1");

            Directory.CreateDirectory(
                objectDirectory);

            var sceneryPath =
                Path.Combine(
                    objectDirectory,
                    "Wall_wall-1.sco");

            await File.WriteAllTextAsync(
                sceneryPath,
                "[friendlyname]\r\nWall\r\n",
                Encoding.ASCII);

            var asset =
                new MapStudioInfrastructureAssetResult(
                    objectDirectory,
                    sceneryPath,
                    Path.Combine(
                        objectDirectory,
                        "model",
                        "infrastructure.o3d"),
                    new MapStudioRoadPoint(
                        150,
                        120),
                    null);

            var result =
                await new MapStudioInfrastructureTilePlacer()
                    .PlaceAsync(
                        root,
                        mapDirectory,
                        asset,
                        heightMeters: 2.5);

            Assert.Equal(
                0,
                result.TileX);

            Assert.Equal(
                0,
                result.TileY);

            Assert.Equal(
                41,
                result.ObjectId);

            Assert.Equal(
                150,
                result.LocalX);

            Assert.Equal(
                120,
                result.LocalZ);

            Assert.True(
                File.Exists(
                    result.BackupPath));

            var updated =
                await File.ReadAllTextAsync(
                    firstTile);

            Assert.Contains(
                "Sceneryobjects\\MapStudio_Infrastructure\\Wall_wall-1\\Wall_wall-1.sco",
                updated);

            Assert.Contains(
                "\r\n41\r\n150\r\n2.5\r\n120\r\n",
                updated);

            var untouched =
                await File.ReadAllTextAsync(
                    secondTile);

            Assert.DoesNotContain(
                "\r\n41\r\n",
                untouched);
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
    public async Task PlaceAsyncRejectsMissingTargetTile()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-placement-missing-" +
                Guid.NewGuid().ToString("N"));

        try
        {
            var mapDirectory =
                Path.Combine(
                    root,
                    "maps",
                    "PlacementTest");

            Directory.CreateDirectory(
                mapDirectory);

            await File.WriteAllTextAsync(
                Path.Combine(
                    mapDirectory,
                    "global.cfg"),
                "[name]\r\nPlacement Test\r\n" +
                "[map]\r\n0\r\n0\r\ntile_0_0.map\r\n",
                Encoding.UTF8);

            await File.WriteAllTextAsync(
                Path.Combine(
                    mapDirectory,
                    "tile_0_0.map"),
                "[version]\r\n14\r\n",
                Encoding.UTF8);

            var objectDirectory =
                Path.Combine(
                    root,
                    "Sceneryobjects",
                    MapStudioInfrastructureAssetGenerator
                        .RootFolderName,
                    "Wall_wall-1");

            Directory.CreateDirectory(
                objectDirectory);

            var sceneryPath =
                Path.Combine(
                    objectDirectory,
                    "Wall_wall-1.sco");

            await File.WriteAllTextAsync(
                sceneryPath,
                "[friendlyname]\r\nWall\r\n",
                Encoding.ASCII);

            var asset =
                new MapStudioInfrastructureAssetResult(
                    objectDirectory,
                    sceneryPath,
                    Path.Combine(
                        objectDirectory,
                        "model",
                        "infrastructure.o3d"),
                    new MapStudioRoadPoint(
                        650,
                        120),
                    null);

            var exception =
                await Assert.ThrowsAsync<InvalidDataException>(
                    () =>
                        new MapStudioInfrastructureTilePlacer()
                            .PlaceAsync(
                                root,
                                mapDirectory,
                                asset));

            Assert.Equal(
                "infrastructureTargetTileMissing",
                exception.Message);
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

    private static string BuildTile(
        int objectId,
        string sceneryPath) =>
        "[version]\r\n14\r\n" +
        "[object]\r\n0\r\n" +
        sceneryPath +
        "\r\n" +
        objectId +
        "\r\n10\r\n0\r\n20\r\n0\r\n0\r\n0\r\n";
}
