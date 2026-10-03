using System.Net;
using System.Text;
using MapStudio.Core.Generation.Terrain;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioGoogleElevationSurfaceClientTests
{
    [Fact]
    public async Task DownloadBuildsNorthToSouthGeoreferencedSurface()
    {
        var handler =
            new DelegateHandler(
                request =>
                {
                    Assert.Contains(
                        "maps.googleapis.com/maps/api/elevation/json",
                        request.RequestUri!.AbsoluteUri,
                        StringComparison.Ordinal);

                    return JsonResponse(
                        """
                        {
                          "status": "OK",
                          "results": [
                            { "elevation": 100.0 },
                            { "elevation": 110.0 },
                            { "elevation": 120.0 },
                            { "elevation": 130.0 }
                          ]
                        }
                        """);
                });

        using var httpClient =
            new HttpClient(
                handler);

        var surface =
            await new MapStudioGoogleElevationSurfaceClient(
                    httpClient)
                .DownloadAsync(
                    "test-key",
                    south: -1,
                    west: 10,
                    north: 1,
                    east: 12,
                    rows: 2,
                    columns: 2);

        Assert.Equal(
            "google-elevation",
            surface.Grid.SourceFormat);

        Assert.Equal(
            100,
            surface.Grid.MinimumElevation);

        Assert.Equal(
            130,
            surface.Grid.MaximumElevation);

        Assert.True(
            surface.TrySampleAbsoluteHeight(
                1,
                10,
                out var northWest));

        Assert.Equal(
            100,
            northWest,
            6);

        Assert.True(
            surface.TrySampleAbsoluteHeight(
                -1,
                12,
                out var southEast));

        Assert.Equal(
            130,
            southEast,
            6);

        Assert.True(
            surface.TrySampleRelativeHeight(
                0,
                11,
                out var center));

        Assert.Equal(
            15,
            center,
            6);
    }

    [Fact]
    public async Task DownloadSplitsLargeGridIntoBoundedBatches()
    {
        var calls =
            0;

        var handler =
            new DelegateHandler(
                request =>
                {
                    calls++;

                    var query =
                        Uri.UnescapeDataString(
                            request.RequestUri!.Query);

                    var locationsText =
                        query
                            .Split(
                                "locations=",
                                StringSplitOptions.None)[1]
                            .Split(
                                '&')[0];

                    var count =
                        locationsText
                            .Split(
                                '|',
                                StringSplitOptions.RemoveEmptyEntries)
                            .Length;

                    var results =
                        string.Join(
                            ",",
                            Enumerable.Range(
                                    0,
                                    count)
                                .Select(
                                    index =>
                                        $"{{\"elevation\":{100 + index}}}"));

                    return JsonResponse(
                        $"{{\"status\":\"OK\",\"results\":[{results}]}}");
                });

        using var httpClient =
            new HttpClient(
                handler);

        var surface =
            await new MapStudioGoogleElevationSurfaceClient(
                    httpClient)
                .DownloadAsync(
                    "test-key",
                    south: -1,
                    west: 10,
                    north: 1,
                    east: 12,
                    rows: 9,
                    columns: 9);

        Assert.Equal(
            81,
            surface.Grid.SampleCount);

        Assert.Equal(
            2,
            calls);
    }

    [Fact]
    public async Task DownloadRejectsProviderErrorStatus()
    {
        var handler =
            new DelegateHandler(
                _ =>
                    JsonResponse(
                        """
                        {
                          "status": "REQUEST_DENIED",
                          "results": []
                        }
                        """));

        using var httpClient =
            new HttpClient(
                handler);

        var exception =
            await Assert.ThrowsAsync<InvalidDataException>(
                () =>
                    new MapStudioGoogleElevationSurfaceClient(
                            httpClient)
                        .DownloadAsync(
                            "bad-key",
                            south: -1,
                            west: 10,
                            north: 1,
                            east: 12,
                            rows: 2,
                            columns: 2));

        Assert.Equal(
            "googleElevation:REQUEST_DENIED",
            exception.Message);
    }

    private static HttpResponseMessage JsonResponse(
        string json) =>
        new(
            HttpStatusCode.OK)
        {
            Content =
                new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json")
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
}
