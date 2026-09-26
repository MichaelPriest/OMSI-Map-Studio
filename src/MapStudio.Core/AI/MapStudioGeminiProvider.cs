using System.Text;
using System.Text.Json;

namespace MapStudio.Core.AI;

public sealed class MapStudioGeminiProvider
    : IMapStudioAiProvider
{
    private readonly HttpClient _httpClient;
    private readonly Uri _endpoint;
    private readonly string _token;

    public MapStudioGeminiProvider(
        HttpClient httpClient,
        string model,
        string token,
        string endpoint =
            "https://generativelanguage.googleapis.com/v1beta",
        string id =
            "gemini",
        string displayName =
            "Google Gemini")
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
                uri,
                model.Trim());
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
                    contents =
                        new[]
                        {
                            new
                            {
                                role =
                                    "user",
                                parts =
                                    new object[]
                                    {
                                        new
                                        {
                                            text =
                                                "Reply with OK."
                                        }
                                    }
                            }
                        },
                    generationConfig =
                        new
                        {
                            maxOutputTokens =
                                8
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
        var parts =
            new List<object>
            {
                new
                {
                    text =
                        prompt
                }
            };

        foreach (
            var image in
                images)
        {
            parts.Add(
                new
                {
                    inline_data =
                        new
                        {
                            mime_type =
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
                    contents =
                        new[]
                        {
                            new
                            {
                                role =
                                    "user",
                                parts
                            }
                        },
                    generationConfig =
                        new
                        {
                            responseMimeType =
                                "application/json",
                            maxOutputTokens =
                                2048
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
            "x-goog-api-key",
            _token);

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
                "Gemini returned " +
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
                    "candidates",
                    out var candidates) ||
            candidates.ValueKind !=
                JsonValueKind.Array ||
            candidates.GetArrayLength() ==
                0)
        {
            throw new InvalidDataException(
                "aiGeminiCandidatesMissing");
        }

        var candidate =
            candidates[0];

        if (
            !candidate.TryGetProperty(
                "content",
                out var content) ||
            !content.TryGetProperty(
                "parts",
                out var parts) ||
            parts.ValueKind !=
                JsonValueKind.Array)
        {
            throw new InvalidDataException(
                "aiGeminiContentMissing");
        }

        foreach (
            var part in
                parts.EnumerateArray())
        {
            if (
                part.TryGetProperty(
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
            "aiGeminiTextMissing");
    }

    private static Uri ResolveEndpoint(
        Uri endpoint,
        string model)
    {
        var raw =
            endpoint.AbsoluteUri
                .TrimEnd('/');

        if (
            raw.EndsWith(
                ":generateContent",
                StringComparison
                    .OrdinalIgnoreCase))
        {
            return new Uri(
                raw);
        }

        if (
            raw.Contains(
                "/models/",
                StringComparison
                    .OrdinalIgnoreCase))
        {
            return new Uri(
                raw +
                ":generateContent");
        }

        return new Uri(
            raw +
            "/models/" +
            Uri.EscapeDataString(
                model) +
            ":generateContent");
    }

    private static string Limit(
        string value,
        int maxLength) =>
        value.Length <=
            maxLength
            ? value
            : value[..maxLength];
}
