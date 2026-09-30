using System.Globalization;
using System.Text.Json;

namespace MapStudio.Core.Generation.Scene;

public sealed record MapStudioOpenAerialMapImage(
    string Id,
    string? Title,
    double West,
    double South,
    double East,
    double North,
    double? GroundSampleDistanceMeters,
    DateTimeOffset? AcquisitionTime,
    string? Platform,
    string? Provider,
    string License,
    string TmsTemplate,
    string? ThumbnailUrl,
    string? SourceUrl)
{
    public bool Contains(
        double latitude,
        double longitude) =>
        double.IsFinite(latitude) &&
        double.IsFinite(longitude) &&
        latitude >= South &&
        latitude <= North &&
        longitude >= West &&
        longitude <= East;

    public bool Intersects(
        double south,
        double west,
        double north,
        double east) =>
        south <= North &&
        north >= South &&
        west <= East &&
        east >= West;

    public bool IsAutomaticDerivationAllowed =>
        MapStudioOpenAerialMapCatalogClient
            .IsAutomaticDerivationLicense(
                License);
}

public sealed record MapStudioOpenAerialMapCatalogPage(
    IReadOnlyList<MapStudioOpenAerialMapImage> Images,
    int Page,
    int Limit,
    int Found);

public sealed class MapStudioOpenAerialMapCatalogClient
{
    public const string DefaultEndpoint =
        "https://api.openaerialmap.org/meta";

    private const int PageSize =
        100;

    private const int MaximumPages =
        5;

    private readonly HttpClient
        _httpClient;

    private readonly Uri
        _endpoint;

    public MapStudioOpenAerialMapCatalogClient()
        : this(
            new HttpClient
            {
                Timeout =
                    TimeSpan.FromSeconds(
                        30)
            })
    {
    }

    public MapStudioOpenAerialMapCatalogClient(
        HttpClient httpClient,
        string endpoint = DefaultEndpoint)
    {
        ArgumentNullException.ThrowIfNull(
            httpClient);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            endpoint);

        if (
            !Uri.TryCreate(
                endpoint,
                UriKind.Absolute,
                out var uri) ||
            uri.Scheme is not
                ("http" or "https"))
        {
            throw new ArgumentException(
                "OpenAerialMap endpoint must be an absolute HTTP(S) URI.",
                nameof(endpoint));
        }

        _httpClient =
            httpClient;

