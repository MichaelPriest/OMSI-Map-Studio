using System.Net;
using System.Text;
using MapStudio.Core.AI;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioAnthropicProviderTests
{
    [Fact]
    public async Task BuildingAnalysisUsesMessagesVisionFormat()
    {
        var handler =
            new FakeHandler(
                "{\"content\":[{\"type\":\"text\",\"text\":\"{\\\"widthMeters\\\":15,\\\"heightMeters\\\":12,\\\"floorCount\\\":4,\\\"roofType\\\":\\\"flat\\\",\\\"confidence\\\":0.93}\"}]}");

        using var client =
            new HttpClient(
                handler);

        var provider =
            new MapStudioAnthropicProvider(
                client,
                "claude-vision",
                "anthropic-secret");

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
                                "image/png")
                        ]));

        Assert.Equal(
            15,
            result.WidthMeters);

        Assert.Equal(
            4,
            result.FloorCount);

        Assert.Contains(
            "\"type\":\"image\"",
            handler.LastRequestBody,
            StringComparison.Ordinal);

        Assert.Contains(
            "\"media_type\":\"image/png\"",
            handler.LastRequestBody,
            StringComparison.Ordinal);

        Assert.Contains(
            "AQID",
            handler.LastRequestBody,
            StringComparison.Ordinal);

        Assert.Equal(
            "anthropic-secret",
            handler.ApiKey);

        Assert.Equal(
            "2023-06-01",
            handler.AnthropicVersion);

        Assert.EndsWith(
            "/v1/messages",
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

        public string? AnthropicVersion
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
                        "x-api-key",
                        out var apiKeys)
                    ? apiKeys.Single()
                    : null;

            AnthropicVersion =
                request.Headers
                    .TryGetValues(
                        "anthropic-version",
                        out var versions)
                    ? versions.Single()
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
