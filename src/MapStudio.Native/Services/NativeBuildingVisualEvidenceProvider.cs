using MapStudio.Core.AI;
using MapStudio.Core.Generation.Buildings;
using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Scene;

namespace MapStudio.Native.Services;

public sealed class NativeBuildingVisualEvidenceProvider :
    IMapStudioBuildingVisualEvidenceProvider
{
    private static readonly IReadOnlyDictionary<
        string,
        string>
        SupportedMimeTypes =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                [".png"] = "image/png",
                [".jpg"] = "image/jpeg",
                [".jpeg"] = "image/jpeg",
                [".webp"] = "image/webp"
            };

    private const int MaximumImagesPerBuilding =
        4;

    private const long MaximumImageBytes =
        20L *
        1024L *
        1024L;

    private const long MaximumBuildingBytes =
        48L *
        1024L *
        1024L;

    private readonly string
        _referenceRoot;

    private readonly IMapStudioAiProvider
        _provider;

    public NativeBuildingVisualEvidenceProvider(
        string mapDirectory,
        IMapStudioAiProvider provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            mapDirectory);

        ArgumentNullException.ThrowIfNull(
            provider);

        _referenceRoot =
            Path.Combine(
                Path.GetFullPath(
                    mapDirectory),
                ".mapstudio",
                "references",
                "buildings");

        _provider =
            provider;
    }

    public bool HasAnyReferences()
    {
        if (!Directory.Exists(
                _referenceRoot))
        {
            return false;
        }

        return Directory
            .EnumerateFiles(
                _referenceRoot,
                "*.*",
                SearchOption.AllDirectories)
            .Any(
                path =>
                    SupportedMimeTypes.ContainsKey(
                        Path.GetExtension(
                            path)));
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

        if (
            buildings.Count ==
                0 ||
            !Directory.Exists(
                _referenceRoot))
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

        var processed =
            0;

        foreach (var building in buildings)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            progress?.Report(
                new MapStudioBuildingVisualEvidenceProgress(
                    processed,
                    buildings.Count,
                    $"Imagens locais: analisando {processed + 1}/{buildings.Count} · {building.Id}"));

            processed++;

            var directory =
                Path.Combine(
                    _referenceRoot,
                    SanitizeDirectoryName(
                        building.Id));

            if (!Directory.Exists(
                    directory))
            {
                continue;
            }

            var files =
                Directory
                    .EnumerateFiles(
                        directory,
                        "*.*",
                        SearchOption.TopDirectoryOnly)
                    .Where(
                        path =>
                            SupportedMimeTypes.ContainsKey(
                                Path.GetExtension(
                                    path)))
                    .OrderBy(
                        path =>
                            path,
                        StringComparer.OrdinalIgnoreCase)
                    .Take(
                        MaximumImagesPerBuilding)
                    .ToArray();

            if (files.Length == 0)
            {
                continue;
            }

            long totalBytes =
                0;

            var images =
                new List<
                    MapStudioAiImageReference>(
                        files.Length);

            var evidenceSources =
                new Dictionary<
                    MapStudioSceneEvidenceSource,
                    List<string>>();

            foreach (var path in files)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                var length =
                    new FileInfo(
                        path)
                        .Length;

                if (
                    length <=
                        0 ||
                    length >
                        MaximumImageBytes)
                {
                    continue;
                }

                totalBytes +=
                    length;

                if (
                    totalBytes >
                    MaximumBuildingBytes)
                {
                    break;
                }

                var extension =
                    Path.GetExtension(
                        path);

                var bytes =
                    await File
                        .ReadAllBytesAsync(
                            path,
                            cancellationToken)
                        .ConfigureAwait(false);

                images.Add(
                    new MapStudioAiImageReference(
                        bytes,
                        SupportedMimeTypes[
                            extension],
                        Path.GetFileName(
                            path)));

                var source =
                    ResolveSource(
                        Path.GetFileNameWithoutExtension(
                            path));

                if (
                    !evidenceSources.TryGetValue(
                        source,
                        out var names))
                {
                    names =
                        new List<string>();

                    evidenceSources[
                        source] =
                        names;
                }

                names.Add(
                    Path.GetFileName(
                        path));
            }

            if (images.Count == 0)
            {
                continue;
            }

            var (
                width,
                depth
            ) =
                ResolveFootprintBounds(
                    building.Points);

            MapStudioBuildingReferenceAnalysis
                analysis;

            try
            {
                analysis =
                    await _provider
                        .AnalyzeBuildingReferenceAsync(
                            new MapStudioBuildingReferenceRequest(
                                images,
                                UserNotes:
                                    "OMSI Map Studio real-world reconstruction. " +
                                    "Treat the OSM footprint as authoritative. " +
                                    "Use the images only to refine visible height, floor count, roof shape/height, facade material and roof material.",
                                KnownWidthMeters:
                                    width >
                                        0
                                        ? width
                                        : null,
                                KnownHeightMeters:
                                    building.WallHeightMeters +
                                    building.RoofHeightMeters,
                                KnownDepthMeters:
                                    depth >
                                        0
                                        ? depth
                                        : null,
                                KnownFloorCount:
                                    building.FloorCount))
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
                analysis.Normalize();

            if (
                normalized.Confidence <
                MapStudioSceneReconstructionPlanBuilder
                    .ReviewThreshold)
            {
                continue;
            }

            var evidence =
                evidenceSources
                    .Select(
                        item =>
                            new MapStudioSceneEvidence(
                                item.Key,
                                normalized.Confidence,
                                string.Join(
                                    ";",
                                    item.Value),
                                "Análise visual de referências locais do usuário."))
                    .ToArray();

            results[
                building.Id] =
                new MapStudioBuildingVisualEvidence(
                    building.Id,
                    normalized,
                    evidence);
        }

        progress?.Report(
            new MapStudioBuildingVisualEvidenceProgress(
                buildings.Count,
                buildings.Count,
                $"Imagens locais: {results.Count} prédio(s) com evidência visual válida."));

        return results;
    }

    private static MapStudioSceneEvidenceSource
        ResolveSource(
            string fileName)
    {
        var normalized =
            fileName
                .Trim()
                .ToLowerInvariant();

        if (
            normalized.Contains(
                "aerial",
                StringComparison.Ordinal) ||
            normalized.Contains(
                "satellite",
                StringComparison.Ordinal) ||
            normalized.Contains(
                "ortho",
                StringComparison.Ordinal))
        {
            return MapStudioSceneEvidenceSource
                .AerialImagery;
        }

        if (
            normalized.Contains(
                "street",
                StringComparison.Ordinal) ||
            normalized.Contains(
                "facade",
                StringComparison.Ordinal) ||
            normalized.Contains(
                "fachada",
                StringComparison.Ordinal) ||
            normalized.Contains(
                "front",
                StringComparison.Ordinal))
        {
            return MapStudioSceneEvidenceSource
                .StreetLevelImagery;
        }

        return MapStudioSceneEvidenceSource
            .UserImagery;
    }

    private static (
        double Width,
        double Depth)
        ResolveFootprintBounds(
            IReadOnlyList<
                MapStudioRoadPoint> points)
    {
        if (points.Count == 0)
        {
            return (
                0,
                0);
        }

        return (
            Math.Max(
                0,
                points.Max(
                    point =>
                        point.X) -
                points.Min(
                    point =>
                        point.X)),
            Math.Max(
                0,
                points.Max(
                    point =>
                        point.Z) -
                points.Min(
                    point =>
                        point.Z)));
    }

    private static string SanitizeDirectoryName(
        string value)
    {
        var invalid =
            Path.GetInvalidFileNameChars()
                .ToHashSet();

        var chars =
            value
                .Select(
                    character =>
                        invalid.Contains(
                            character)
                            ? '_'
                            : character)
                .ToArray();

        var result =
            new string(
                chars)
                .Trim();

        return string.IsNullOrWhiteSpace(
                result)
            ? "building"
            : result;
    }
}
