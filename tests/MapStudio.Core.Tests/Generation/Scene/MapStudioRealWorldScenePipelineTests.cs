using System.Net;
using System.Net.Http;
using System.Text;
using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Scene;
using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Indexing;
using MapStudio.Core.Omsi.Structures;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioRealWorldScenePipelineTests
{
    [Fact]
    public async Task RunAsyncDownloadsAndReconstructsSceneInOneCall()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-scene-pipeline-" +
                Guid.NewGuid().ToString("N"));

        try
        {
            var mapDirectory =
                await CreateMapAsync(root);

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

            using var httpClient =
                new HttpClient(
                    new DelegateHandler(
                        _ =>
                            XmlResponse(
                                SceneXml)))
                {
                    Timeout =
                        TimeSpan.FromSeconds(5)
                };

            var sceneClient =
                new MapStudioOverpassSceneClient(
                    httpClient,
                    [
                        new Uri(
                            "https://scene.test/api/interpreter")
                    ]);

            var progress =
                new List<MapStudioRealWorldScenePipelineProgress>();

            var result =
                await new MapStudioRealWorldScenePipeline(
                        sceneClient)
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
                        BuildStreetLevelEvidence(
                            "osm-street-furniture-50"),
                        new InlineProgress(
                            progress.Add));

            Assert.True(
                result.Download.NodeCount >= 6);

            Assert.Equal(
                3,
                result.Reconstruction
                    .PlacedObjectCount);

            Assert.Equal(
                new[]
                {
                    MapStudioRealWorldScenePipelineStage
                        .DownloadingOpenStreetMap,
                    MapStudioRealWorldScenePipelineStage
                        .ReconstructingScene,
                    MapStudioRealWorldScenePipelineStage
                        .Completed
                },
                progress
                    .Select(
                        item =>
                            item.Stage)
                    .ToArray());

            var tile =
                await OmsiConfigParser
                    .ParseFileAsync(
                        Path.Combine(
                            mapDirectory,
                            "tile_0_0.map"));

            Assert.Equal(
                4,
                tile
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

    [Fact]
    public async Task RunAsyncDoesNotModifyMapWhenOverpassFails()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-scene-pipeline-fail-" +
                Guid.NewGuid().ToString("N"));

        try
        {
            var mapDirectory =
                await CreateMapAsync(root);

            var tilePath =
                Path.Combine(
                    mapDirectory,
                    "tile_0_0.map");

            var original =
                await File.ReadAllBytesAsync(
                    tilePath);

            using var httpClient =
                new HttpClient(
                    new DelegateHandler(
                        _ =>
                            new HttpResponseMessage(
                                HttpStatusCode
                                    .ServiceUnavailable)))
                {
                    Timeout =
                        TimeSpan.FromSeconds(5)
                };

            var sceneClient =
                new MapStudioOverpassSceneClient(
                    httpClient,
                    [
                        new Uri(
                            "https://scene.test/api/interpreter")
                    ]);

            await Assert.ThrowsAsync<
                HttpRequestException>(
                    () =>
                        new MapStudioRealWorldScenePipeline(
                                sceneClient)
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
                                Array.Empty<
                                    OmsiAssetIndexEntry>()));

            Assert.Equal(
                original,
                await File.ReadAllBytesAsync(
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

    private static async Task<string>
        CreateMapAsync(
            string root)
    {
        var mapDirectory =
            Path.Combine(
                root,
                "maps",
                "PipelineTest");

        Directory.CreateDirectory(
            mapDirectory);

        await File.WriteAllTextAsync(
            Path.Combine(
                mapDirectory,
                "global.cfg"),
            "[name]\r\nPipeline Test\r\n" +
            "[map]\r\n0\r\n0\r\ntile_0_0.map\r\n",
            Encoding.UTF8);

        await File.WriteAllTextAsync(
            Path.Combine(
                mapDirectory,
                "tile_0_0.map"),
            "[version]\r\n14\r\n" +
            "[object]\r\n0\r\n" +
            "Sceneryobjects\\Pack\\Existing.sco\r\n" +
            "10\r\n10\r\n0\r\n20\r\n0\r\n0\r\n0\r\n",
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

    private static HttpResponseMessage XmlResponse(
        string xml) =>
        new(
            HttpStatusCode.OK)
        {
            Content =
                new StringContent(xml)
        };

    private sealed class InlineProgress(
        Action<MapStudioRealWorldScenePipelineProgress>
            report)
        : IProgress<MapStudioRealWorldScenePipelineProgress>
    {
        public void Report(
            MapStudioRealWorldScenePipelineProgress value) =>
            report(value);
    }

    private sealed class DelegateHandler(
        Func<HttpRequestMessage, HttpResponseMessage>
            handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage>
            SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken) =>
            Task.FromResult(
                handler(request));
    }

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
}
