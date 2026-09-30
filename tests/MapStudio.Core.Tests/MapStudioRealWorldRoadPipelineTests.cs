using System.Net;
using System.Net.Http;
using System.Text;
using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Scene;
using MapStudio.Core.Generation.Terrain;
using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Indexing;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Structures;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioRealWorldRoadPipelineTests
{
    [Fact]
    public async Task RoadRunnerWritesSelectedRoadKitSplinesInOneTileBatch()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-road-batch-" +
                Guid.NewGuid().ToString("N"));

        try
        {
            var mapDirectory =
                await CreateMapAsync(
                    root);

            var result =
                await new MapStudioRoadBatchReconstructionRunner()
                    .RunAsync(
                        root,
                        mapDirectory,
                        RoadXml,
                        new MapStudioGeographicAnchor(
                            -23.55000,
                            -46.63000,
                            50,
                            50));

            Assert.Equal(
                2,
                result.PlacedSplineCount);

            Assert.Equal(
                1,
                result.ModifiedTileCount);

            Assert.Equal(
                0,
                result.SkippedOutsideMapSegmentCount);

            Assert.Single(
                result.BackupPaths);

            Assert.All(
                result.Placements,
                placement =>
                    Assert.EndsWith(
                        ".sli",
                        placement.SplinePath,
                        StringComparison.OrdinalIgnoreCase));

            Assert.Contains(
                result.Placements,
                placement =>
                    placement.SplinePath.Contains(
                        "sidewalk",
                        StringComparison.OrdinalIgnoreCase));

            Assert.True(
                Directory.Exists(
                    result.RoadKit.PackDirectory));

            var document =
                await OmsiConfigParser
                    .ParseFileAsync(
                        Path.Combine(
                            mapDirectory,
                            "tile_0_0.map"));

            Assert.Equal(
                2,
                document
                    .FindSections(
                        "spline")
                    .Count());

            var tileContent =
                await new OmsiTileReader()
                    .ReadContentAsync(
                        Path.Combine(
                            mapDirectory,
                            "tile_0_0.map"));

            foreach (var placement in result.Placements)
            {
                var spline =
                    Assert.Single(
                        tileContent.Splines,
                        item =>
                            item.SplineId ==
                            placement.SplineId);

                Assert.Equal(
                    placement.LocalX,
                    spline.X,
                    6);

                Assert.Equal(
                    placement.LocalZ,
                    spline.Z,
                    6);

                Assert.Equal(
                    0,
                    spline.Y,
                    6);
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

    [Fact]
    public async Task RoadRunnerUsesDemHeightAndGradientForSplinePlacement()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-road-elevation-" +
                Guid.NewGuid().ToString("N"));

        try
        {
            var mapDirectory =
                await CreateMapAsync(
                    root);

            var anchor =
                new MapStudioGeographicAnchor(
                    -23.55000,
                    -46.63000,
                    50,
                    50);

            var elevation =
                new MapStudioGeoreferencedElevationSurface(
                    new MapStudioElevationGrid(
                        2,
                        2,
                        [
                            100,
                            100,
                            103,
                            103
                        ],
                        100,
                        103,
                        "test"),
                    south: -23.55020,
                    west: -46.63010,
                    north: -23.54990,
                    east: -46.62990);

            var result =
                await new MapStudioRoadBatchReconstructionRunner()
                    .RunAsync(
                        root,
                        mapDirectory,
                        ElevationRoadXml,
                        anchor,
                        CancellationToken.None,
                        elevation);

            var placement =
                Assert.Single(
                    result.Placements);

            var content =
                await new OmsiTileReader()
                    .ReadContentAsync(
                        Path.Combine(
                            mapDirectory,
                            "tile_0_0.map"));

            var spline =
                Assert.Single(
                    content.Splines);

            Assert.Equal(
                placement.LocalZ,
                spline.Z,
                6);

            Assert.InRange(
                spline.Y,
                0.8,
                1.2);

            Assert.InRange(
                spline.GradientStart,
                7.0,
                11.0);

            Assert.Equal(
                spline.GradientStart,
                spline.GradientEnd,
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
    public async Task RoadRunnerGeneratesPhysicalJunctionAtSharedOsmNode()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-road-junction-" +
                Guid.NewGuid().ToString("N"));

        try
        {
            var mapDirectory =
                await CreateMapAsync(
                    root);

            var result =
                await new MapStudioRoadBatchReconstructionRunner()
                    .RunAsync(
                        root,
                        mapDirectory,
                        JunctionXml,
                        new MapStudioGeographicAnchor(
                            -23.55000,
                            -46.63000,
                            50,
                            50));

            Assert.Equal(
                1,
                result.JunctionCandidateCount);

            var junction =
                Assert.Single(
                    result.JunctionPlacements);

            Assert.Equal(
                "osm-junction-3",
                junction.Id);

            Assert.Equal(
                1,
                result.GeneratedJunctionCount);

            Assert.True(
                File.Exists(
                    Path.Combine(
                        root,
                        junction.SceneryObjectPath.Replace(
                            '\\',
                            Path.DirectorySeparatorChar))));

            var tile =
                await OmsiConfigParser
                    .ParseFileAsync(
                        Path.Combine(
                            mapDirectory,
                            "tile_0_0.map"));

            Assert.NotEmpty(
                tile.FindSections(
                    "spline"));

            Assert.Single(
                tile.FindSections(
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

    [Fact]
    public async Task FullMapPipelineDownloadsOnceAndCreatesRoadsAndScene()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-full-map-pipeline-" +
                Guid.NewGuid().ToString("N"));

        try
        {
            var mapDirectory =
                await CreateMapAsync(
                    root);

            var assets =
                new List<OmsiAssetIndexEntry>();

            AddAsset(
                root,
                assets,
                @"Sceneryobjects\Vegetation\Tipuana\tipuana_tree.sco");

            using var httpClient =
                new HttpClient(
                    new DelegateHandler(
                        _ =>
                            XmlResponse(
                                FullSceneXml)))
                {
                    Timeout =
                        TimeSpan.FromSeconds(5)
                };

            var client =
                new MapStudioOverpassSceneClient(
                    httpClient,
                    [
                        new Uri(
                            "https://scene.test/api/interpreter")
                    ]);

            var progress =
                new List<MapStudioRealWorldMapPipelineProgress>();

            var result =
                await new MapStudioRealWorldMapPipeline(
                        client)
                    .RunAsync(
                        root,
                        mapDirectory,
                        -23.551,
                        -46.631,
                        -23.549,
                        -46.629,
                        new MapStudioGeographicAnchor(
                            -23.55000,
                            -46.63000,
                            50,
                            50),
                        assets,
                        progress:
                            new Progress<MapStudioRealWorldMapPipelineProgress>(
                                item =>
                                    progress.Add(
                                        item)));

            Assert.True(
                result.Roads.PlacedSplineCount >
                    0);

            Assert.True(
                result.Scene.PlacedObjectCount >
                    0);

            Assert.True(
                result.PlacedElementCount >=
                    2);

            Assert.True(
                Directory.Exists(
                    result.SessionBackupDirectory));

            var tile =
                await OmsiConfigParser
                    .ParseFileAsync(
                        Path.Combine(
                            mapDirectory,
                            "tile_0_0.map"));

            Assert.NotEmpty(
                tile.FindSections(
                    "spline"));

            Assert.NotEmpty(
                tile.FindSections(
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

    [Fact]
    public void SplineBatchInserterAppendsMultipleSplinesInOnePass()
    {
        var document =
            OmsiConfigParser
                .Parse(
                    "[version]\r\n14\r\n");

        var result =
            OmsiTileSplineInserter
                .AppendMany(
                    document,
                    [
                        CreateSpline(
                            10,
                            0,
                            0,
                            20,
                            90),
                        CreateSpline(
                            11,
                            20,
                            0,
                            20,
                            90)
                    ]);

        var text =
            Encoding.UTF8
                .GetString(
                    result.Bytes);

        Assert.Equal(
            2,
            result.SourceSectionOrdinals.Count);

        Assert.Equal(
            2,
            text.Split(
                    "[spline]",
                    StringSplitOptions.None)
                .Length -
                1);
    }

    private static OmsiNewPlacedSpline CreateSpline(
        int id,
        double x,
        double z,
        double length,
        double rotation) =>
        new(
            "0",
            @"Splines\MapStudio_RoadKit\ms_road_2lane_7m.sli",
            id,
            -1,
            -1,
            x,
            0,
            z,
            rotation,
            length,
            0,
            0,
            0,
            false,
            Array.Empty<string>());

    private static async Task<string> CreateMapAsync(
        string root)
    {
        var mapDirectory =
            Path.Combine(
                root,
                "maps",
                "RoadTest");

        Directory.CreateDirectory(
            mapDirectory);

        await File.WriteAllTextAsync(
            Path.Combine(
                mapDirectory,
                "global.cfg"),
            "[name]\r\nRoad Test\r\n" +
            "[map]\r\n0\r\n0\r\ntile_0_0.map\r\n",
            Encoding.UTF8);

        await File.WriteAllTextAsync(
            Path.Combine(
                mapDirectory,
                "tile_0_0.map"),
            "[version]\r\n14\r\n",
            Encoding.UTF8);

        return mapDirectory;
    }

    private static void AddAsset(
        string root,
        ICollection<OmsiAssetIndexEntry> assets,
        string relativePath)
    {
        var fullPath =
            Path.Combine(
                root,
                relativePath.Replace(
                    '\\',
                    Path.DirectorySeparatorChar));

        Directory.CreateDirectory(
            Path.GetDirectoryName(
                fullPath)!);

        File.WriteAllText(
            fullPath,
            "[friendlyname]\r\nAsset\r\n",
            Encoding.ASCII);

        assets.Add(
            new OmsiAssetIndexEntry(
                relativePath,
                OmsiAssetKind.SceneryObject,
                1,
                1));
    }

    private static HttpResponseMessage XmlResponse(
        string xml) =>
        new(
            HttpStatusCode.OK)
        {
            Content =
                new StringContent(
                    xml,
                    Encoding.UTF8,
                    "application/xml")
        };

    private sealed class DelegateHandler
        : HttpMessageHandler
    {
        private readonly Func<
            HttpRequestMessage,
            HttpResponseMessage>
            _handler;

        public DelegateHandler(
            Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler =
                handler;
        }

        protected override Task<HttpResponseMessage>
            SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken) =>
            Task.FromResult(
                _handler(
                    request));
    }

    private const string RoadXml =
        """
        <osm version="0.6">
          <node id="1" lat="-23.55000" lon="-46.63000"/>
          <node id="2" lat="-23.55000" lon="-46.62990"/>
          <node id="3" lat="-23.55000" lon="-46.62980"/>
          <way id="100">
            <nd ref="1"/><nd ref="2"/><nd ref="3"/>
            <tag k="highway" v="residential"/>
            <tag k="lanes" v="2"/>
            <tag k="width" v="7"/>
          </way>
        </osm>
        """;

    private const string ElevationRoadXml =
        """
        <osm version="0.6">
          <node id="1" lat="-23.55000" lon="-46.63000"/>
          <node id="2" lat="-23.55010" lon="-46.63000"/>
          <way id="150">
            <nd ref="1"/><nd ref="2"/>
            <tag k="highway" v="residential"/>
            <tag k="lanes" v="2"/>
            <tag k="width" v="7"/>
          </way>
        </osm>
        """;

    private const string JunctionXml =
        """
        <osm version="0.6">
          <node id="1" lat="-23.55000" lon="-46.63010"/>
          <node id="2" lat="-23.55000" lon="-46.63005"/>
          <node id="3" lat="-23.55000" lon="-46.63000"/>
          <node id="4" lat="-23.55000" lon="-46.62995"/>
          <node id="5" lat="-23.55000" lon="-46.62990"/>
          <node id="6" lat="-23.55010" lon="-46.63000"/>
          <node id="7" lat="-23.55005" lon="-46.63000"/>
          <node id="8" lat="-23.54995" lon="-46.63000"/>
          <node id="9" lat="-23.54990" lon="-46.63000"/>

          <way id="100">
            <nd ref="1"/><nd ref="2"/><nd ref="3"/><nd ref="4"/><nd ref="5"/>
            <tag k="highway" v="residential"/>
            <tag k="lanes" v="2"/>
            <tag k="width" v="7"/>
          </way>

          <way id="200">
            <nd ref="6"/><nd ref="7"/><nd ref="3"/><nd ref="8"/><nd ref="9"/>
            <tag k="highway" v="secondary"/>
            <tag k="lanes" v="2"/>
            <tag k="width" v="7"/>
          </way>
        </osm>
        """;

    private const string FullSceneXml =
        """
        <osm version="0.6">
          <node id="1" lat="-23.55000" lon="-46.63000"/>
          <node id="2" lat="-23.55000" lon="-46.62990"/>
          <node id="3" lat="-23.55000" lon="-46.62980"/>
          <node id="10" lat="-23.55003" lon="-46.62996">
            <tag k="natural" v="tree"/>
            <tag k="species" v="Tipuana tipu"/>
            <tag k="genus" v="Tipuana"/>
          </node>
          <way id="100">
            <nd ref="1"/><nd ref="2"/><nd ref="3"/>
            <tag k="highway" v="residential"/>
            <tag k="lanes" v="2"/>
            <tag k="width" v="7"/>
          </way>
        </osm>
        """;
}
