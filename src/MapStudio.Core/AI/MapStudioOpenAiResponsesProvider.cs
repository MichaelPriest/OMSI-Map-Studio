using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MapStudio.Core.AI;

/// <summary>
/// Native OpenAI Responses API provider used by OMSI Map Studio.
/// Keeps OpenAI-specific request/response handling separate from generic
/// OpenAI-compatible chat/completions servers such as Ollama and LM Studio.
/// </summary>
public sealed class MapStudioOpenAiResponsesProvider
    : IMapStudioAiProvider,
      IMapStudioAssetClassificationProvider
{
    private readonly HttpClient _httpClient;
    private readonly Uri _endpoint;
    private readonly string _model;
    private readonly string _apiKey;

    public MapStudioOpenAiResponsesProvider(
        HttpClient httpClient,
        string endpoint,
        string model,
        string apiKey,
        string id = "openai",
        string displayName = "OpenAI")
    {
        ArgumentNullException.ThrowIfNull(
            httpClient);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            endpoint);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            model);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            apiKey);

        if (
            !Uri.TryCreate(
                endpoint.Trim(),
                UriKind.Absolute,
                out var uri) ||
            uri.Scheme is not
                ("http" or "https"))
        {
            throw new ArgumentException(
                "OpenAI endpoint must be an absolute HTTP(S) URI.",
                nameof(endpoint));
        }

        _httpClient =
            httpClient;

        _endpoint =
            ResolveEndpoint(
                uri);

        _model =
            model.Trim();

        _apiKey =
            apiKey.Trim();

        Descriptor =
            new MapStudioAiProviderDescriptor(
                id,
                displayName,
                MapStudioAiCapability.ImageUnderstanding |
                MapStudioAiCapability.BuildingReferenceAnalysis |
                MapStudioAiCapability.RoadReferenceAnalysis |
                MapStudioAiCapability.SceneReferenceAnalysis |
                MapStudioAiCapability.StructuredOutput |
                MapStudioAiCapability.AssetClassification,
                IsLocal:
                    false);
    }

    public MapStudioAiProviderDescriptor Descriptor
    {
        get;
    }

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
                    input =
                        "Reply only with OK."
                });

        using var response =
            await SendAsync(
                    payload,
                    cancellationToken)
                .ConfigureAwait(false);

        var body =
            await response.Content
                .ReadAsStringAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        EnsureSuccess(
            response,
            body);

        if (
            string.IsNullOrWhiteSpace(
                ExtractOutputText(
                    body)))
        {
            throw new InvalidDataException(
                "openAiResponsesOutputMissing");
        }
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
                    maximum:
                        8,
                    "aiBuildingReferenceImageRequired");

        var result =
            await SendVisionRequestAsync(
                    MapStudioAiStructuredAnalysis
                        .BuildBuildingPrompt(
                            request),
                    images,
                    cancellationToken)
                .ConfigureAwait(false);

        return MapStudioAiStructuredAnalysis
            .ParseBuilding(
                result);
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
                    maximum:
                        4,
                    "aiRoadReferenceImageRequired");

        var result =
            await SendVisionRequestAsync(
                    MapStudioAiStructuredAnalysis
                        .BuildRoadPrompt(
                            request),
                    images,
                    cancellationToken)
                .ConfigureAwait(false);

        return MapStudioAiStructuredAnalysis
            .ParseRoads(
                result);
    }

    public async Task<MapStudioAssetClassificationAnalysis>
        AnalyzeAssetClassificationAsync(
            MapStudioAssetClassificationRequest request,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        var allowedGroups =
            OmsiAssetLibraryClassifier
                .GetGroupsForKind(
                    request.Kind)
                .Where(
                    group =>
                        group !=
                            OmsiAssetLibraryGroup.All)
                .ToArray();

        if (allowedGroups.Length == 0)
        {
            return new MapStudioAssetClassificationAnalysis(
                OmsiAssetLibraryGroup.Other,
                "Outros",
                1,
                "Tipo de asset não usa categorias visuais.");
        }

        var allowed =
            string.Join(
                ", ",
                allowedGroups);

        var prompt =
            "Classify one OMSI Map Studio asset into the editor library. " +
            "Return ONLY one JSON object with fields group, subcategory, confidence, notes. " +
            "group MUST be one of: " +
            allowed +
            ". confidence must be 0..1. " +
            "Use filename/path semantics in Portuguese, English and German. " +
            "Do not invent a group outside the allowed list. " +
            "Asset kind: " +
            request.Kind +
            ". Relative path: " +
            request.RelativePath +
            ". Local heuristic currently says group=" +
            request.HeuristicGroup +
            ", subcategory=" +
            request.HeuristicSubcategory +
            ". Improve the heuristic only when there is enough evidence.";

        var payload =
            JsonSerializer.Serialize(
                new
                {
                    model =
                        _model,
                    input =
                        prompt
                });

        using var response =
            await SendAsync(
                    payload,
                    cancellationToken)
                .ConfigureAwait(false);

        var body =
            await response.Content
                .ReadAsStringAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        EnsureSuccess(
            response,
            body);

        var output =
            ExtractOutputText(
                body);

        if (
            string.IsNullOrWhiteSpace(
                output))
        {
            throw new InvalidDataException(
                "openAiAssetClassificationOutputMissing");
        }

        using var document =
            JsonDocument.Parse(
                MapStudioAiStructuredAnalysis
                    .ExtractJsonObject(
                        output,
                        "openAiAssetClassificationJsonMissing"));

        var root =
            document.RootElement;

        var groupText =
            root.TryGetProperty(
                "group",
                out var groupElement) &&
            groupElement.ValueKind ==
                JsonValueKind.String
                ? groupElement.GetString()
                : null;

        var group =
            Enum.TryParse<
                OmsiAssetLibraryGroup>(
                    groupText,
                    ignoreCase:
                        true,
                    out var parsedGroup) &&
            allowedGroups.Contains(
                parsedGroup)
                ? parsedGroup
                : OmsiAssetLibraryGroup.Other;

        var subcategory =
            root.TryGetProperty(
                "subcategory",
                out var subcategoryElement) &&
            subcategoryElement.ValueKind ==
                JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(
                subcategoryElement.GetString())
                ? subcategoryElement
                    .GetString()!
                    .Trim()
                : OmsiAssetLibraryClassifier
                    .GetDisplayName(
                        group);

        var confidence =
            root.TryGetProperty(
                "confidence",
                out var confidenceElement) &&
            confidenceElement.ValueKind ==
                JsonValueKind.Number &&
            confidenceElement.TryGetDouble(
                out var confidenceValue)
                ? Math.Clamp(
                    confidenceValue,
                    0,
                    1)
                : 0;

        var notes =
            root.TryGetProperty(
                "notes",
                out var notesElement) &&
            notesElement.ValueKind ==
                JsonValueKind.String
                ? notesElement.GetString()
                : null;

        return new MapStudioAssetClassificationAnalysis(
            group,
            subcategory,
            confidence,
            string.IsNullOrWhiteSpace(
                notes)
                ? null
                : notes.Trim());
    }

    private async Task<string> SendVisionRequestAsync(
        string prompt,
        IReadOnlyList<MapStudioAiImageReference> images,
        CancellationToken cancellationToken)
    {
        var content =
            new List<object>
            {
                new
                {
                    type =
                        "input_text",
                    text =
                        prompt
                }
            };

        foreach (
            var image in
                images)
        {
            content.Add(
                new
                {
                    type =
                        "input_image",
                    image_url =
                        "data:" +
                        image.MimeType +
                        ";base64," +
                        Convert.ToBase64String(
                            image.Data.Span),
                    detail =
                        "high"
                });
        }

        var payload =
            JsonSerializer.Serialize(
                new
                {
                    model =
                        _model,
                    input =
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

        using var response =
            await SendAsync(
                    payload,
                    cancellationToken)
                .ConfigureAwait(false);

        var body =
            await response.Content
                .ReadAsStringAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        EnsureSuccess(
            response,
            body);

        var output =
            ExtractOutputText(
                body);

        if (
            string.IsNullOrWhiteSpace(
                output))
        {
            throw new InvalidDataException(
                "openAiResponsesOutputMissing");
        }

        return MapStudioAiStructuredAnalysis
            .ExtractJsonObject(
                output,
                "openAiResponsesJsonMissing");
    }

    private async Task<HttpResponseMessage> SendAsync(
        string payload,
        CancellationToken cancellationToken)
    {
        using var request =
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

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                _apiKey);

        return await _httpClient
            .SendAsync(
                request,
                HttpCompletionOption
                    .ResponseHeadersRead,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static void EnsureSuccess(
        HttpResponseMessage response,
        string body)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        throw new HttpRequestException(
            "OpenAI API returned " +
            (int)response.StatusCode +
            ": " +
            Limit(
                body,
                700));
    }

    private static string? ExtractOutputText(
        string json)
    {
        using var document =
            JsonDocument.Parse(
                json);

        var root =
            document.RootElement;

        if (
            root.TryGetProperty(
                "output_text",
                out var direct) &&
            direct.ValueKind ==
                JsonValueKind.String)
        {
            var directText =
                direct.GetString();

            if (
                !string.IsNullOrWhiteSpace(
                    directText))
            {
                return directText;
            }
        }

        if (
            !root.TryGetProperty(
                "output",
                out var output) ||
            output.ValueKind !=
                JsonValueKind.Array)
        {
            return null;
        }

        var builder =
            new StringBuilder();

        foreach (
            var item in
                output.EnumerateArray())
        {
            if (
                !item.TryGetProperty(
                    "content",
                    out var content) ||
                content.ValueKind !=
                    JsonValueKind.Array)
            {
                continue;
            }

            foreach (
                var part in
                    content.EnumerateArray())
            {
                if (
                    !part.TryGetProperty(
                        "type",
                        out var type) ||
                    type.ValueKind !=
                        JsonValueKind.String ||
                    !string.Equals(
                        type.GetString(),
                        "output_text",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (
                    part.TryGetProperty(
                        "text",
                        out var text) &&
                    text.ValueKind ==
                        JsonValueKind.String)
                {
                    var value =
                        text.GetString();

                    if (
                        !string.IsNullOrWhiteSpace(
                            value))
                    {
                        if (builder.Length > 0)
                        {
                            builder.AppendLine();
                        }

                        builder.Append(
                            value);
                    }
                }
            }
        }

        return builder.Length ==
            0
            ? null
            : builder.ToString();
    }

    private static Uri ResolveEndpoint(
        Uri endpoint)
    {
        var raw =
            endpoint.AbsoluteUri
                .TrimEnd('/');

        if (
            raw.EndsWith(
                "/responses",
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
                "/responses");
        }

        return new Uri(
            raw +
            "/v1/responses");
    }

    private static string Limit(
        string value,
        int maxLength) =>
        value.Length <=
            maxLength
            ? value
            : value[..maxLength];
}
