using System.Net;
using System.Text;
using MapStudio.Core.Generation.Terrain;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioOpenMeteoElevationSurfaceClientTests
{
    [Fact]
    public async Task DownloadBuildsGeoreferencedSurface()
    {
        var handler =
            new DelegateHandler(
                request =>
                {
                    Assert.Contains(
                        "customer-api.open-meteo.com/v1/elevation",
                        request.RequestUri!.AbsoluteUri,
                        StringComparison.Ordinal);

                    Assert.Contains(
                        "apikey=test-key",
                        request.RequestUri.AbsoluteUri,
                        StringComparison.Ordinal);

                    return JsonResponse(
                        """
                        {
                          "elevation": [100.0, 110.0, 120.0, 130.0]
                        }
                        """);
                });

        using var httpClient =
            new HttpClient(
                handler);

        var surface =
            await new MapStudioOpenMeteoElevationSurfaceClient(
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
            "open-meteo-copernicus",
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

                    var latitudeText =
                        query
                            .Split(
                                "latitude=",
                                StringSplitOptions.None)[1]
                            .Split(
                                '&')[0];

                    var count =
                        latitudeText
                            .Split(
                                ',',
                                StringSplitOptions.RemoveEmptyEntries)
                            .Length;

                    var elevations =
                        string.Join(
                            ",",
                            Enumerable.Range(
                                    0,
                                    count)
                                .Select(
                                    index =>
                                        (100 + index)
                                            .ToString(
                                                System.Globalization
                                                    .CultureInfo
                                                    .InvariantCulture)));

                    return JsonResponse(
                        $"{{\"elevation\":[{elevations}]}}");
                });

        using var httpClient =
            new HttpClient(
                handler);

        var surface =
            await new MapStudioOpenMeteoElevationSurfaceClient(
                    httpClient)
                .DownloadAsync(
                    "test-key",
                    south: -1,
                    west: 10,
                    north: 1,
                    east: 12,
                    rows: 11,
                    columns: 11);

        Assert.Equal(
            121,
            surface.Grid.SampleCount);

        Assert.Equal(
            2,
            calls);
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
