using System.Net;
using System.Text;
using MapStudio.Core.AI;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioGeminiProviderTests
{
    [Fact]
    public async Task RoadAnalysisUsesInlineDataAndParsesJson()
    {
        var handler =
            new FakeHandler(
                "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"{\\\"roads\\\":[{\\\"kind\\\":\\\"avenue\\\",\\\"laneCount\\\":4,\\\"oneWay\\\":false,\\\"points\\\":[{\\\"x\\\":0.1,\\\"y\\\":0.2},{\\\"x\\\":0.9,\\\"y\\\":0.8}]}],\\\"confidence\\\":0.88}\"}]}}]}");

        using var client =
            new HttpClient(
                handler);

        var provider =
            new MapStudioGeminiProvider(
                client,
                "gemini-vision",
                "google-secret");

        var result =
            await provider
                .AnalyzeRoadReferenceAsync(
                    new MapStudioRoadReferenceRequest(
                        [
                            new MapStudioAiImageReference(
                                new byte[]
                                {
                                    9,
                                    8,
                                    7
                                },
                                "image/jpeg")
                        ]));

        var road =
            Assert.Single(
                result.Roads);

        Assert.Equal(
            "avenue",
            road.Kind);

        Assert.Equal(
            4,
            road.LaneCount);

        Assert.Contains(
            "\"inline_data\"",
            handler.LastRequestBody,
            StringComparison.Ordinal);

        Assert.Contains(
            "\"mime_type\":\"image/jpeg\"",
            handler.LastRequestBody,
            StringComparison.Ordinal);

        Assert.Contains(
            "CQgH",
            handler.LastRequestBody,
            StringComparison.Ordinal);

        Assert.Equal(
            "google-secret",
            handler.ApiKey);

        Assert.EndsWith(
            "/v1beta/models/gemini-vision:generateContent",
            handler.LastUri,
            StringComparison.Ordinal);
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

        public string? ApiKey
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

            ApiKey =
                request.Headers
                    .TryGetValues(
                        "x-goog-api-key",
                        out var apiKeys)
                    ? apiKeys.Single()
                    : null;

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
