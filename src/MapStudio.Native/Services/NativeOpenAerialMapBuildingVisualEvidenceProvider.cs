using System.Globalization;
using MapStudio.Core.AI;
using MapStudio.Core.Generation.Buildings;
using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Scene;

namespace MapStudio.Native.Services;

public sealed class NativeOpenAerialMapBuildingVisualEvidenceProvider :
    IMapStudioBuildingVisualEvidenceProvider
{
    private const int MaximumBuildingsPerRun =
        48;

    private const double MaximumUsefulGsdMeters =
        0.75;

    private const long MaximumTileBytes =
        8L *
        1024L *
        1024L;

    private static readonly HttpClient
        HttpClient =
            new()
            {
                Timeout =
                    TimeSpan.FromSeconds(
                        30)
            };

    private readonly IMapStudioAiProvider
        _provider;

    private readonly MapStudioOpenAerialMapCatalogClient
        _catalog;

    public NativeOpenAerialMapBuildingVisualEvidenceProvider(
        IMapStudioAiProvider provider)
        : this(
            provider,
            new MapStudioOpenAerialMapCatalogClient(
                HttpClient))
    {
    }

    public NativeOpenAerialMapBuildingVisualEvidenceProvider(
        IMapStudioAiProvider provider,
        MapStudioOpenAerialMapCatalogClient catalog)
    {
        ArgumentNullException.ThrowIfNull(
            provider);

        ArgumentNullException.ThrowIfNull(
            catalog);

        _provider =
            provider;

        _catalog =
            catalog;
    }

    public async Task<IReadOnlyDictionary<
        string,
        MapStudioBuildingVisualEvidence>>
        AnalyzeAsync(
            IReadOnlyList<
                MapStudioProjectedBuildingFootprint>
                buildings,
            MapStudioGeographicAnchor anchor,
            IProgress<MapStudioBuildingVisualEvidenceProgress>?
                progress = null,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentNullException.ThrowIfNull(
            buildings);

        if (buildings.Count == 0)
        {
            return new Dictionary<
                string,
                MapStudioBuildingVisualEvidence>(
                    StringComparer.Ordinal);
        }

        var targets =
            buildings
                .Select(
                    building =>
                        (
                            Building:
                                building,
                            Geographic:
                                MapStudioGeographicProjection
                                    .Unproject(
                                        anchor,
                                        building.Center)
                        ))
                .ToArray();

        const double paddingDegrees =
            0.00020;

        var south =
            Math.Max(
                -90,
                targets.Min(
                    item =>
                        item.Geographic.Latitude) -
                paddingDegrees);

        var north =
            Math.Min(
                90,
                targets.Max(
                    item =>
                        item.Geographic.Latitude) +
                paddingDegrees);

        var west =
            Math.Max(
                -180,
                targets.Min(
                    item =>
                        item.Geographic.Longitude) -
                paddingDegrees);

        var east =
            Math.Min(
                180,
                targets.Max(
                    item =>
                        item.Geographic.Longitude) +
                paddingDegrees);

        if (
            south >= north ||
            west >= east)
        {
            return new Dictionary<
                string,
                MapStudioBuildingVisualEvidence>(
                    StringComparer.Ordinal);
        }

        IReadOnlyList<
            MapStudioOpenAerialMapImage>
            imagery;

        progress?.Report(
            new MapStudioBuildingVisualEvidenceProgress(
                0,
                Math.Min(
                    buildings.Count,
                    MaximumBuildingsPerRun),
                "OpenAerialMap: procurando ortofotos CC BY 4.0 para os prédios em revisão..."));

        try
        {
            imagery =
                await _catalog
                    .SearchAsync(
                        south,
                        west,
                        north,
                        east,
                        cancellationToken)
                    .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new Dictionary<
                string,
                MapStudioBuildingVisualEvidence>(
                    StringComparer.Ordinal);
        }

        if (imagery.Count == 0)
        {
            return new Dictionary<
                string,
                MapStudioBuildingVisualEvidence>(
                    StringComparer.Ordinal);
        }

        var results =
            new Dictionary<
                string,
                MapStudioBuildingVisualEvidence>(
                    StringComparer.Ordinal);

        var tileCache =
            new Dictionary<
                string,
                MapStudioAiImageReference>(
                    StringComparer.Ordinal);

        var attemptedCount =
            0;

        foreach (var target in targets)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            if (
                attemptedCount >=
                MaximumBuildingsPerRun)
            {
                break;
            }

            var image =
                MapStudioOpenAerialMapCatalogClient
                    .SelectBestForPoint(
                        imagery,
                        target.Geographic.Latitude,
                        target.Geographic.Longitude);

            if (
                image is null ||
                image.GroundSampleDistanceMeters is
                    > MaximumUsefulGsdMeters)
            {
                continue;
            }

            attemptedCount++;

            progress?.Report(
                new MapStudioBuildingVisualEvidenceProgress(
                    attemptedCount - 1,
                    Math.Min(
                        buildings.Count,
                        MaximumBuildingsPerRun),
                    $"OpenAerialMap + IA: {attemptedCount}/{Math.Min(buildings.Count, MaximumBuildingsPerRun)} · {target.Building.Id}"));

            var zoom =
                ResolveZoom(
                    target.Geographic.Latitude,
                    image.GroundSampleDistanceMeters);

            var tiles =
                ResolveContextTiles(
                    target.Geographic.Latitude,
                    target.Geographic.Longitude,
                    zoom);

            var references =
                new List<
                    MapStudioAiImageReference>(
                        tiles.Count);

            foreach (
                var tile in tiles)
            {
                var cacheKey =
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{image.Id}|{zoom}|{tile.X}|{tile.Y}");

                if (
                    !tileCache.TryGetValue(
                        cacheKey,
                        out var reference))
                {
                    reference =
                        await DownloadTileAsync(
                                image,
                                zoom,
                                tile.X,
                                tile.Y,
                                cancellationToken)
                            .ConfigureAwait(false);

                    if (reference is null)
                    {
                        continue;
                    }

                    tileCache[
                        cacheKey] =
                        reference;
                }

                references.Add(
                    reference);
            }

            if (references.Count == 0)
            {
                continue;
            }

            MapStudioBuildingReferenceAnalysis
                analysis;

            try
            {
                analysis =
                    await _provider
                        .AnalyzeBuildingReferenceAsync(
                            new MapStudioBuildingReferenceRequest(
                                references,
                                UserNotes:
                                    "OpenAerialMap orthophoto / aerial imagery only. " +
                                    "The OSM footprint is authoritative. " +
                                    "Infer roofType and roofMaterial only when clearly visible. " +
                                    "Set widthMeters, heightMeters, depthMeters, floorCount, roofHeightMeters, " +
                                    "windowsPerFloor, doorCount, typicalWindowWidthMeters, " +
                                    "typicalWindowHeightMeters and facadeMaterial to null or unknown. " +
                                    "Do not infer facade details from a nadir aerial view."))
                        .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                continue;
            }

            var normalized =
                analysis
                    .Normalize();

            normalized =
                normalized with
                {
                    WidthMeters =
                        null,
                    HeightMeters =
                        null,
                    DepthMeters =
                        null,
                    FloorCount =
                        null,
                    RoofHeightMeters =
                        null,
                    Openings =
                        null,
                    FacadeMaterial =
                        null,
                    ArchitecturalStyle =
                        null,
                    Confidence =
                        Math.Min(
                            normalized.Confidence,
                            0.95)
                };

            if (
                normalized.Confidence <
                MapStudioStreetLevelBuildingRefiner
                    .MinimumRefinementConfidence ||
                normalized.RoofType ==
                    MapStudioBuildingRoofType.Unknown &&
                string.IsNullOrWhiteSpace(
                    normalized.RoofMaterial))
            {
                continue;
            }

            var attribution =
                BuildAttribution(
                    image);

            results[
                target.Building.Id] =
                new MapStudioBuildingVisualEvidence(
                    target.Building.Id,
                    normalized,
                    [
                        new MapStudioSceneEvidence(
                            MapStudioSceneEvidenceSource
                                .AerialImagery,
                            normalized.Confidence,
                            image.Id,
                            attribution)
                    ]);

        }

        progress?.Report(
            new MapStudioBuildingVisualEvidenceProgress(
                attemptedCount,
                Math.Min(
                    buildings.Count,
                    MaximumBuildingsPerRun),
                $"OpenAerialMap: {results.Count} telhado(s) refinado(s) com evidência aérea válida."));

        return results;
    }

    public static int ResolveZoom(
        double latitude,
        double? gsdMeters)
    {
        if (
            !double.IsFinite(
                latitude))
        {
            return 19;
        }

        if (
            gsdMeters is not
                > 0 ||
            !double.IsFinite(
                gsdMeters.Value))
        {
            return 19;
        }

        var latitudeRadians =
            Math.Clamp(
                latitude,
                -85.0,
                85.0) *
            Math.PI /
            180.0;

        var metersAtZoomZero =
            156543.03392804097 *
            Math.Cos(
                latitudeRadians);

        var targetMetersPerPixel =
            Math.Clamp(
                gsdMeters.Value *
                    1.5,
                0.08,
                1.0);

        var zoom =
            (int)Math.Ceiling(
                Math.Log2(
                    metersAtZoomZero /
                    targetMetersPerPixel));

        return Math.Clamp(
            zoom,
            16,
            21);
    }

    public static IReadOnlyList<(
        int X,
        int Y)> ResolveContextTiles(
            double latitude,
            double longitude,
            int zoom)
    {
        var boundedZoom =
            Math.Clamp(
                zoom,
                0,
                22);

        var n =
            Math.Pow(
                2,
                boundedZoom);

        var boundedLatitude =
            Math.Clamp(
                latitude,
                -85.05112878,
                85.05112878);

        var boundedLongitude =
            Math.Clamp(
                longitude,
                -180,
                180);

        var xValue =
            (
                boundedLongitude +
                180.0
            ) /
            360.0 *
            n;

        var latitudeRadians =
            boundedLatitude *
            Math.PI /
            180.0;

        var yValue =
            (
                1.0 -
                Math.Asinh(
                    Math.Tan(
                        latitudeRadians)) /
                Math.PI
            ) /
            2.0 *
            n;

        var x =
            Math.Clamp(
                (int)Math.Floor(
                    xValue),
                0,
                checked(
                    (int)n -
                    1));

        var y =
            Math.Clamp(
                (int)Math.Floor(
                    yValue),
                0,
                checked(
                    (int)n -
                    1));

        var neighborX =
            xValue -
                Math.Floor(
                    xValue) <
            0.5
                ? x -
                    1
                : x +
                    1;

        var neighborY =
            yValue -
                Math.Floor(
                    yValue) <
            0.5
                ? y -
                    1
                : y +
                    1;

        neighborX =
            Math.Clamp(
                neighborX,
                0,
                checked(
                    (int)n -
                    1));

        neighborY =
            Math.Clamp(
                neighborY,
                0,
                checked(
                    (int)n -
                    1));

        return new[]
            {
                (
                    x,
                    y
                ),
                (
                    neighborX,
                    y
                ),
                (
                    x,
                    neighborY
                ),
                (
                    neighborX,
                    neighborY
                )
            }
            .Distinct()
            .ToArray();
    }

    private static async Task<MapStudioAiImageReference?>
        DownloadTileAsync(
            MapStudioOpenAerialMapImage image,
            int zoom,
            int x,
            int y,
            CancellationToken cancellationToken)
    {
        var url =
            image.TmsTemplate
                .Replace(
                    "{z}",
                    zoom.ToString(
                        CultureInfo.InvariantCulture),
                    StringComparison.OrdinalIgnoreCase)
                .Replace(
                    "{x}",
                    x.ToString(
                        CultureInfo.InvariantCulture),
                    StringComparison.OrdinalIgnoreCase)
                .Replace(
                    "{y}",
                    y.ToString(
                        CultureInfo.InvariantCulture),
                    StringComparison.OrdinalIgnoreCase);

        if (
            !Uri.TryCreate(
                url,
                UriKind.Absolute,
                out var uri) ||
            uri.Scheme is not
                ("http" or "https"))
        {
            return null;
        }

        using var response =
            await HttpClient
                .GetAsync(
                    uri,
                    HttpCompletionOption
                        .ResponseHeadersRead,
                    cancellationToken)
                .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var contentLength =
            response.Content.Headers
                .ContentLength;

        if (
            contentLength is
                <= 0 or >
                MaximumTileBytes)
        {
            return null;
        }

        var bytes =
            await response.Content
                .ReadAsByteArrayAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        if (
            bytes.Length ==
                0 ||
            bytes.LongLength >
                MaximumTileBytes)
        {
            return null;
        }

        var mime =
            response.Content.Headers
                .ContentType
                ?.MediaType;

        if (
            string.IsNullOrWhiteSpace(
                mime) ||
            !mime.StartsWith(
                "image/",
                StringComparison.OrdinalIgnoreCase))
        {
            mime =
                "image/png";
        }

        return new MapStudioAiImageReference(
            bytes,
            mime,
            string.Create(
                CultureInfo.InvariantCulture,
                $"oam-{zoom}-{x}-{y}.png"));
    }

    private static string BuildAttribution(
        MapStudioOpenAerialMapImage image)
    {
        var parts =
            new List<string>
            {
                "OpenAerialMap",
                "license=" +
                    image.License,
                "id=" +
                    image.Id
            };

        if (
            !string.IsNullOrWhiteSpace(
                image.Provider))
        {
            parts.Add(
                "provider=" +
                image.Provider);
        }

        if (
            image.AcquisitionTime is
                { } acquisition)
        {
            parts.Add(
                "acquired=" +
                acquisition
                    .ToString(
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture));
        }

        if (
            image.GroundSampleDistanceMeters is
                { } gsd)
        {
            parts.Add(
                "gsd=" +
                gsd.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture) +
                "m");
        }

        if (
            !string.IsNullOrWhiteSpace(
                image.SourceUrl))
        {
            parts.Add(
                "source=" +
                image.SourceUrl);
        }

        return string.Join(
            " | ",
            parts);
    }
}
