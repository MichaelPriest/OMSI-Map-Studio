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
