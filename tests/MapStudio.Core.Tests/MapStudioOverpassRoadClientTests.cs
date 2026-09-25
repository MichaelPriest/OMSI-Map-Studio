using System.Net;
using System.Net.Http;
using MapStudio.Core.Generation.Roads;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioOverpassRoadClientTests
{
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

                        if (
                            request.RequestUri!
                                .Host ==
                            "primary.test")
                        {
                            return new HttpResponseMessage(
                                HttpStatusCode
                                    .ServiceUnavailable);
                        }

                        return XmlResponse(
                            SampleOsm);
                    }))
            {
                Timeout =
                    TimeSpan.FromSeconds(
                        5)
            };

        var client =
            new MapStudioOverpassRoadClient(
                httpClient,
                [
                    new Uri(
                        "https://primary.test/api/interpreter"),
                    new Uri(
                        "https://fallback.test/api/interpreter")
                ]);

        var result =
            await client.DownloadAsync(
                -23.551,
                -46.634,
                -23.549,
                -46.632);

        Assert.Single(
            result.Traces);

        Assert.Equal(
            1,
            result.SuccessfulChunkCount);

        Assert.Equal(
            0,
            result.FailedChunkCount);

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
    public async Task DownloaderChunksLargeAreaAndDeduplicatesWays()
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
                            SampleOsm);
                    }))
            {
                Timeout =
                    TimeSpan.FromSeconds(
                        5)
            };

        var client =
            new MapStudioOverpassRoadClient(
                httpClient,
                [
                    new Uri(
                        "https://primary.test/api/interpreter")
                ]);

        var result =
            await client.DownloadAsync(
                -23.57,
                -46.66,
                -23.53,
                -46.61);

        Assert.True(
            requestCount >
            1);

        Assert.Single(
            result.Traces);

        Assert.Equal(
            requestCount,
            result.SuccessfulChunkCount);

        Assert.Equal(
            requestCount,
            result.RequestAttemptCount);
    }

    [Fact]
    public async Task DownloaderRejectsIncompleteAreaAfterRecoveryFails()
    {
        var requestCount =
            0;

        using var httpClient =
            new HttpClient(
                new DelegateHandler(
                    _ =>
                    {
                        requestCount++;

                        return requestCount ==
                            1
                            ? XmlResponse(
                                SampleOsm)
                            : new HttpResponseMessage(
                                HttpStatusCode
                                    .GatewayTimeout);
                    }))
            {
                Timeout =
                    TimeSpan.FromSeconds(
                        5)
            };

        var client =
            new MapStudioOverpassRoadClient(
                httpClient,
                [
                    new Uri(
                        "https://primary.test/api/interpreter")
                ]);

        var exception =
            await Assert.ThrowsAsync<
                HttpRequestException>(
                    () =>
                        client.DownloadAsync(
                            -23.57,
                            -46.66,
                            -23.53,
                            -46.61));

        Assert.Contains(
            "incompleta",
            exception.Message,
            StringComparison
                .OrdinalIgnoreCase);

        Assert.True(
            requestCount >
            2);
    }

    [Fact]
    public async Task DownloaderRecoversFailedParentBySubdividingArea()
    {
        var requestCount =
            0;

        using var httpClient =
            new HttpClient(
                new DelegateHandler(
                    _ =>
                    {
                        requestCount++;

                        return requestCount ==
                            1
                            ? new HttpResponseMessage(
                                HttpStatusCode
                                    .GatewayTimeout)
                            : XmlResponse(
                                SampleOsm);
                    }))
            {
                Timeout =
                    TimeSpan.FromSeconds(
                        5)
            };

        var client =
            new MapStudioOverpassRoadClient(
                httpClient,
                [
                    new Uri(
                        "https://primary.test/api/interpreter")
                ]);

        var result =
            await client.DownloadAsync(
                -23.551,
                -46.634,
                -23.549,
                -46.632);

        Assert.Single(
            result.Traces);

        Assert.Equal(
            4,
            result.SuccessfulChunkCount);

        Assert.Equal(
            0,
            result.FailedChunkCount);

        Assert.Equal(
            5,
            requestCount);
    }

    private static HttpResponseMessage
        XmlResponse(
            string xml) =>
        new(
            HttpStatusCode.OK)
        {
            Content =
                new StringContent(
                    xml)
        };

    private sealed class DelegateHandler(
        Func<HttpRequestMessage, HttpResponseMessage>
            handler)
        : HttpMessageHandler
    {
        protected override Task<
            HttpResponseMessage>
            SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken) =>
            Task.FromResult(
                handler(
                    request));
    }

    private const string SampleOsm =
        """
        <osm version="0.6">
          <node id="1" lat="-23.5500" lon="-46.6330" />
          <node id="2" lat="-23.5500" lon="-46.6320" />
          <way id="100">
            <nd ref="1" />
            <nd ref="2" />
            <tag k="highway" v="residential" />
          </way>
        </osm>
        """;
}
