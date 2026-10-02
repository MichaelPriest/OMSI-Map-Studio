using System.Text;
using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Scene;
using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Indexing;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Structures;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioStreetFurnitureBatchReconstructionRunnerTests
{
    [Fact]
    public async Task RunAsyncPlacesMappedFurnitureWithMatchingOmsiAssets()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-furniture-batch-" +
                Guid.NewGuid().ToString("N"));

        try
        {
            var mapDirectory =
                Path.Combine(
                    root,
                    "maps",
                    "FurnitureBatchTest");

            Directory.CreateDirectory(
                mapDirectory);

            await File.WriteAllTextAsync(
                Path.Combine(
                    mapDirectory,
                    "global.cfg"),
                "[name]\r\nFurniture Batch Test\r\n" +
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
                    90,
                    @"Sceneryobjects\Pack\ExistingB.sco"),
                Encoding.UTF8);

            var assets =
                new List<OmsiAssetIndexEntry>();

            AddAsset(
                root,
                assets,
                @"Sceneryobjects\StreetFurniture\street_lamp.sco");

            AddAsset(
                root,
                assets,
                @"Sceneryobjects\Utilities\power_pole.sco");

            AddAsset(
                root,
                assets,
                @"Sceneryobjects\StreetFurniture\traffic_sign_BR_R_1.sco");

            AddAsset(
                root,
                assets,
                @"Sceneryobjects\Transit\busstop_shelter.sco");

            AddAsset(
                root,
                assets,
                @"Sceneryobjects\StreetFurniture\bench.sco");

            const string xml =
                """
                <osm version="0.6">
                  <node id="10" lat="-23.55000" lon="-46.63000">
                    <tag k="highway" v="street_lamp"/>
                  </node>
                  <node id="20" lat="-23.55002" lon="-46.62998">
                    <tag k="power" v="pole"/>
                    <tag k="operator" v="ENEL"/>
                  </node>
                  <node id="30" lat="-23.55004" lon="-46.62996">
                    <tag k="traffic_sign" v="BR:R-1"/>
                    <tag k="ref" v="BR:R-1"/>
                    <tag k="direction" v="0"/>
                  </node>
                  <node id="40" lat="-23.55006" lon="-46.62994">
                    <tag k="highway" v="bus_stop"/>
                    <tag k="shelter" v="yes"/>
                    <tag k="shelter_type" v="public_transport"/>
                    <tag k="name" v="Parada Central"/>
                  </node>
                </osm>
                """;

            var originalSecondTile =
                await File.ReadAllTextAsync(
                    secondTile);

            var result =
                await new MapStudioStreetFurnitureBatchReconstructionRunner()
                    .RunAsync(
                        root,
                        mapDirectory,
                        xml,
                        new MapStudioGeographicAnchor(
                            -23.55000,
                            -46.63000,
                            50,
                            50),
                        assets,
                        BuildStreetLevelEvidence(
                            "osm-street-furniture-10",
                            "osm-street-furniture-20",
                            "osm-street-furniture-30",
                            "osm-street-furniture-40"));

            Assert.Equal(
                4,
                result.Placements.Count);

            Assert.Equal(
                new[]
                {
                    91,
                    92,
                    93,
                    94
                },
                result.Placements
                    .Select(
                        placement =>
                            placement.ObjectId)
                    .ToArray());

            Assert.Empty(
                result.ReviewFeatureIds);

            Assert.Empty(
                result.MissingAssetFeatureIds);

            Assert.Empty(
                result.OutsideMapFeatureIds);

            Assert.Equal(
                1,
                result.ModifiedTileCount);

            Assert.Single(
                result.BackupPaths);

            Assert.Contains(
                result.Placements,
                placement =>
                    placement.SceneryObjectPath
                        .EndsWith(
                            @"street_lamp.sco",
                            StringComparison.OrdinalIgnoreCase));

            Assert.Contains(
                result.Placements,
                placement =>
                    placement.SceneryObjectPath
                        .EndsWith(
                            @"power_pole.sco",
                            StringComparison.OrdinalIgnoreCase));

            Assert.Contains(
                result.Placements,
                placement =>
                    placement.SceneryObjectPath
                        .EndsWith(
                            @"traffic_sign_BR_R_1.sco",
                            StringComparison.OrdinalIgnoreCase));

            Assert.Contains(
                result.Placements,
                placement =>
                    placement.SceneryObjectPath
                        .EndsWith(
                            @"busstop_shelter.sco",
                            StringComparison.OrdinalIgnoreCase));

            Assert.DoesNotContain(
                result.Placements,
                placement =>
                    placement.SceneryObjectPath
                        .EndsWith(
                            @"bench.sco",
                            StringComparison.OrdinalIgnoreCase));

            var updated =
                await OmsiConfigParser
                    .ParseFileAsync(
                        firstTile);

            Assert.Equal(
                5,
                updated
                    .FindSections(
                        "object")
                    .Count());

            var content =
                await new OmsiTileReader()
                    .ReadContentAsync(
                        firstTile);

            var trafficSign =
                Assert.Single(
                    content.Objects,
                    item =>
                        item.SceneryObjectPath
                            .Contains(
                                "traffic_sign_BR_R_1.sco",
                                StringComparison.OrdinalIgnoreCase));

            Assert.InRange(
                trafficSign.Rotation,
                179.999,
                180.001);

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
    public async Task RunAsyncSkipsFeaturesAlreadyConsumedByRoadJunctions()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-furniture-consumed-" +
                Guid.NewGuid().ToString("N"));

        try
        {
            var mapDirectory =
                Path.Combine(
                    root,
                    "maps",
                    "FurnitureConsumedTest");

            Directory.CreateDirectory(
                mapDirectory);

            await File.WriteAllTextAsync(
                Path.Combine(
                    mapDirectory,
                    "global.cfg"),
                "[name]\r\nFurniture Consumed Test\r\n" +
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

            var assets =
                new List<OmsiAssetIndexEntry>();

            AddAsset(
                root,
                assets,
                @"Sceneryobjects\StreetFurniture\traffic_light.sco");

            const string xml =
                """
                <osm version="0.6">
                  <node id="10" lat="-23.55000" lon="-46.63000">
                    <tag k="highway" v="traffic_signals"/>
                  </node>
                  <node id="20" lat="-23.55002" lon="-46.62998">
                    <tag k="highway" v="traffic_signals"/>
                  </node>
                </osm>
                """;

            var result =
                await new MapStudioStreetFurnitureBatchReconstructionRunner()
                    .RunAsync(
                        root,
                        mapDirectory,
                        xml,
                        new MapStudioGeographicAnchor(
                            -23.55000,
                            -46.63000,
                            50,
                            50),
                        assets,
                        cancellationToken:
                            default,
                        elevation:
                            null,
                        excludedFeatureIds:
                            new HashSet<string>(
                                StringComparer.Ordinal)
                            {
                                "osm-street-furniture-10"
                            });

            var placement =
                Assert.Single(
                    result.Placements);

            Assert.Equal(
                "osm-street-furniture-20",
                placement.Id);

            var content =
                await new OmsiTileReader()
                    .ReadContentAsync(
                        tilePath);

            Assert.Single(
                content.Objects);

            Assert.DoesNotContain(
                content.Objects,
                item =>
                    item.ObjectId ==
                        0);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive:
                        true);
            }
        }
    }

    [Fact]
    public async Task RunAsyncPlacesCompatibleStockTrafficSignAndRejectsUnrelatedCode()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-furniture-stock-sign-" +
                Guid.NewGuid().ToString("N"));

        try
        {
            var mapDirectory =
                Path.Combine(
                    root,
                    "maps",
                    "FurnitureStockSignTest");

            Directory.CreateDirectory(
                mapDirectory);

            await File.WriteAllTextAsync(
                Path.Combine(
                    mapDirectory,
                    "global.cfg"),
                "[name]\r\nFurniture Stock Sign Test\r\n" +
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

            var assets =
                new List<OmsiAssetIndexEntry>();

            AddAsset(
                root,
                assets,
                @"Sceneryobjects\Verkehrszeichen_MC\VZ_205.sco");

            AddAsset(
                root,
                assets,
                @"Sceneryobjects\Verkehrszeichen_MC\VZ_206.sco");

            const string xml =
                """
                <osm version="0.6">
                  <node id="10" lat="-23.55000" lon="-46.63000">
                    <tag k="traffic_sign" v="DE:206"/>
                    <tag k="direction" v="90"/>
                  </node>
                  <node id="20" lat="-23.55002" lon="-46.62998">
                    <tag k="traffic_sign" v="BR:R-1"/>
                    <tag k="direction" v="0"/>
                  </node>
                </osm>
                """;

            var result =
                await new MapStudioStreetFurnitureBatchReconstructionRunner()
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

            Assert.EndsWith(
                @"Verkehrszeichen_MC\VZ_206.sco",
                placement.SceneryObjectPath,
                StringComparison.OrdinalIgnoreCase);

            Assert.Contains(
                "osm-street-furniture-20",
                result.MissingAssetFeatureIds);

            var content =
                await new OmsiTileReader()
                    .ReadContentAsync(
                        tilePath);

            var sign =
                Assert.Single(
                    content.Objects);

            Assert.Contains(
                "VZ_206.sco",
                sign.SceneryObjectPath,
                StringComparison.OrdinalIgnoreCase);

            Assert.InRange(
                sign.Rotation,
                89.999,
                90.001);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive:
                        true);
            }
        }
    }

    [Fact]
    public async Task RunAsyncReportsMissingFurnitureAssetWithoutChangingMap()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-furniture-missing-" +
                Guid.NewGuid().ToString("N"));

        try
        {
            var mapDirectory =
                Path.Combine(
                    root,
                    "maps",
                    "FurnitureBatchTest");

            Directory.CreateDirectory(
                mapDirectory);

            await File.WriteAllTextAsync(
                Path.Combine(
                    mapDirectory,
                    "global.cfg"),
                "[name]\r\nFurniture Batch Test\r\n" +
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
                    <tag k="highway" v="street_lamp"/>
                  </node>
                </osm>
                """;

            var result =
                await new MapStudioStreetFurnitureBatchReconstructionRunner()
                    .RunAsync(
                        root,
                        mapDirectory,
                        xml,
                        new MapStudioGeographicAnchor(
                            -23.55000,
                            -46.63000,
                            50,
                            50),
                        Array.Empty<OmsiAssetIndexEntry>(),
                        BuildStreetLevelEvidence(
                            "osm-street-furniture-10"));

            Assert.Empty(
                result.Placements);

            Assert.Contains(
                "osm-street-furniture-10",
                result.MissingAssetFeatureIds);

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

    private static IReadOnlyDictionary<
        string,
        IReadOnlyList<MapStudioSceneEvidence>>
        BuildStreetLevelEvidence(
            params string[] ids) =>
        ids.ToDictionary(
            id =>
                id,
            id =>
                (IReadOnlyList<MapStudioSceneEvidence>)
                [
                    new(
                        MapStudioSceneEvidenceSource
                            .StreetLevelImagery,
                        0.92,
                        "test-street-image-" + id)
                ],
            StringComparer.Ordinal);

    private static void AddAsset(
        string root,
        ICollection<OmsiAssetIndexEntry> assets,
        string relativePath)
    {
        var path =
            Path.Combine(
                root,
                relativePath
                    .Replace(
                        '\\',
                        Path.DirectorySeparatorChar));

        Directory.CreateDirectory(
            Path.GetDirectoryName(path)!);

        File.WriteAllText(
            path,
            "[friendlyname]\r\nTest\r\n",
            Encoding.ASCII);

        assets.Add(
            new OmsiAssetIndexEntry(
                relativePath,
                OmsiAssetKind.SceneryObject,
                1,
                1));
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
