using System.Text;
using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Indexing;
using MapStudio.Core.Omsi.Structures;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioVegetationBatchReconstructionRunnerTests
{
    [Fact]
    public async Task RunAsyncPlacesStrongTreeWithSpeciesMatchedOmsiAsset()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-vegetation-batch-" +
                Guid.NewGuid().ToString("N"));

        try
        {
            var mapDirectory =
                Path.Combine(
                    root,
                    "maps",
                    "VegetationBatchTest");

            Directory.CreateDirectory(
                mapDirectory);

            await File.WriteAllTextAsync(
                Path.Combine(
                    mapDirectory,
                    "global.cfg"),
                "[name]\r\nVegetation Batch Test\r\n" +
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
                    4,
                    @"Sceneryobjects\Pack\ExistingA.sco"),
                Encoding.UTF8);

            await File.WriteAllTextAsync(
                secondTile,
                BuildTile(
                    70,
                    @"Sceneryobjects\Pack\ExistingB.sco"),
                Encoding.UTF8);

            var vegetationDirectory =
                Path.Combine(
                    root,
                    "Sceneryobjects",
                    "Vegetation",
                    "Tipuana");

            Directory.CreateDirectory(
                vegetationDirectory);

            var speciesAsset =
                Path.Combine(
                    vegetationDirectory,
                    "tipuana_tipu_tree.sco");

            await File.WriteAllTextAsync(
                speciesAsset,
                "[friendlyname]\r\nTipuana\r\n",
                Encoding.ASCII);

            var genericTreeDirectory =
                Path.Combine(
                    root,
                    "Sceneryobjects",
                    "Vegetation");

            var genericAsset =
                Path.Combine(
                    genericTreeDirectory,
                    "generic_tree.sco");

            await File.WriteAllTextAsync(
                genericAsset,
                "[friendlyname]\r\nGeneric Tree\r\n",
                Encoding.ASCII);

            var assets =
                new[]
                {
                    new OmsiAssetIndexEntry(
                        @"Sceneryobjects\Vegetation\generic_tree.sco",
                        OmsiAssetKind.SceneryObject,
                        1,
                        1),
                    new OmsiAssetIndexEntry(
                        @"Sceneryobjects\Vegetation\Tipuana\tipuana_tipu_tree.sco",
                        OmsiAssetKind.SceneryObject,
                        1,
                        1)
                };

            const string xml =
                """
                <osm version="0.6">
                  <node id="10" lat="-23.55000" lon="-46.63000">
                    <tag k="natural" v="tree"/>
                    <tag k="species" v="Tipuana tipu"/>
                    <tag k="genus" v="Tipuana"/>
                  </node>
                  <node id="20" lat="-23.55005" lon="-46.62995">
                    <tag k="natural" v="tree"/>
                  </node>
                  <node id="30" lat="-23.55008" lon="-46.62992">
                    <tag k="natural" v="shrub"/>
                    <tag k="species" v="Duranta erecta"/>
                    <tag k="genus" v="Duranta"/>
                  </node>
                </osm>
                """;

            var originalSecondTile =
                await File.ReadAllTextAsync(
                    secondTile);

            var result =
                await new MapStudioVegetationBatchReconstructionRunner()
                    .RunAsync(
                        root,
                        mapDirectory,
                        xml,
                        new MapStudioGeographicAnchor(
                            -23.55000,
                            -46.63000,
                            50,
                            50),
                        assets);

            var placement =
                Assert.Single(
                    result.Placements);

            Assert.Equal(
                "osm-vegetation-10",
                placement.Id);

            Assert.Equal(
                71,
                placement.ObjectId);

            Assert.EndsWith(
                @"Sceneryobjects\Vegetation\Tipuana\tipuana_tipu_tree.sco",
                placement.SceneryObjectPath,
                StringComparison.OrdinalIgnoreCase);

            Assert.Contains(
                "osm-vegetation-20",
                result.ReviewVegetationIds);

            Assert.Contains(
                "osm-vegetation-30",
                result.ReviewVegetationIds);

            Assert.Empty(
                result.MissingAssetVegetationIds);

            Assert.Empty(
                result.OutsideMapVegetationIds);

            Assert.Equal(
                1,
                result.PlacedVegetationCount);

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
    public async Task RunAsyncReportsMissingVegetationAssetWithoutChangingMap()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-vegetation-missing-" +
                Guid.NewGuid().ToString("N"));

        try
        {
            var mapDirectory =
                Path.Combine(
                    root,
                    "maps",
                    "VegetationBatchTest");

            Directory.CreateDirectory(
                mapDirectory);

            await File.WriteAllTextAsync(
                Path.Combine(
                    mapDirectory,
                    "global.cfg"),
                "[name]\r\nVegetation Batch Test\r\n" +
                "[map]\r\n0\r\n0\r\ntile_0_0.map\r\n",
                Encoding.UTF8);

            var tilePath =
                Path.Combine(
                    mapDirectory,
                    "tile_0_0.map");

            const string original =
                "[version]\r\n14\r\n";

            await File.WriteAllTextAsync(
                tilePath,
                original,
                Encoding.UTF8);

            const string xml =
                """
                <osm version="0.6">
                  <node id="10" lat="-23.55000" lon="-46.63000">
                    <tag k="natural" v="tree"/>
                    <tag k="species" v="Tipuana tipu"/>
                    <tag k="genus" v="Tipuana"/>
                  </node>
                </osm>
                """;

            var result =
                await new MapStudioVegetationBatchReconstructionRunner()
                    .RunAsync(
                        root,
                        mapDirectory,
                        xml,
                        new MapStudioGeographicAnchor(
                            -23.55000,
                            -46.63000,
                            50,
                            50),
                        Array.Empty<OmsiAssetIndexEntry>());

            Assert.Empty(
                result.Placements);

            Assert.Contains(
                "osm-vegetation-10",
                result.MissingAssetVegetationIds);

            Assert.Empty(
                result.BackupPaths);

            Assert.Equal(
                original,
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
