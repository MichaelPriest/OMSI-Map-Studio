using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using MapStudio.Core.Generation.Buildings;
using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Scene;
using MapStudio.Core.Generation.Vegetation;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioOverpassSceneClientTests
{
    [Fact]
    public async Task DownloaderReturnsUnifiedXmlConsumableBySceneImporters()
    {
        string? query = null;

        using var httpClient =
            new HttpClient(
                new DelegateHandler(
                    request =>
                    {
                        query =
                            ReadQuery(
                                request);

                        return XmlResponse(
                            SampleSceneOsm);
                    }))
            {
                Timeout =
                    TimeSpan.FromSeconds(5)
            };

        var result =
            await new MapStudioOverpassSceneClient(
                    httpClient,
                    [
                        new Uri(
                            "https://scene.test/api/interpreter")
                    ])
                .DownloadAsync(
                    -23.551,
                    -46.634,
                    -23.549,
                    -46.632);

        Assert.NotNull(query);

        Assert.Contains(
            "way[\"highway\"]",
            query,
            StringComparison.Ordinal);

        Assert.Contains(
            "way[\"building\"]",
            query,
            StringComparison.Ordinal);

        Assert.Contains(
            "node[\"natural\"~\"^(tree|shrub)$\"]",
            query,
            StringComparison.Ordinal);

        Assert.Contains(
            "node[\"highway\"=\"street_lamp\"]",
            query,
            StringComparison.Ordinal);

        Assert.Contains(
            "way[\"barrier\"~\"^(wall|fence|guard_rail)$\"]",
            query,
            StringComparison.Ordinal);

        Assert.Contains(
            "way[\"amenity\"=\"parking\"]",
            query,
            StringComparison.Ordinal);

        Assert.Single(
            new MapStudioOsmRoadImporter()
                .Parse(
                    result.OsmXml)
                .Traces);

        Assert.Single(
            new MapStudioOsmBuildingImporter()
                .Parse(
                    result.OsmXml)
                .Buildings);

        Assert.Single(
            new MapStudioOsmVegetationImporter()
                .Parse(
                    result.OsmXml)
                .Points);

        Assert.Single(
            new MapStudioOsmStreetFurnitureImporter()
                .Parse(
                    result.OsmXml)
                .Points);

        Assert.Single(
            new MapStudioOsmInfrastructureImporter()
                .Parse(
                    result.OsmXml)
                .Features);

        Assert.Equal(
            1,
            result.SuccessfulChunkCount);

        Assert.Equal(
            0,
            result.FailedChunkCount);
    }

    [Fact]
    public async Task DownloaderFallsBackToSecondEndpoint()
    {
        var requests =
            new List<Uri>();

        using var httpClient =
            new HttpClient(
                new DelegateHandler(
                    request =>
                    {
                        requests.Add(
                            request.RequestUri!);

                        return request.RequestUri!
                            .Host ==
                            "primary.test"
                            ? new HttpResponseMessage(
                                HttpStatusCode
                                    .ServiceUnavailable)
                            : XmlResponse(
                                SampleSceneOsm);
                    }))
            {
                Timeout =
                    TimeSpan.FromSeconds(5)
            };

        var result =
            await new MapStudioOverpassSceneClient(
                    httpClient,
                    [
                        new Uri(
                            "https://primary.test/api/interpreter"),
                        new Uri(
                            "https://fallback.test/api/interpreter")
                    ])
                .DownloadAsync(
                    -23.551,
                    -46.634,
                    -23.549,
                    -46.632);

        Assert.Equal(
            2,
            result.RequestAttemptCount);

        Assert.Contains(
            "https://fallback.test",
            result.UsedEndpoints);

        Assert.Equal(
            2,
            requests.Count);
    }

    [Fact]
    public async Task DownloaderChunksLargeAreaAndDeduplicatesElements()
    {
        var requestCount =
            0;

        using var httpClient =
            new HttpClient(
                new DelegateHandler(
                    _ =>
                    {
                        requestCount++;

                        return XmlResponse(
                            SampleSceneOsm);
                    }))
            {
                Timeout =
                    TimeSpan.FromSeconds(5)
            };

        var result =
            await new MapStudioOverpassSceneClient(
                    httpClient,
                    [
                        new Uri(
                            "https://scene.test/api/interpreter")
                    ])
                .DownloadAsync(
                    -23.57,
                    -46.66,
                    -23.53,
                    -46.61);

        Assert.True(
            requestCount > 1);

        Assert.Equal(
            requestCount,
            result.SuccessfulChunkCount);

        Assert.Equal(
            10,
            result.NodeCount);

        Assert.Equal(
            3,
            result.WayCount);

        Assert.Equal(
            0,
            result.RelationCount);
    }

    [Fact]
    public async Task DownloaderProcessesPrimaryChunksConcurrentlyAndReportsProgress()
    {
        var stateGate =
            new object();

        var activeRequests =
            0;

        var maximumConcurrentRequests =
            0;

        var startedRequests =
            0;

        var twoRequestsStarted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        var releaseRequests =
            new TaskCompletionSource<bool>(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        using var httpClient =
            new HttpClient(
                new AsyncDelegateHandler(
                    async (
                        _,
                        cancellationToken) =>
                    {
                        lock (stateGate)
                        {
                            activeRequests++;

                            maximumConcurrentRequests =
                                Math.Max(
                                    maximumConcurrentRequests,
                                    activeRequests);

                            startedRequests++;

                            if (startedRequests >= 2)
                            {
                                twoRequestsStarted
                                    .TrySetResult(
                                        true);
                            }
                        }

                        try
                        {
                            await releaseRequests
                                .Task
                                .WaitAsync(
                                    cancellationToken);

                            return XmlResponse(
                                SampleSceneOsm);
                        }
                        finally
                        {
                            lock (stateGate)
                            {
                                activeRequests--;
                            }
                        }
                    }))
            {
                Timeout =
                    TimeSpan.FromSeconds(5)
            };

        var updates =
            new ConcurrentQueue<
                MapStudioOverpassSceneDownloadProgress>();

        var client =
            new MapStudioOverpassSceneClient(
                httpClient,
                [
                    new Uri(
                        "https://scene.test/api/interpreter")
                ]);

        var downloadTask =
            client.DownloadAsync(
                -23.556,
                -46.636,
                -23.544,
                -46.624,
                CancellationToken.None,
                new InlineProgress<
                    MapStudioOverpassSceneDownloadProgress>(
                        updates.Enqueue));

        var concurrencyObserved =
            await Task.WhenAny(
                twoRequestsStarted.Task,
                Task.Delay(
                    TimeSpan.FromSeconds(2)));

        Assert.Same(
            twoRequestsStarted.Task,
            concurrencyObserved);

        releaseRequests
            .TrySetResult(
                true);

        var result =
            await downloadTask;

        Assert.Equal(
            2,
            maximumConcurrentRequests);

        Assert.True(
            result.SuccessfulChunkCount >
                1);

        var finalProgress =
            updates
                .OrderBy(
                    update =>
                        update.CompletedPrimaryChunks)
                .Last();

        Assert.True(
            finalProgress.TotalPrimaryChunks >
                1);

        Assert.Equal(
            finalProgress.TotalPrimaryChunks,
            finalProgress.CompletedPrimaryChunks);

        Assert.Equal(
            result.RequestAttemptCount,
            finalProgress.RequestAttemptCount);

        Assert.Contains(
            "OpenStreetMap",
            finalProgress.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task DownloaderRecoversFailedParentBySubdivision()
    {
        var requestCount =
            0;

        using var httpClient =
            new HttpClient(
                new DelegateHandler(
                    _ =>
                    {
                        requestCount++;

                        return requestCount == 1
                            ? new HttpResponseMessage(
                                HttpStatusCode
                                    .GatewayTimeout)
                            : XmlResponse(
                                SampleSceneOsm);
                    }))
            {
                Timeout =
                    TimeSpan.FromSeconds(5)
            };

        var result =
            await new MapStudioOverpassSceneClient(
                    httpClient,
                    [
                        new Uri(
                            "https://scene.test/api/interpreter")
                    ])
                .DownloadAsync(
                    -23.551,
                    -46.634,
                    -23.549,
                    -46.632);

        Assert.Equal(
            5,
            requestCount);

        Assert.Equal(
            4,
            result.SuccessfulChunkCount);

        Assert.Equal(
            0,
            result.FailedChunkCount);
    }

    private static string ReadQuery(
        HttpRequestMessage request)
    {
        var body =
            request.Content!
                .ReadAsStringAsync()
                .GetAwaiter()
                .GetResult();

        var encoded =
            body.StartsWith(
                "data=",
                StringComparison.Ordinal)
                ? body[5..]
                : body;

        return Uri.UnescapeDataString(
            encoded.Replace(
                '+',
                ' '));
    }

    private static HttpResponseMessage XmlResponse(
        string xml) =>
        new(
            HttpStatusCode.OK)
        {
            Content =
                new StringContent(xml)
        };

    private sealed class InlineProgress<T>(
        Action<T> report)
        : IProgress<T>
    {
        public void Report(
            T value) =>
            report(value);
    }

    private sealed class AsyncDelegateHandler(
        Func<
            HttpRequestMessage,
            CancellationToken,
            Task<HttpResponseMessage>>
            handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage>
            SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken) =>
            handler(
                request,
                cancellationToken);
    }

    private sealed class DelegateHandler(
        Func<HttpRequestMessage, HttpResponseMessage> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage>
            SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken) =>
            Task.FromResult(
                handler(request));
    }

    private const string SampleSceneOsm =
        """
        <osm version="0.6">
          <node id="1" lat="-23.5500" lon="-46.6330"/>
          <node id="2" lat="-23.5500" lon="-46.6320"/>
          <node id="3" lat="-23.5499" lon="-46.6320"/>
          <node id="4" lat="-23.5499" lon="-46.6330"/>
          <node id="5" lat="-23.5502" lon="-46.6330"/>
          <node id="6" lat="-23.5502" lon="-46.6328"/>
          <node id="7" lat="-23.5503" lon="-46.6328"/>
          <node id="8" lat="-23.5503" lon="-46.6330"/>
          <node id="9" lat="-23.5501" lon="-46.6327">
            <tag k="natural" v="tree"/>
            <tag k="species" v="Tipuana tipu"/>
          </node>
          <node id="10" lat="-23.5501" lon="-46.6326">
            <tag k="highway" v="street_lamp"/>
          </node>

          <way id="100">
            <nd ref="1"/><nd ref="2"/>
            <tag k="highway" v="residential"/>
          </way>

          <way id="200">
            <nd ref="1"/><nd ref="2"/><nd ref="3"/><nd ref="4"/><nd ref="1"/>
            <tag k="building" v="apartments"/>
            <tag k="building:levels" v="4"/>
            <tag k="height" v="14"/>
          </way>

          <way id="300">
            <nd ref="5"/><nd ref="6"/><nd ref="7"/>
            <tag k="barrier" v="wall"/>
          </way>
        </osm>
        """;
}
