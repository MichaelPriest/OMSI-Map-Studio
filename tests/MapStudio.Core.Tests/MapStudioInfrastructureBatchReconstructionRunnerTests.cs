using System.Text;
using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Structures;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioInfrastructureBatchReconstructionRunnerTests
{
    [Fact]
    public async Task RunAsyncProcessesAutoGenerateFeaturesInOneTileWrite()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-infra-batch-" +
                Guid.NewGuid().ToString("N"));

        try
        {
            var mapDirectory =
                Path.Combine(
                    root,
                    "maps",
                    "BatchTest");

            Directory.CreateDirectory(
                mapDirectory);

            await File.WriteAllTextAsync(
                Path.Combine(
                    mapDirectory,
                    "global.cfg"),
                "[name]\r\nBatch Test\r\n" +
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
                    10,
                    @"Sceneryobjects\Pack\ExistingA.sco"),
                Encoding.UTF8);

            await File.WriteAllTextAsync(
                secondTile,
                BuildTile(
                    80,
                    @"Sceneryobjects\Pack\ExistingB.sco"),
                Encoding.UTF8);

            const string xml =
                """
                <osm version="0.6">
                  <node id="1" lat="-23.55000" lon="-46.63000"/>
                  <node id="2" lat="-23.55000" lon="-46.62990"/>
                  <node id="3" lat="-23.55002" lon="-46.62998"/>
                  <node id="4" lat="-23.55002" lon="-46.62990"/>
                  <node id="5" lat="-23.55008" lon="-46.62990"/>
                  <node id="6" lat="-23.55008" lon="-46.62998"/>
                  <node id="7" lat="-23.55010" lon="-46.63000"/>
                  <node id="8" lat="-23.55010" lon="-46.62990"/>

                  <way id="10">
                    <nd ref="1"/><nd ref="2"/>
                    <tag k="highway" v="footway"/>
                    <tag k="footway" v="sidewalk"/>
                    <tag k="surface" v="concrete"/>
                    <tag k="width" v="2.0"/>
                    <tag k="name" v="Main sidewalk"/>
                  </way>

                  <way id="20">
                    <nd ref="3"/><nd ref="4"/><nd ref="5"/><nd ref="6"/><nd ref="3"/>
                    <tag k="amenity" v="parking"/>
                    <tag k="surface" v="asphalt"/>
                    <tag k="width" v="12"/>
                    <tag k="name" v="Terminal parking"/>
                  </way>

                  <way id="30">
                    <nd ref="7"/><nd ref="8"/>
                    <tag k="barrier" v="fence"/>
                  </way>
                </osm>
                """;

            var originalSecondTile =
                await File.ReadAllTextAsync(
                    secondTile);

            var result =
                await new MapStudioInfrastructureBatchReconstructionRunner()
                    .RunAsync(
                        root,
                        mapDirectory,
                        xml,
                        new MapStudioGeographicAnchor(
                            -23.55000,
                            -46.63000,
                            50,
                            50));

            Assert.Equal(
                2,
                result.Placements.Count);

            Assert.Equal(
                2,
                result.GeneratedAssetCount);

            Assert.Equal(
                1,
                result.ModifiedTileCount);

            Assert.Single(
                result.BackupPaths);

            Assert.Contains(
                "osm-infrastructure-30",
                result.ReviewFeatureIds);

            Assert.Empty(
                result.OutsideMapFeatureIds);

            Assert.Empty(
                result.RejectedGeometryFeatureIds);

            Assert.Equal(
                new[]
                {
                    81,
                    82
                },
                result.Placements
                    .Select(
                        placement =>
                            placement.ObjectId)
                    .ToArray());

            Assert.All(
                result.Placements,
                placement =>
                    Assert.Equal(
                        0,
                        placement.TileX));

            Assert.All(
                result.Placements,
                placement =>
                    Assert.Equal(
                        0,
                        placement.TileY));

            var updated =
                await OmsiConfigParser
                    .ParseFileAsync(
                        firstTile);

            Assert.Equal(
                3,
                updated
                    .FindSections(
                        "object")
                    .Count());

            Assert.Equal(
                originalSecondTile,
                await File.ReadAllTextAsync(
                    secondTile));

            foreach (var placement in result.Placements)
            {
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
            }

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
    public async Task RunAsyncSkipsAutoGenerateFeatureOutsideExistingMap()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-infra-batch-outside-" +
                Guid.NewGuid().ToString("N"));

        try
        {
            var mapDirectory =
                Path.Combine(
                    root,
                    "maps",
                    "BatchTest");

            Directory.CreateDirectory(
                mapDirectory);

            await File.WriteAllTextAsync(
                Path.Combine(
                    mapDirectory,
                    "global.cfg"),
                "[name]\r\nBatch Test\r\n" +
                "[map]\r\n0\r\n0\r\ntile_0_0.map\r\n",
                Encoding.UTF8);

            var tilePath =
                Path.Combine(
                    mapDirectory,
                    "tile_0_0.map");

            const string originalTile =
                "[version]\r\n14\r\n";

            await File.WriteAllTextAsync(
                tilePath,
                originalTile,
                Encoding.UTF8);

            const string xml =
                """
                <osm version="0.6">
                  <node id="1" lat="-23.55000" lon="-46.63000"/>
                  <node id="2" lat="-23.55000" lon="-46.62990"/>
                  <way id="40">
                    <nd ref="1"/><nd ref="2"/>
                    <tag k="highway" v="footway"/>
                    <tag k="footway" v="sidewalk"/>
                    <tag k="surface" v="concrete"/>
                    <tag k="width" v="2"/>
                    <tag k="name" v="Outside sidewalk"/>
                  </way>
                </osm>
                """;

            var result =
                await new MapStudioInfrastructureBatchReconstructionRunner()
                    .RunAsync(
                        root,
                        mapDirectory,
                        xml,
                        new MapStudioGeographicAnchor(
                            -23.55000,
                            -46.63000,
                            650,
                            650));

            Assert.Empty(
                result.Placements);

            Assert.Empty(
                result.BackupPaths);

            Assert.Contains(
                "osm-infrastructure-40",
                result.OutsideMapFeatureIds);

            Assert.Equal(
                originalTile,
                await File.ReadAllTextAsync(
                    tilePath));
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
