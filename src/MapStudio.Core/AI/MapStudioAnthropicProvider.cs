using System.Text;
using System.Text.Json;

namespace MapStudio.Core.AI;

public sealed class MapStudioAnthropicProvider
    : IMapStudioAiProvider
{
    private readonly HttpClient _httpClient;
    private readonly Uri _endpoint;
    private readonly string _model;
    private readonly string _token;

    public MapStudioAnthropicProvider(
        HttpClient httpClient,
        string model,
        string token,
        string endpoint =
            "https://api.anthropic.com/v1/messages",
        string id =
            "anthropic",
        string displayName =
            "Anthropic")
    {
        ArgumentNullException.ThrowIfNull(
            httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            model);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            token);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            endpoint);

        if (
            !Uri.TryCreate(
                endpoint.Trim(),
                UriKind.Absolute,
                out var uri) ||
            uri.Scheme is not
                (
                    "http" or
                    "https"
                ))
        {
            throw new ArgumentException(
                "AI endpoint must be an absolute HTTP(S) URI.",
                nameof(endpoint));
        }

        _httpClient =
            httpClient;
        _endpoint =
            ResolveEndpoint(
                uri);
        _model =
            model.Trim();
        _token =
            token.Trim();

        Descriptor =
            new MapStudioAiProviderDescriptor(
                id,
                displayName,
                MapStudioAiCapability
                    .ImageUnderstanding |
                MapStudioAiCapability
                    .BuildingReferenceAnalysis |
                MapStudioAiCapability
                    .RoadReferenceAnalysis |
                MapStudioAiCapability
                    .StructuredOutput);
    }

    public MapStudioAiProviderDescriptor
        Descriptor { get; }

    public async Task TestConnectionAsync(
        CancellationToken cancellationToken =
            default)
    {
        var payload =
            JsonSerializer.Serialize(
                new
                {
                    model =
                        _model,
                    max_tokens =
                        8,
                    messages =
                        new[]
                        {
                            new
                            {
                                role =
                                    "user",
                                content =
                                    "Reply with OK."
                            }
                        }
                });

        await SendAsync(
                payload,
                cancellationToken,
                requireText:
                    false)
            .ConfigureAwait(false);
    }

    public async Task<MapStudioBuildingReferenceAnalysis>
        AnalyzeBuildingReferenceAsync(
            MapStudioBuildingReferenceRequest request,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        var images =
            MapStudioAiStructuredAnalysis
                .GetUsableImages(
                    request.Images,
                    8,
                    "aiBuildingReferenceImageRequired");

        var text =
            await SendVisionAsync(
                    MapStudioAiStructuredAnalysis
                        .BuildBuildingPrompt(
                            request),
                    images,
                    cancellationToken)
                .ConfigureAwait(false);

        return MapStudioAiStructuredAnalysis
            .ParseBuilding(
                text);
    }

    public async Task<MapStudioRoadReferenceAnalysis>
        AnalyzeRoadReferenceAsync(
            MapStudioRoadReferenceRequest request,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        var images =
            MapStudioAiStructuredAnalysis
                .GetUsableImages(
                    request.Images,
                    4,
                    "aiRoadReferenceImageRequired");

        var text =
            await SendVisionAsync(
                    MapStudioAiStructuredAnalysis
                        .BuildRoadPrompt(
                            request),
                    images,
                    cancellationToken)
                .ConfigureAwait(false);

        return MapStudioAiStructuredAnalysis
            .ParseRoads(
                text);
    }

    private async Task<string> SendVisionAsync(
        string prompt,
        IReadOnlyList<
            MapStudioAiImageReference>
            images,
        CancellationToken cancellationToken)
    {
        var content =
            new List<object>();

        content.Add(
            new
            {
                type =
                    "text",
                text =
                    prompt
            });

        foreach (
            var image in
                images)
        {
            content.Add(
                new
                {
                    type =
                        "image",
                    source =
                        new
                        {
                            type =
                                "base64",
                            media_type =
                                image.MimeType,
                            data =
                                Convert
                                    .ToBase64String(
                                        image.Data
                                            .Span)
                        }
                });
        }

        var payload =
            JsonSerializer.Serialize(
                new
                {
                    model =
                        _model,
                    max_tokens =
                        2048,
                    messages =
                        new[]
                        {
                            new
                            {
                                role =
                                    "user",
                                content
                            }
                        }
                });

        return await SendAsync(
                payload,
                cancellationToken,
                requireText:
                    true)
            .ConfigureAwait(false);
    }

    private async Task<string> SendAsync(
        string payload,
        CancellationToken cancellationToken,
        bool requireText)
    {
        using var message =
            new HttpRequestMessage(
                HttpMethod.Post,
                _endpoint)
            {
                Content =
                    new StringContent(
                        payload,
                        Encoding.UTF8,
                        "application/json")
            };

        message.Headers.TryAddWithoutValidation(
            "x-api-key",
            _token);

        message.Headers.TryAddWithoutValidation(
            "anthropic-version",
            "2023-06-01");

        using var response =
            await _httpClient
                .SendAsync(
                    message,
                    HttpCompletionOption
                        .ResponseHeadersRead,
                    cancellationToken)
                .ConfigureAwait(false);

        var body =
            await response.Content
                .ReadAsStringAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        if (
            !response
                .IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                "Anthropic returned " +
                (int)response.StatusCode +
                ": " +
                Limit(
                    body,
                    500));
        }

        if (!requireText)
        {
            return string.Empty;
        }

        using var document =
            JsonDocument.Parse(
                body);

        if (
            !document.RootElement
                .TryGetProperty(
                    "content",
                    out var content) ||
            content.ValueKind !=
                JsonValueKind.Array)
        {
            throw new InvalidDataException(
                "aiAnthropicContentMissing");
        }

        foreach (
            var block in
                content.EnumerateArray())
        {
            if (
                block.TryGetProperty(
                    "type",
                    out var type) &&
                string.Equals(
                    type.GetString(),
                    "text",
                    StringComparison.OrdinalIgnoreCase) &&
                block.TryGetProperty(
                    "text",
                    out var text))
            {
                var value =
                    text.GetString();

                if (
                    !string.IsNullOrWhiteSpace(
                        value))
                {
                    return value;
                }
            }
        }

        throw new InvalidDataException(
            "aiAnthropicTextMissing");
    }

    private static Uri ResolveEndpoint(
        Uri endpoint)
    {
        var raw =
            endpoint.AbsoluteUri
                .TrimEnd('/');

        if (
            raw.EndsWith(
                "/messages",
                StringComparison
                    .OrdinalIgnoreCase))
        {
            return new Uri(
                raw);
        }

        if (
            raw.EndsWith(
                "/v1",
                StringComparison
                    .OrdinalIgnoreCase))
        {
            return new Uri(
                raw +
                "/messages");
        }

        return new Uri(
            raw +
            "/v1/messages");
    }

    private static string Limit(
        string value,
        int maxLength) =>
        value.Length <=
            maxLength
            ? value
            : value[..maxLength];
}
