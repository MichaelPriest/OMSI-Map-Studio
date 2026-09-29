using System.Text;
using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Scene;
using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Indexing;
using MapStudio.Core.Omsi.Structures;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioRealWorldSceneReconstructionRunnerTests
{
    [Fact]
    public async Task RunAsyncCoordinatesBuildingVegetationAndFurnitureInOneSession()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-real-scene-" +
                Guid.NewGuid().ToString("N"));

        try
        {
            var mapDirectory =
                await CreateMapAsync(
                    root,
                    initialObjectId: 10);

            var assets =
                new List<OmsiAssetIndexEntry>();

            AddAsset(
                root,
                assets,
                @"Sceneryobjects\Vegetation\Tipuana\tipuana_tree.sco");

            AddAsset(
                root,
                assets,
                @"Sceneryobjects\StreetFurniture\street_lamp.sco");

            var result =
                await new MapStudioRealWorldSceneReconstructionRunner()
                    .RunAsync(
                        root,
                        mapDirectory,
                        SceneXml,
                        new MapStudioGeographicAnchor(
                            -23.55000,
                            -46.63000,
                            50,
                            50),
                        assets,
                        BuildStreetLevelEvidence(
                            "osm-street-furniture-50"));

            Assert.Equal(
                1,
                result.Buildings.GeneratedBuildingCount);

            Assert.Equal(
                1,
                result.Vegetation.PlacedVegetationCount);

            Assert.Equal(
                1,
                result.StreetFurniture.PlacedFeatureCount);

            Assert.Equal(
                3,
                result.PlacedObjectCount);

            Assert.True(
                Directory.Exists(
                    result.SessionBackupDirectory));

            var tilePath =
                Path.Combine(
                    mapDirectory,
                    "tile_0_0.map");

            var updated =
                await OmsiConfigParser
                    .ParseFileAsync(
                        tilePath);

            Assert.Equal(
                4,
                updated
                    .FindSections(
                        "object")
                    .Count());

            var ids =
                updated
                    .FindSections(
                        "object")
                    .Select(
                        section =>
                            int.Parse(
                                section.ContentLines[1]))
                    .ToArray();

            Assert.Contains(
                11,
                ids);

            Assert.Contains(
                12,
                ids);

            Assert.Contains(
                13,
                ids);
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
    public async Task RunAsyncRestoresTilesWhenLaterCategoryFails()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-real-scene-rollback-" +
                Guid.NewGuid().ToString("N"));

        try
        {
            var mapDirectory =
                await CreateMapAsync(
                    root,
                    initialObjectId: 10);

            var tilePath =
                Path.Combine(
                    mapDirectory,
                    "tile_0_0.map");

            var original =
                await File.ReadAllBytesAsync(
                    tilePath);

            var assets =
                new[]
                {
                    new OmsiAssetIndexEntry(
                        @"C:\outside\street_lamp.sco",
                        OmsiAssetKind.SceneryObject,
                        1,
                        1)
                };

            await Assert.ThrowsAsync<
                InvalidDataException>(
                    () =>
                        new MapStudioRealWorldSceneReconstructionRunner()
                            .RunAsync(
                                root,
                                mapDirectory,
                                SceneXmlWithoutTree,
                                new MapStudioGeographicAnchor(
                                    -23.55000,
                                    -46.63000,
                                    50,
                                    50),
                                assets,
                                BuildStreetLevelEvidence(
                                    "osm-street-furniture-50")));

            Assert.Equal(
                original,
                await File.ReadAllBytesAsync(
                    tilePath));

            var restored =
                await OmsiConfigParser
                    .ParseFileAsync(
                        tilePath);

            Assert.Single(
                restored
                    .FindSections(
                        "object"));
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

    private static async Task<string>
        CreateMapAsync(
            string root,
            int initialObjectId)
    {
        var mapDirectory =
            Path.Combine(
                root,
                "maps",
                "RealSceneTest");

        Directory.CreateDirectory(
            mapDirectory);

        await File.WriteAllTextAsync(
            Path.Combine(
                mapDirectory,
                "global.cfg"),
            "[name]\r\nReal Scene Test\r\n" +
            "[map]\r\n0\r\n0\r\ntile_0_0.map\r\n",
            Encoding.UTF8);

        await File.WriteAllTextAsync(
            Path.Combine(
                mapDirectory,
                "tile_0_0.map"),
            "[version]\r\n14\r\n" +
            "[object]\r\n0\r\n" +
            "Sceneryobjects\\Pack\\Existing.sco\r\n" +
            initialObjectId +
            "\r\n10\r\n0\r\n20\r\n0\r\n0\r\n0\r\n",
            Encoding.UTF8);

        return mapDirectory;
    }

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
                        0.94,
                        "street-image-" + id)
                ],
            StringComparer.Ordinal);

    private const string SceneXml =
        """
        <osm version="0.6">
          <node id="1" lat="-23.55000" lon="-46.63000"/>
          <node id="2" lat="-23.55000" lon="-46.62990"/>
          <node id="3" lat="-23.55008" lon="-46.62990"/>
          <node id="4" lat="-23.55008" lon="-46.63000"/>

          <node id="40" lat="-23.55004" lon="-46.62996">
            <tag k="natural" v="tree"/>
            <tag k="species" v="Tipuana tipu"/>
            <tag k="genus" v="Tipuana"/>
          </node>

          <node id="50" lat="-23.55005" lon="-46.62995">
            <tag k="highway" v="street_lamp"/>
          </node>

          <way id="100">
            <nd ref="1"/><nd ref="2"/><nd ref="3"/><nd ref="4"/><nd ref="1"/>
            <tag k="building" v="apartments"/>
            <tag k="building:levels" v="4"/>
            <tag k="height" v="14"/>
            <tag k="roof:shape" v="hipped"/>
            <tag k="roof:height" v="2"/>
            <tag k="addr:street" v="Rua Teste"/>
            <tag k="addr:housenumber" v="100"/>
          </way>
        </osm>
        """;

    private const string SceneXmlWithoutTree =
        """
        <osm version="0.6">
          <node id="1" lat="-23.55000" lon="-46.63000"/>
          <node id="2" lat="-23.55000" lon="-46.62990"/>
          <node id="3" lat="-23.55008" lon="-46.62990"/>
          <node id="4" lat="-23.55008" lon="-46.63000"/>

          <node id="50" lat="-23.55005" lon="-46.62995">
            <tag k="highway" v="street_lamp"/>
          </node>

          <way id="100">
            <nd ref="1"/><nd ref="2"/><nd ref="3"/><nd ref="4"/><nd ref="1"/>
            <tag k="building" v="apartments"/>
            <tag k="building:levels" v="4"/>
            <tag k="height" v="14"/>
            <tag k="roof:shape" v="hipped"/>
            <tag k="roof:height" v="2"/>
            <tag k="addr:street" v="Rua Teste"/>
            <tag k="addr:housenumber" v="100"/>
          </way>
        </osm>
        """;
}
