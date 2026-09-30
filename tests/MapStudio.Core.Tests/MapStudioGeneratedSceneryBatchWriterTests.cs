using System.Text;
using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Omsi.Maps;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioGeneratedSceneryBatchWriterTests
{
    [Fact]
    public async Task WriteStoresGroundPlaneAndHeightInOmsiAxisOrder()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-generated-scenery-" +
                Guid.NewGuid().ToString("N"));

        try
        {
            var mapDirectory =
                Path.Combine(
                    root,
                    "maps",
                    "GeneratedScenery");

            Directory.CreateDirectory(
                mapDirectory);

            await File.WriteAllTextAsync(
                Path.Combine(
                    mapDirectory,
                    "global.cfg"),
                "[name]\r\nGenerated Scenery\r\n" +
                "[map]\r\n0\r\n0\r\ntile_0_0.map\r\n",
                Encoding.UTF8);

            var tilePath =
                Path.Combine(
                    mapDirectory,
                    "tile_0_0.map");

            await File.WriteAllTextAsync(
                tilePath,
                "[version]\r\n14\r\n",
                Encoding.UTF8);

            var assetDirectory =
                Path.Combine(
                    root,
                    "Sceneryobjects",
                    "Test");

            Directory.CreateDirectory(
                assetDirectory);

            var assetPath =
                Path.Combine(
                    assetDirectory,
                    "test.sco");

            await File.WriteAllTextAsync(
                assetPath,
                "[friendlyname]\r\nTest\r\n",
                Encoding.ASCII);

            await new MapStudioGeneratedSceneryBatchWriter()
                .WriteAsync(
                    root,
                    mapDirectory,
                    [
                        new MapStudioGeneratedSceneryPlacementRequest(
                            "test",
                            assetPath,
                            new MapStudioRoadPoint(
                                45,
                                120),
                            HeightMeters:
                                7.5)
                    ]);

            var content =
                await new OmsiTileReader()
                    .ReadContentAsync(
                        tilePath);

            var placed =
                Assert.Single(
                    content.Objects);

            Assert.Equal(
                45,
                placed.X,
                6);

            Assert.Equal(
                120,
                placed.Y,
                6);

            Assert.Equal(
                7.5,
                placed.Z,
                6);
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
    public async Task WriteKeepsMultiTilePlacementsInsidePhysicalMapBounds()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-generated-scenery-multitile-" +
                Guid.NewGuid().ToString("N"));

        try
        {
            var mapDirectory =
                Path.Combine(
                    root,
                    "maps",
                    "GeneratedSceneryMultiTile");

            Directory.CreateDirectory(
                mapDirectory);

            await File.WriteAllTextAsync(
                Path.Combine(
                    mapDirectory,
                    "global.cfg"),
                "[name]\r\nGenerated Scenery Multi Tile\r\n" +
                "[map]\r\n-1\r\n0\r\ntile_-1_0.map\r\n" +
                "[map]\r\n0\r\n0\r\ntile_0_0.map\r\n" +
                "[map]\r\n1\r\n0\r\ntile_1_0.map\r\n",
                Encoding.UTF8);

            foreach (
                var tileName in
                    new[]
                    {
                        "tile_-1_0.map",
                        "tile_0_0.map",
                        "tile_1_0.map"
                    })
            {
                await File.WriteAllTextAsync(
                    Path.Combine(
                        mapDirectory,
                        tileName),
                    "[version]\r\n14\r\n",
                    Encoding.UTF8);
            }

            var assetDirectory =
                Path.Combine(
                    root,
                    "Sceneryobjects",
                    "Test");

            Directory.CreateDirectory(
                assetDirectory);

            var assetPath =
                Path.Combine(
                    assetDirectory,
                    "test.sco");

            await File.WriteAllTextAsync(
                assetPath,
                "[friendlyname]\r\nTest\r\n",
                Encoding.ASCII);

            var expectedWorldCenters =
                new Dictionary<
                    string,
                    MapStudioRoadPoint>(
                    StringComparer.Ordinal)
                {
                    ["west"] =
                        new(
                            -299.75,
                            12.0),
                    ["center"] =
                        new(
                            150.0,
                            150.0),
                    ["east"] =
                        new(
                            599.75,
                            288.0)
                };

            var requests =
                expectedWorldCenters
                    .Select(
                        item =>
                            new MapStudioGeneratedSceneryPlacementRequest(
                                item.Key,
                                assetPath,
                                item.Value))
                    .Append(
                        new MapStudioGeneratedSceneryPlacementRequest(
                            "outside",
                            assetPath,
                            new MapStudioRoadPoint(
                                601.0,
                                150.0)))
                    .ToArray();

            var result =
                await new MapStudioGeneratedSceneryBatchWriter()
                    .WriteAsync(
                        root,
                        mapDirectory,
                        requests);

            Assert.Equal(
                3,
                result.Placements.Count);

            Assert.Contains(
                "outside",
                result.OutsideMapIds);

            Assert.All(
                result.Placements,
                placement =>
                {
                    Assert.True(
                        placement.LocalX >=
                            0 &&
                        placement.LocalX <
                            OmsiTileGrid.TileSize);

                    Assert.True(
                        placement.LocalZ >=
                            0 &&
                        placement.LocalZ <
                            OmsiTileGrid.TileSize);
                });

            foreach (var placement in result.Placements)
            {
                var content =
                    await new OmsiTileReader()
                        .ReadContentAsync(
                            placement.TilePath);

                var placed =
                    Assert.Single(
                        content.Objects,
                        item =>
                            item.ObjectId ==
                            placement.ObjectId);

                var worldX =
                    OmsiTileGrid
                        .GetOriginX(
                            placement.TileX) +
                    placed.X;

                var worldZ =
                    OmsiTileGrid
                        .GetOriginZ(
                            placement.TileY) +
                    placed.Y;

                var expected =
                    expectedWorldCenters[
                        placement.Id];

                Assert.Equal(
                    expected.X,
                    worldX,
                    6);

                Assert.Equal(
                    expected.Z,
                    worldZ,
                    6);

                Assert.InRange(
                    worldX,
                    -300.0,
                    599.999999);

                Assert.InRange(
                    worldZ,
                    0.0,
                    299.999999);
            }
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
