using System.Text;
using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Structures;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioBuildingBatchReconstructionRunnerTests
{
    [Fact]
    public async Task RunAsyncGeneratesStrongBuildingAndKeepsWeakBuildingForReview()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-building-batch-" +
                Guid.NewGuid().ToString("N"));

        try
        {
            var mapDirectory =
                Path.Combine(
                    root,
                    "maps",
                    "BuildingBatchTest");

            Directory.CreateDirectory(
                mapDirectory);

            await File.WriteAllTextAsync(
                Path.Combine(
                    mapDirectory,
                    "global.cfg"),
                "[name]\r\nBuilding Batch Test\r\n" +
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
                    5,
                    @"Sceneryobjects\Pack\ExistingA.sco"),
                Encoding.UTF8);

            await File.WriteAllTextAsync(
                secondTile,
                BuildTile(
                    50,
                    @"Sceneryobjects\Pack\ExistingB.sco"),
                Encoding.UTF8);

            const string xml =
                """
                <osm version="0.6">
                  <node id="1" lat="-23.55000" lon="-46.63000"/>
                  <node id="2" lat="-23.55000" lon="-46.62990"/>
                  <node id="3" lat="-23.55008" lon="-46.62990"/>
                  <node id="4" lat="-23.55008" lon="-46.63000"/>

                  <node id="5" lat="-23.55010" lon="-46.63000"/>
                  <node id="6" lat="-23.55010" lon="-46.62994"/>
                  <node id="7" lat="-23.55016" lon="-46.62994"/>
                  <node id="8" lat="-23.55016" lon="-46.63000"/>

                  <way id="100">
                    <nd ref="1"/><nd ref="2"/><nd ref="3"/><nd ref="4"/><nd ref="1"/>
                    <tag k="building" v="apartments"/>
                    <tag k="building:levels" v="4"/>
                    <tag k="height" v="14"/>
                    <tag k="roof:shape" v="hipped"/>
                    <tag k="roof:height" v="2"/>
                    <tag k="name" v="Edificio Forte"/>
                    <tag k="addr:street" v="Rua Teste"/>
                    <tag k="addr:housenumber" v="100"/>
                  </way>

                  <way id="200">
                    <nd ref="5"/><nd ref="6"/><nd ref="7"/><nd ref="8"/><nd ref="5"/>
                    <tag k="building" v="yes"/>
                  </way>
                </osm>
                """;

            var originalSecondTile =
                await File.ReadAllTextAsync(
                    secondTile);

            var result =
                await new MapStudioBuildingBatchReconstructionRunner()
                    .RunAsync(
                        root,
                        mapDirectory,
                        xml,
                        new MapStudioGeographicAnchor(
                            -23.55000,
                            -46.63000,
                            50,
                            50));

            var placement =
                Assert.Single(
                    result.Placements);

            Assert.Equal(
                "osm-building-100",
                placement.Id);

            Assert.Equal(
                51,
                placement.ObjectId);

            Assert.Equal(
                0,
                placement.TileX);

            Assert.Equal(
                0,
                placement.TileY);

            Assert.Contains(
                "osm-building-200",
                result.ReviewBuildingIds);

            Assert.Empty(
                result.OutsideMapBuildingIds);

            Assert.Empty(
                result.RejectedGeometryBuildingIds);

            Assert.Equal(
                1,
                result.GeneratedBuildingCount);

            Assert.Equal(
                1,
                result.ModifiedTileCount);

            Assert.Single(
                result.BackupPaths);

            var updated =
                await OmsiConfigParser
                    .ParseFileAsync(
                        firstTile);

            Assert.Equal(
                2,
                updated
                    .FindSections(
                        "object")
                    .Count());

            Assert.Equal(
                originalSecondTile,
                await File.ReadAllTextAsync(
                    secondTile));

            var fullSceneryPath =
                Path.Combine(
                    root,
                    placement
                        .SceneryObjectPath
                        .Replace(
                            '\\',
                            Path.DirectorySeparatorChar));

            Assert.True(
                File.Exists(
                    fullSceneryPath));

            Assert.True(
                File.Exists(
                    Assert.Single(
                        result.BackupPaths)));
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
    public async Task GenericWriterBatchesMultipleGeneratedObjectsPerTile()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-generic-writer-" +
                Guid.NewGuid().ToString("N"));

        try
        {
            var mapDirectory =
                Path.Combine(
                    root,
                    "maps",
                    "WriterTest");

            Directory.CreateDirectory(
                mapDirectory);

            await File.WriteAllTextAsync(
                Path.Combine(
                    mapDirectory,
                    "global.cfg"),
                "[name]\r\nWriter Test\r\n" +
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
                    "WriterTest");

            Directory.CreateDirectory(
                assetDirectory);

            var firstAsset =
                Path.Combine(
                    assetDirectory,
                    "A.sco");

            var secondAsset =
                Path.Combine(
                    assetDirectory,
                    "B.sco");

            await File.WriteAllTextAsync(
                firstAsset,
                "[friendlyname]\r\nA\r\n",
                Encoding.ASCII);

            await File.WriteAllTextAsync(
                secondAsset,
                "[friendlyname]\r\nB\r\n",
                Encoding.ASCII);

            var result =
                await new MapStudioGeneratedSceneryBatchWriter()
                    .WriteAsync(
                        root,
                        mapDirectory,
                        [
                            new(
                                "a",
                                firstAsset,
                                new MapStudioRoadPoint(
                                    10,
                                    20)),
                            new(
                                "b",
                                secondAsset,
                                new MapStudioRoadPoint(
                                    30,
                                    40))
                        ]);

            Assert.Equal(
                2,
                result.Placements.Count);

            Assert.Equal(
                1,
                result.ModifiedTileCount);

            Assert.Single(
                result.BackupPaths);

            var updated =
                await OmsiConfigParser
                    .ParseFileAsync(
                        tilePath);

            Assert.Equal(
                2,
                updated
                    .FindSections(
                        "object")
                    .Count());
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
