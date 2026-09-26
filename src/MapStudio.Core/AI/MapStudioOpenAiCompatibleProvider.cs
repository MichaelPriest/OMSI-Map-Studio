using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MapStudio.Core.AI;

public sealed class MapStudioOpenAiCompatibleProvider
    : IMapStudioAiProvider
{
    private readonly HttpClient _httpClient;
    private readonly Uri _endpoint;
    private readonly string _model;
    private readonly string? _token;

    public MapStudioOpenAiCompatibleProvider(
        HttpClient httpClient,
        string endpoint,
        string model,
        string? token = null,
        string id = "openai-compatible",
        string displayName = "OpenAI-compatible",
        bool isLocal = false)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        if (!Uri.TryCreate(endpoint.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException(
                "AI endpoint must be an absolute HTTP(S) URI.",
                nameof(endpoint));
        }

        _httpClient = httpClient;
        _endpoint = ResolveEndpoint(uri);
        _model = model.Trim();
        _token = string.IsNullOrWhiteSpace(token) ? null : token.Trim();

        Descriptor = new MapStudioAiProviderDescriptor(
            id,
            displayName,
            MapStudioAiCapability.ImageUnderstanding |
            MapStudioAiCapability.BuildingReferenceAnalysis |
            MapStudioAiCapability.RoadReferenceAnalysis |
            MapStudioAiCapability.StructuredOutput,
            isLocal);
    }

    public MapStudioAiProviderDescriptor Descriptor { get; }

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
                        },
                    max_tokens =
                        4
                });

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

        if (_token is not null)
        {
            message.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    _token);
        }

        using var response =
            await _httpClient
                .SendAsync(
                    message,
                    HttpCompletionOption
                        .ResponseHeadersRead,
                    cancellationToken)
                .ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body =
            await response.Content
                .ReadAsStringAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        throw new HttpRequestException(
            "AI provider returned " +
            (int)response.StatusCode +
            ": " +
            Limit(
                body,
                500));
    }

    public async Task<MapStudioBuildingReferenceAnalysis>
        AnalyzeBuildingReferenceAsync(
            MapStudioBuildingReferenceRequest request,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var images = (request.Images ??
                Array.Empty<MapStudioAiImageReference>())
            .Where(image => image.IsUsable)
            .Take(8)
            .ToArray();

        if (images.Length == 0)
        {
            throw new InvalidDataException(
                "aiBuildingReferenceImageRequired");
        }

        var prompt =
            "Analyze the building image. Return ONLY one JSON object. " +
            "Use meters. Fields: widthMeters, heightMeters, depthMeters, " +
            "floorCount, roofType, roofHeightMeters, windowsPerFloor, " +
            "doorCount, typicalWindowWidthMeters, typicalWindowHeightMeters, " +
            "facadeMaterial, roofMaterial, architecturalStyle, notes, confidence. " +
            "roofType: unknown|flat|gable|hip|shed|mansard|dome|custom. " +
            "confidence must be 0..1.";

        if (!string.IsNullOrWhiteSpace(request.UserNotes))
        {
            prompt += " User notes: " + request.UserNotes.Trim();
        }

        var analysisJson =
            await SendVisionRequestAsync(
                prompt,
                images,
                cancellationToken)
            .ConfigureAwait(false);

        return MapStudioAiStructuredAnalysis
            .ParseBuilding(
                analysisJson);
    }

    public async Task<MapStudioRoadReferenceAnalysis>
        AnalyzeRoadReferenceAsync(
            MapStudioRoadReferenceRequest request,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var images =
            (request.Images ??
                Array.Empty<MapStudioAiImageReference>())
            .Where(image => image.IsUsable)
            .Take(4)
            .ToArray();

        if (images.Length == 0)
        {
            throw new InvalidDataException(
                "aiRoadReferenceImageRequired");
        }

        var prompt =
            "Analyze the map/reference image and trace visible road centerlines. " +
            "Return ONLY one JSON object. Coordinates must be normalized image coordinates: " +
            "x=0 left, x=1 right, y=0 top, y=1 bottom. " +
            "Schema: roads array; each road has kind, laneCount, oneWay, widthMeters, " +
            "and points array with x,y. Include at least two points per road. " +
            "Also return notes and confidence from 0 to 1.";

        if (!string.IsNullOrWhiteSpace(request.UserNotes))
        {
            prompt += " User notes: " + request.UserNotes.Trim();
        }

        var json =
            await SendVisionRequestAsync(
                prompt,
                images,
                cancellationToken)
            .ConfigureAwait(false);

        return MapStudioAiStructuredAnalysis
            .ParseRoads(
                json);
    }

    private async Task<string>
        SendVisionRequestAsync(
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
                    type =
                        "text",
                    text =
                        prompt
                }
            };

        foreach (var image in images)
        {
            parts.Add(
                new
                {
                    type =
                        "image_url",
                    image_url =
                        new
                        {
                            url =
                                "data:" +
                                image.MimeType +
                                ";base64," +
                                Convert.ToBase64String(
                                    image.Data.Span),
                            detail =
                                "high"
                        }
                });
        }

        var payload =
            JsonSerializer.Serialize(
                new
                {
                    model =
                        _model,
                    messages =
                        new[]
                        {
                            new
                            {
                                role =
                                    "user",
                                content =
                                    parts
                            }
                        }
                });

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

        if (_token is not null)
        {
            message.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    _token);
        }

        using var response =
            await _httpClient
                .SendAsync(
                    message,
                    HttpCompletionOption
                        .ResponseHeadersRead,
                    cancellationToken)
                .ConfigureAwait(false);

        var responseBody =
            await response.Content
                .ReadAsStringAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                "AI provider returned " +
                (int)response.StatusCode +
                ": " +
                Limit(
                    responseBody,
                    500));
        }

        using var responseDocument =
            JsonDocument.Parse(
                responseBody);

        var choices =
            responseDocument.RootElement
                .GetProperty(
                    "choices");

        if (choices.GetArrayLength() == 0)
        {
            throw new InvalidDataException(
                "aiCompatibleChoicesMissing");
        }

        var text =
            choices[0]
                .GetProperty(
                    "message")
                .GetProperty(
                    "content")
                .GetString();

        if (
            string.IsNullOrWhiteSpace(
                text))
        {
            throw new InvalidDataException(
                "aiCompatibleContentMissing");
        }

        var start =
            text.IndexOf(
                '{');

        var end =
            text.LastIndexOf(
                '}');

        if (
            start < 0 ||
            end <= start)
        {
            throw new InvalidDataException(
                "aiCompatibleJsonMissing");
        }

        return text[
            start..(end + 1)];
    }

    private static MapStudioBuildingOpeningEstimate?
        BuildOpenings(JsonElement root)
    {
        var windows = GetInt(root, "windowsPerFloor");
        var doors = GetInt(root, "doorCount");
        var width = GetDouble(root, "typicalWindowWidthMeters");
        var height = GetDouble(root, "typicalWindowHeightMeters");

        if (windows is null &&
            doors is null &&
            width is null &&
            height is null)
        {
            return null;
        }

        return new MapStudioBuildingOpeningEstimate(
            Math.Max(0, windows ?? 0),
            Math.Max(0, doors ?? 0),
            width,
            height);
    }

    private static Uri ResolveEndpoint(Uri endpoint)
    {
        var raw = endpoint.AbsoluteUri.TrimEnd('/');

        if (raw.EndsWith(
                "/chat/completions",
                StringComparison.OrdinalIgnoreCase))
        {
            return new Uri(raw);
        }

        if (raw.EndsWith(
                "/v1",
                StringComparison.OrdinalIgnoreCase))
        {
            return new Uri(raw + "/chat/completions");
        }

        return new Uri(raw + "/v1/chat/completions");
    }

    private static double? GetDouble(
        JsonElement root,
        string name) =>
        root.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetDouble(out var number)
            ? number
            : null;

    private static int? GetInt(
        JsonElement root,
        string name) =>
        root.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out var number)
            ? number
            : null;

    private static string? GetString(
        JsonElement root,
        string name)
    {
        if (!root.TryGetProperty(name, out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString();

        return string.IsNullOrWhiteSpace(text)
            ? null
            : text.Trim();
    }

    private static MapStudioBuildingRoofType ParseRoofType(
        string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "flat" => MapStudioBuildingRoofType.Flat,
            "gable" => MapStudioBuildingRoofType.Gable,
            "hip" => MapStudioBuildingRoofType.Hip,
            "shed" => MapStudioBuildingRoofType.Shed,
            "mansard" => MapStudioBuildingRoofType.Mansard,
            "dome" => MapStudioBuildingRoofType.Dome,
            "custom" => MapStudioBuildingRoofType.Custom,
            _ => MapStudioBuildingRoofType.Unknown
        };

    private static string Limit(
        string value,
        int maxLength) =>
        value.Length <= maxLength
            ? value
            : value[..maxLength];
}
