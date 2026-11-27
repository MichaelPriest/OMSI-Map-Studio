using System.Net;
using System.Text;
using MapStudio.Core.AI;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioOpenAiCompatibleProviderTests
{
    [Fact]
    public async Task BuildingAnalysisSendsImageAndParsesJson()
    {
        var handler =
            new FakeHandler(
                "{\"choices\":[{\"message\":{\"content\":\"{\\\"widthMeters\\\":12,\\\"heightMeters\\\":9,\\\"depthMeters\\\":8,\\\"floorCount\\\":3,\\\"roofType\\\":\\\"gable\\\",\\\"roofHeightMeters\\\":2,\\\"confidence\\\":0.91}\"}}]}");

        using var client =
            new HttpClient(handler);

        var provider =
            new MapStudioOpenAiCompatibleProvider(
                client,
                "http://localhost:1234/v1",
                "vision-model",
                "secret",
                isLocal: true);

        var result =
            await provider
                .AnalyzeBuildingReferenceAsync(
                    new MapStudioBuildingReferenceRequest(
                        [
                            new MapStudioAiImageReference(
                                new byte[] { 1, 2, 3 },
                                "image/png",
                                "house.png")
                        ]));

        Assert.Equal(
            12,
            result.WidthMeters);

        Assert.Equal(
            3,
            result.FloorCount);

        Assert.Equal(
            MapStudioBuildingRoofType.Gable,
            result.RoofType);

        Assert.Equal(
            0.91,
            result.Confidence,
            2);

        Assert.Contains(
            "data:image/png;base64,AQID",
            handler.LastRequestBody);

        Assert.Equal(
            "Bearer",
            handler.LastAuthorizationScheme);

        Assert.EndsWith(
            "/v1/chat/completions",
            handler.LastUri);
    }

    [Fact]
    public async Task BuildingAnalysisRequiresImage()
    {
        using var client =
            new HttpClient(
                new FakeHandler(
                    "{}"));

        var provider =
            new MapStudioOpenAiCompatibleProvider(
                client,
                "https://example.test/v1",
                "vision");

        await Assert.ThrowsAsync<
            InvalidDataException>(
                () =>
                    provider
                        .AnalyzeBuildingReferenceAsync(
                            new MapStudioBuildingReferenceRequest(
                                [])));
    }

    [Fact]
    public async Task RoadAnalysisParsesNormalizedCenterlines()
    {
        var handler =
            new FakeHandler(
                "{\"choices\":[{\"message\":{\"content\":\"{\\\"roads\\\":[{\\\"kind\\\":\\\"primary\\\",\\\"laneCount\\\":4,\\\"oneWay\\\":false,\\\"widthMeters\\\":14,\\\"points\\\":[{\\\"x\\\":-0.1,\\\"y\\\":0.25},{\\\"x\\\":1.2,\\\"y\\\":0.75}]}],\\\"notes\\\":\\\"main avenue\\\",\\\"confidence\\\":0.82}\"}}]}");

        using var client =
            new HttpClient(
                handler);

        var provider =
            new MapStudioOpenAiCompatibleProvider(
                client,
                "https://example.test/v1",
                "vision");

        var result =
            await provider
                .AnalyzeRoadReferenceAsync(
                    new MapStudioRoadReferenceRequest(
                        [
                            new MapStudioAiImageReference(
                                new byte[] { 9, 8, 7 },
                                "image/png",
                                "map.png")
                        ]));

        var road =
            Assert.Single(
                result.Roads);

        Assert.Equal(
            "primary",
            road.Kind);

        Assert.Equal(
            4,
            road.LaneCount);

        Assert.False(
            road.OneWay);

        Assert.Equal(
            0,
            road.Points[0].X);

        Assert.Equal(
            1,
            road.Points[1].X);

        Assert.Equal(
            MapStudioRoadReferenceCoordinateSpace
                .NormalizedImage,
            result.CoordinateSpace);

        Assert.Equal(
            0.82,
            result.Confidence,
            2);
    }

    private sealed class FakeHandler(
        string response)
        : HttpMessageHandler
    {
        public string LastRequestBody
        {
            get;
            private set;
        } =
            string.Empty;

        public string? LastAuthorizationScheme
        {
            get;
            private set;
        }

        public string LastUri
        {
            get;
            private set;
        } =
            string.Empty;

        protected override async Task<HttpResponseMessage>
            SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
        {
            LastRequestBody =
                request.Content is null
                    ? string.Empty
                    : await request.Content
                        .ReadAsStringAsync(
                            cancellationToken);

            LastAuthorizationScheme =
                request.Headers
                    .Authorization
                    ?.Scheme;

            LastUri =
                request.RequestUri
                    ?.AbsoluteUri ??
                string.Empty;

            return new HttpResponseMessage(
                HttpStatusCode.OK)
            {
                Content =
                    new StringContent(
                        response,
                        Encoding.UTF8,
                        "application/json")
            };
        }
    }
}
