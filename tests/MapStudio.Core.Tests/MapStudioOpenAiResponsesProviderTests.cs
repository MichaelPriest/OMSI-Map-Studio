using System.Net;
using System.Text;
using MapStudio.Core.AI;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioOpenAiResponsesProviderTests
{
    [Fact]
    public async Task TestConnectionUsesResponsesApiAndBearerKey()
    {
        var handler =
            new FakeHandler(
                "{\"output\":[{\"type\":\"message\",\"content\":[{\"type\":\"output_text\",\"text\":\"OK\"}]}]}");

        using var client =
            new HttpClient(
                handler);

        var provider =
            new MapStudioOpenAiResponsesProvider(
                client,
                "https://api.openai.com/v1",
                "gpt-5.6-terra",
                "secret");

        await provider
            .TestConnectionAsync();

        Assert.Equal(
            "Bearer",
            handler.LastAuthorizationScheme);

        Assert.Equal(
            "secret",
            handler.LastAuthorizationParameter);

        Assert.Equal(
            "https://api.openai.com/v1/responses",
            handler.LastUri);

        Assert.Contains(
            "\"model\":\"gpt-5.6-terra\"",
            handler.LastRequestBody);
    }

    [Fact]
    public async Task BuildingAnalysisUsesResponsesVisionInput()
    {
        var handler =
            new FakeHandler(
                "{\"output\":[{\"type\":\"message\",\"content\":[{\"type\":\"output_text\",\"text\":\"{\\\"widthMeters\\\":12,\\\"heightMeters\\\":8,\\\"depthMeters\\\":9,\\\"floorCount\\\":2,\\\"roofType\\\":\\\"flat\\\",\\\"confidence\\\":0.88}\"}]}]}");

        using var client =
            new HttpClient(
                handler);

        var provider =
            new MapStudioOpenAiResponsesProvider(
                client,
                "https://api.openai.com/v1/responses",
                "gpt-5.6-terra",
                "secret");

        var result =
            await provider
                .AnalyzeBuildingReferenceAsync(
                    new MapStudioBuildingReferenceRequest(
                        [
                            new MapStudioAiImageReference(
                                new byte[]
                                {
                                    1,
                                    2,
                                    3
                                },
                                "image/png",
                                "building.png")
                        ]));

        Assert.Equal(
            12,
            result.WidthMeters);

        Assert.Equal(
            MapStudioBuildingRoofType.Flat,
            result.RoofType);

        Assert.Contains(
            "\"type\":\"input_image\"",
            handler.LastRequestBody);

        Assert.Contains(
            "data:image/png;base64,AQID",
            handler.LastRequestBody);
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

        public string? LastAuthorizationParameter
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

            LastAuthorizationParameter =
                request.Headers
                    .Authorization
                    ?.Parameter;

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
