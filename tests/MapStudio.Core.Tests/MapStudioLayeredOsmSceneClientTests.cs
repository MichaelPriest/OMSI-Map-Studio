using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using MapStudio.Core.Generation.Scene;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioLayeredOsmSceneClientTests
{
    [Fact]
    public async Task DownloaderSeparatesHeavySceneCategoriesIntoFiveLayerQueries()
    {
        var queries =
            new ConcurrentQueue<string>();

        using var httpClient =
            new HttpClient(
                new DelegateHandler(
                    request =>
                    {
                        queries.Enqueue(
                            ReadQuery(
                                request));

                        return XmlResponse(
                            SampleSceneOsm);
                    }))
            {
                Timeout =
                    TimeSpan.FromSeconds(5)
            };

        var result =
            await new MapStudioLayeredOsmSceneClient(
                    httpClient,
                    [
                        new Uri(
                            "https://scene.test/api/interpreter")
                    ])
                .DownloadAsync(
                    -23.5510,
                    -46.6340,
                    -23.5500,
                    -46.6330);

        var captured =
            queries.ToArray();

        Assert.Equal(
            5,
            captured.Length);

        Assert.Contains(
            captured,
            query =>
                query.Contains(
                    "way[\"highway\"~",
                    StringComparison.Ordinal) &&
                query.Contains(
                    "motorway",
                    StringComparison.Ordinal) &&
                query.Contains(
                    "busway",
                    StringComparison.Ordinal) &&
                !query.Contains(
                    "way[\"building\"]",
                    StringComparison.Ordinal));

        Assert.Contains(
            captured,
            query =>
                query.Contains(
                    "way[\"building\"]",
                    StringComparison.Ordinal) &&
                query.Contains(
                    "relation[\"building\"]",
                    StringComparison.Ordinal));

        Assert.Contains(
            captured,
            query =>
                query.Contains(
                    "way[\"barrier\"~\"^(wall|fence|guard_rail)$\"]",
                    StringComparison.Ordinal));

        Assert.Contains(
            captured,
            query =>
                query.Contains(
                    "node[\"natural\"~\"^(tree|shrub)$\"]",
                    StringComparison.Ordinal));

        Assert.Contains(
            captured,
            query =>
                query.Contains(
                    "node[\"highway\"=\"street_lamp\"]",
                    StringComparison.Ordinal) &&
                query.Contains(
                    "node[\"highway\"=\"traffic_signals\"]",
                    StringComparison.Ordinal) &&
                query.Contains(
                    "node[\"highway\"=\"crossing\"]",
                    StringComparison.Ordinal));

        Assert.DoesNotContain(
            captured,
            query =>
                query.Contains(
                    "way[\"highway\"~",
                    StringComparison.Ordinal) &&
                query.Contains(
                    "way[\"building\"]",
                    StringComparison.Ordinal) &&
                query.Contains(
                    "node[\"highway\"=\"street_lamp\"]",
                    StringComparison.Ordinal));

        Assert.Equal(
            5,
            result.SuccessfulChunkCount);

        Assert.Equal(
            5,
            result.RequestAttemptCount);

        Assert.Equal(
            0,
            result.FailedChunkCount);
    }

    [Fact]
    public async Task DownloaderHonorsRetryAfterByCoolingRateLimitedEndpoint()
    {
        var counts =
            new ConcurrentDictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

        var updates =
            new ConcurrentQueue<
                MapStudioOverpassSceneDownloadProgress>();

        using var httpClient =
            new HttpClient(
                new DelegateHandler(
                    request =>
                    {
                        var host =
                            request.RequestUri!
                                .Host;

                        counts.AddOrUpdate(
                            host,
                            1,
                            (
                                _,
                                count) =>
                                count + 1);

                        if (
                            host ==
                            "primary.test")
                        {
                            var response =
                                new HttpResponseMessage(
                                    HttpStatusCode
                                        .TooManyRequests);

                            response.Headers
                                .RetryAfter =
                                new RetryConditionHeaderValue(
                                    TimeSpan.FromSeconds(
                                        120));

                            return response;
                        }

                        return XmlResponse(
                            SampleSceneOsm);
                    }))
            {
                Timeout =
                    TimeSpan.FromSeconds(5)
            };

        var result =
            await new MapStudioLayeredOsmSceneClient(
                    httpClient,
                    [
                        new Uri(
                            "https://primary.test/api/interpreter"),
                        new Uri(
                            "https://fallback.test/api/interpreter")
                    ])
                .DownloadAsync(
                    -23.5510,
                    -46.6340,
                    -23.5500,
                    -46.6330,
                    progress:
                        new InlineProgress<
                            MapStudioOverpassSceneDownloadProgress>(
                                updates.Enqueue));

        Assert.Equal(
            1,
            counts["primary.test"]);

        Assert.Equal(
            5,
            counts["fallback.test"]);

        Assert.Equal(
            6,
            result.RequestAttemptCount);

        Assert.Contains(
            updates,
            update =>
                update.Message.Contains(
                    "Retry-After 120s",
                    StringComparison.Ordinal));
    }

    [Fact]
    public async Task DownloaderReusesValidatedLayerChunksFromPersistentCache()
    {
        var cacheRoot =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-osm-layer-cache-" +
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            var firstRequests =
                0;

            using (
                var firstHttpClient =
                    new HttpClient(
                        new DelegateHandler(
                            _ =>
                            {
                                firstRequests++;

                                return XmlResponse(
                                    SampleSceneOsm);
                            }))
                    {
                        Timeout =
                            TimeSpan.FromSeconds(5)
                    })
            {
                var first =
                    await new MapStudioLayeredOsmSceneClient(
                            firstHttpClient,
                            [
                                new Uri(
                                    "https://scene.test/api/interpreter")
                            ])
                        .DownloadAsync(
                            -23.5510,
                            -46.6340,
                            -23.5500,
                            -46.6330,
                            cacheRoot);

                Assert.Equal(
                    5,
                    first.RequestAttemptCount);
            }

            Assert.Equal(
                5,
                firstRequests);

            Assert.Equal(
                5,
                Directory
                    .EnumerateFiles(
                        cacheRoot,
                        "*.osm.xml",
                        SearchOption
                            .AllDirectories)
                    .Count());

            var secondRequests =
                0;

            var updates =
                new ConcurrentQueue<
                    MapStudioOverpassSceneDownloadProgress>();

            using var secondHttpClient =
                new HttpClient(
                    new DelegateHandler(
                        _ =>
                        {
                            secondRequests++;

                            return new HttpResponseMessage(
                                HttpStatusCode
                                    .ServiceUnavailable);
                        }))
                {
                    Timeout =
                        TimeSpan.FromSeconds(5)
                };

            var second =
                await new MapStudioLayeredOsmSceneClient(
                        secondHttpClient,
                        [
                            new Uri(
                                "https://scene.test/api/interpreter")
                        ])
                    .DownloadAsync(
                        -23.5510,
                        -46.6340,
                        -23.5500,
                        -46.6330,
                        cacheRoot,
                        progress:
                            new InlineProgress<
                                MapStudioOverpassSceneDownloadProgress>(
                                    updates.Enqueue));

            Assert.Equal(
                0,
                secondRequests);

            Assert.Equal(
                0,
                second.RequestAttemptCount);

            Assert.Equal(
                5,
                second.SuccessfulChunkCount);

            Assert.Contains(
                updates,
                update =>
                    update.Message.Contains(
                        "cache local validado",
                        StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(
                    cacheRoot))
            {
                Directory.Delete(
                    cacheRoot,
                    recursive:
                        true);
            }
        }
    }

    [Fact]
    public async Task DownloaderFailureNamesLayerAndChunk()
    {
        using var httpClient =
            new HttpClient(
                new DelegateHandler(
                    _ =>
                        new HttpResponseMessage(
                            HttpStatusCode
                                .GatewayTimeout)))
            {
                Timeout =
                    TimeSpan.FromSeconds(5)
            };

        var exception =
            await Assert.ThrowsAsync<
                HttpRequestException>(
                    () =>
                        new MapStudioLayeredOsmSceneClient(
                                httpClient,
                                [
                                    new Uri(
                                        "https://scene.test/api/interpreter")
                                ])
                            .DownloadAsync(
                                -23.5510,
                                -46.6340,
                                -23.5500,
                                -46.6330));

        Assert.Contains(
            "Vias",
            exception.Message,
            StringComparison.Ordinal);

        Assert.Contains(
            "bloco",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
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
                new StringContent(
                    xml)
        };

    private sealed class InlineProgress<T>(
        Action<T> report)
        : IProgress<T>
    {
        public void Report(
            T value) =>
            report(
                value);
    }

    private sealed class DelegateHandler(
        Func<
            HttpRequestMessage,
            HttpResponseMessage>
            handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage>
            SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken) =>
            Task.FromResult(
                handler(
                    request));
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
          </way>
          <way id="300">
            <nd ref="5"/><nd ref="6"/><nd ref="7"/>
            <tag k="barrier" v="wall"/>
          </way>
        </osm>
        """;
}