        _endpoint =
            uri;
    }

    public async Task<IReadOnlyList<
        MapStudioOpenAerialMapImage>>
        SearchAsync(
            double south,
            double west,
            double north,
            double east,
            CancellationToken cancellationToken =
                default)
    {
        ValidateBounds(
            south,
            west,
            north,
            east);

        var results =
            new List<
                MapStudioOpenAerialMapImage>();

        for (
            var page = 1;
            page <= MaximumPages;
            page++)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            var uri =
                BuildSearchUri(
                    south,
                    west,
                    north,
                    east,
                    page);

            using var response =
                await _httpClient
                    .GetAsync(
                        uri,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"openAerialMapCatalogHttp:{(int)response.StatusCode}");
            }

            var json =
                await response.Content
                    .ReadAsStringAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

            var parsed =
                ParseResponse(
                    json);

            results.AddRange(
                parsed.Images
                    .Where(
                        image =>
                            image.Intersects(
                                south,
                                west,
                                north,
                                east)));

            if (
                parsed.Images.Count <
                    Math.Max(
                        1,
                        parsed.Limit) ||
                parsed.Page *
                    Math.Max(
                        1,
                        parsed.Limit) >=
                    parsed.Found)
            {
                break;
            }
        }

        return results
            .Where(
                image =>
                    image.IsAutomaticDerivationAllowed &&
                    !string.IsNullOrWhiteSpace(
                        image.TmsTemplate))
            .GroupBy(
                image =>
                    image.Id,
                StringComparer.OrdinalIgnoreCase)
            .Select(
                group =>
                    group.First())
            .OrderBy(
                image =>
                    image.GroundSampleDistanceMeters ??
                    double.MaxValue)
            .ThenByDescending(
                image =>
                    image.AcquisitionTime ??
                    DateTimeOffset.MinValue)
            .ToArray();
    }

    public static MapStudioOpenAerialMapImage?
        SelectBestForPoint(
            IReadOnlyList<
                MapStudioOpenAerialMapImage>
                images,
            double latitude,
            double longitude)
    {
        ArgumentNullException.ThrowIfNull(
            images);

        return images
            .Where(
                image =>
                    image.IsAutomaticDerivationAllowed &&
                    image.Contains(
                        latitude,
                        longitude))
            .OrderBy(
                image =>
                    image.GroundSampleDistanceMeters ??
                    double.MaxValue)
            .ThenByDescending(
                image =>
                    image.AcquisitionTime ??
                    DateTimeOffset.MinValue)
            .FirstOrDefault();
    }

    public static bool IsAutomaticDerivationLicense(
        string? license)
    {
        if (string.IsNullOrWhiteSpace(
                license))
        {
            return false;
        }

        var normalized =
            new string(
                license
                    .Trim()
                    .ToUpperInvariant()
                    .Where(
                        character =>
                            char.IsLetterOrDigit(
                                character))
                    .ToArray());

        return normalized ==
            "CCBY40";
    }

    public static MapStudioOpenAerialMapCatalogPage
        ParseResponse(
            string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            json);

        using var document =
            JsonDocument.Parse(
                json);

        var root =
            document.RootElement;

        var page =
            1;

        var limit =
            PageSize;

        var found =
            0;

        if (
            root.TryGetProperty(
                "meta",
                out var meta) &&
            meta.ValueKind ==
                JsonValueKind.Object)
        {
            page =
                ReadInt(
                    meta,
                    "page") ??
                page;

            limit =
                ReadInt(
                    meta,
                    "limit") ??
                limit;

            found =
                ReadInt(
                    meta,
                    "found") ??
                found;
        }

        var images =
            new List<
                MapStudioOpenAerialMapImage>();

        if (
            root.TryGetProperty(
                "results",
                out var resultArray) &&
            resultArray.ValueKind ==
                JsonValueKind.Array)
        {
            foreach (
                var item in
                    resultArray.EnumerateArray())
            {
                var parsed =
                    TryParseImage(
                        item);

                if (parsed is not null)
                {
                    images.Add(
                        parsed);
                }
            }
        }

        if (found <= 0)
        {
            found =
                images.Count;
        }

        return new MapStudioOpenAerialMapCatalogPage(
            images,
            Math.Max(
                1,
                page),
            Math.Max(
                1,
                limit),
            Math.Max(
                images.Count,
                found));
    }

    private Uri BuildSearchUri(
        double south,
        double west,
        double north,
        double east,
        int page)
    {
        var invariant =
            CultureInfo.InvariantCulture;

        var bbox =
            string.Join(
                ",",
                west.ToString(
                    "G17",
                    invariant),
                south.ToString(
                    "G17",
                    invariant),
                east.ToString(
                    "G17",
                    invariant),
                north.ToString(
                    "G17",
                    invariant));

        var separator =
            string.IsNullOrEmpty(
                _endpoint.Query)
                ? "?"
                : "&";

        return new Uri(
            _endpoint +
            separator +
            "bbox=" +
            Uri.EscapeDataString(
                bbox) +
            "&has_tiled=true" +
            "&limit=" +
            PageSize.ToString(
                invariant) +
            "&page=" +
            page.ToString(
                invariant));
    }

    private static MapStudioOpenAerialMapImage?
        TryParseImage(
            JsonElement item)
    {
        if (
            item.ValueKind !=
                JsonValueKind.Object ||
            !TryReadBounds(
                item,
                out var west,
                out var south,
                out var east,
                out var north))
        {
            return null;
        }

        var properties =
            item.TryGetProperty(
                "properties",
                out var propertyElement) &&
            propertyElement.ValueKind ==
                JsonValueKind.Object
                ? propertyElement
                : default;

        var license =
            ReadString(
                properties,
                "license") ??
            ReadString(
                item,
                "license");

        var tms =
            ReadString(
                properties,
                "tms") ??
            ReadString(
                item,
                "tms");

        if (
            string.IsNullOrWhiteSpace(
                license) ||
            string.IsNullOrWhiteSpace(
                tms))
        {
            return null;
        }

        if (
            !Uri.TryCreate(
                tms,
                UriKind.Absolute,
                out var tmsUri) ||
            tmsUri.Scheme is not
                ("http" or "https") ||
            !tms.Contains(
                "{z}",
                StringComparison.OrdinalIgnoreCase) ||
            !tms.Contains(
                "{x}",
                StringComparison.OrdinalIgnoreCase) ||
            !tms.Contains(
                "{y}",
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var id =
            ReadString(
                item,
                "uuid") ??
            ReadString(
                item,
                "_id") ??
            tms;

        var gsd =
            ReadDouble(
                item,
                "gsd") ??
            ReadDouble(
                properties,
                "resolution_in_meters");

        if (
            gsd is not null &&
            (
                !double.IsFinite(
                    gsd.Value) ||
                gsd.Value <=
                    0
            ))
        {
            gsd =
                null;
        }

        return new MapStudioOpenAerialMapImage(
            id,
            ReadString(
                item,
                "title"),
            west,
            south,
            east,
            north,
            gsd,
            ReadDate(
                item,
                "acquisition_end") ??
            ReadDate(
                item,
                "acquisition_start"),
            ReadString(
                item,
                "platform"),
            ReadString(
                item,
                "provider"),
            license.Trim(),
            tms.Trim(),
            ReadString(
                properties,
                "thumbnail") ??
            ReadString(
                item,
                "thumbnail"),
            ReadString(
                properties,
                "url") ??
            ReadString(
                item,
                "meta_uri"));
    }

    private static bool TryReadBounds(
        JsonElement item,
        out double west,
        out double south,
        out double east,
        out double north)
    {
        west =
            south =
            east =
            north =
                0;

        if (
            !item.TryGetProperty(
                "bbox",
                out var bbox) ||
            bbox.ValueKind !=
                JsonValueKind.Array ||
            bbox.GetArrayLength() <
                4)
        {
            return false;
        }

        var values =
            bbox
                .EnumerateArray()
                .Take(4)
                .Select(
                    ReadFlexibleDouble)
                .ToArray();

        if (
            values.Any(
                value =>
                    value is null))
        {
            return false;
        }

        west =
            Math.Min(
                values[0]!.Value,
                values[2]!.Value);

        east =
            Math.Max(
                values[0]!.Value,
                values[2]!.Value);

        south =
            Math.Min(
                values[1]!.Value,
                values[3]!.Value);

        north =
            Math.Max(
                values[1]!.Value,
                values[3]!.Value);

        return
            west >=
                -180 &&
            east <=
                180 &&
            south >=
                -90 &&
            north <=
                90 &&
            west <
                east &&
            south <
                north;
    }

    private static string? ReadString(
        JsonElement element,
        string name)
    {
        if (
            element.ValueKind !=
                JsonValueKind.Object ||
            !element.TryGetProperty(
                name,
                out var value))
        {
            return null;
        }

        if (
            value.ValueKind ==
                JsonValueKind.String)
        {
            var text =
                value.GetString();

            return string.IsNullOrWhiteSpace(
                    text)
                ? null
                : text.Trim();
        }

        if (
            value.ValueKind ==
                JsonValueKind.Object &&
            value.TryGetProperty(
                "$oid",
                out var oid) &&
            oid.ValueKind ==
                JsonValueKind.String)
        {
            return oid.GetString();
        }

        return null;
    }

    private static int? ReadInt(
        JsonElement element,
        string name)
    {
        if (
            element.ValueKind !=
                JsonValueKind.Object ||
            !element.TryGetProperty(
                name,
                out var value))
        {
            return null;
        }

        if (
            value.ValueKind ==
                JsonValueKind.Number &&
            value.TryGetInt32(
                out var direct))
        {
            return direct;
        }

        if (
            value.ValueKind ==
                JsonValueKind.String &&
            int.TryParse(
                value.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static double? ReadDouble(
        JsonElement element,
        string name)
    {
        if (
            element.ValueKind !=
                JsonValueKind.Object ||
            !element.TryGetProperty(
                name,
                out var value))
        {
            return null;
        }

        return ReadFlexibleDouble(
            value);
    }

    private static double? ReadFlexibleDouble(
        JsonElement value)
    {
        if (
            value.ValueKind ==
                JsonValueKind.Number &&
            value.TryGetDouble(
                out var direct))
        {
            return direct;
        }

        if (
            value.ValueKind ==
                JsonValueKind.String &&
            double.TryParse(
                value.GetString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var text))
        {
            return text;
        }

        if (
            value.ValueKind ==
                JsonValueKind.Object)
        {
            foreach (
                var key in
                    new[]
                    {
                        "$numberDouble",
                        "$numberInt",
                        "$numberLong"
                    })
            {
                if (
                    value.TryGetProperty(
                        key,
                        out var wrapped))
                {
                    return ReadFlexibleDouble(
                        wrapped);
                }
            }
        }

        return null;
    }

    private static DateTimeOffset? ReadDate(
        JsonElement element,
        string name)
    {
        if (
            element.ValueKind !=
                JsonValueKind.Object ||
            !element.TryGetProperty(
                name,
                out var value))
        {
            return null;
        }

        if (
            value.ValueKind ==
                JsonValueKind.String &&
            DateTimeOffset.TryParse(
                value.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var parsed))
        {
            return parsed;
        }

        if (
            value.ValueKind ==
                JsonValueKind.Object &&
            value.TryGetProperty(
                "$date",
                out var wrapped))
        {
            var milliseconds =
                ReadFlexibleDouble(
                    wrapped);

            if (
                milliseconds is not null &&
                milliseconds.Value >=
                    0 &&
                milliseconds.Value <=
                    253402300799999)
            {
                try
                {
                    return DateTimeOffset
                        .FromUnixTimeMilliseconds(
                            checked(
                                (long)Math.Round(
                                    milliseconds.Value)));
                }
                catch
                {
                    return null;
                }
            }
        }

        return null;
    }

    private static void ValidateBounds(
        double south,
        double west,
        double north,
        double east)
    {
        if (
            !double.IsFinite(
                south) ||
            !double.IsFinite(
                west) ||
            !double.IsFinite(
                north) ||
            !double.IsFinite(
                east) ||
            south is <
                -90 or >
                90 ||
            north is <
                -90 or >
                90 ||
            west is <
                -180 or >
                180 ||
            east is <
                -180 or >
                180 ||
            south >=
                north ||
            west >=
                east)
        {
            throw new InvalidDataException(
                "invalidOpenAerialMapBounds");
        }
    }
}
