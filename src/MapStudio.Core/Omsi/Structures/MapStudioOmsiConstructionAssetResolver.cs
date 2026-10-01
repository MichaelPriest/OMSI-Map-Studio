using MapStudio.Core.Omsi.Indexing;
using MapStudio.Core.Omsi.Splines;

namespace MapStudio.Core.Omsi.Structures;

public sealed record MapStudioOmsiConstructionAssetCatalog(
    IReadOnlyList<OmsiAssetIndexEntry> RoadSplines,
    IReadOnlyList<OmsiAssetIndexEntry> JunctionObjects,
    IReadOnlyList<OmsiAssetIndexEntry> TrafficSignalObjects,
    IReadOnlyList<OmsiAssetIndexEntry> CrosswalkAssets)
{
    public int TotalRecognized =>
        RoadSplines.Count +
        JunctionObjects.Count +
        TrafficSignalObjects.Count +
        CrosswalkAssets.Count;
}

public sealed record MapStudioOmsiRoadSplineMatch(
    string RelativePath,
    double WidthMeters,
    int TrafficPathCount,
    double Score);

public static class MapStudioOmsiConstructionAssetClassifier
{
    public static MapStudioOmsiConstructionAssetCatalog Build(
        IReadOnlyList<OmsiAssetIndexEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var roads =
            new List<OmsiAssetIndexEntry>();

        var junctions =
            new List<OmsiAssetIndexEntry>();

        var signals =
            new List<OmsiAssetIndexEntry>();

        var crosswalks =
            new List<OmsiAssetIndexEntry>();

        foreach (var entry in entries)
        {
            var normalized =
                Normalize(
                    entry.RelativePath);

            if (
                entry.Kind ==
                    OmsiAssetKind.Spline &&
                OmsiAssetLibraryClassifier
                    .Classify(
                        entry) ==
                    OmsiAssetLibraryGroup.Roads &&
                !normalized.Contains(
                    "mapstudio roadkit",
                    StringComparison.Ordinal) &&
                !normalized.Contains(
                    "mapstudio road kit",
                    StringComparison.Ordinal))
            {
                roads.Add(
                    entry);
            }

            if (
                entry.Kind ==
                    OmsiAssetKind.SceneryObject &&
                ContainsAny(
                    normalized,
                    "junction",
                    "intersection",
                    "kreuzung",
                    "kreuz",
                    "einmuendung",
                    "einmundung",
                    "roundabout",
                    "kreisel",
                    "cruzamento",
                    "rotatoria"))
            {
                junctions.Add(
                    entry);
            }

            if (
                entry.Kind ==
                    OmsiAssetKind.SceneryObject &&
                ContainsAny(
                    normalized,
                    "trafficlight",
                    "traffic light",
                    "traffic signal",
                    "ampel",
                    "semaforo",
                    "signal head",
                    "signalgeber"))
            {
                signals.Add(
                    entry);
            }

            if (
                (
                    entry.Kind ==
                        OmsiAssetKind.Spline ||
                    entry.Kind ==
                        OmsiAssetKind.SceneryObject
                ) &&
                ContainsAny(
                    normalized,
                    "crosswalk",
                    "zebra",
                    "zebrastreifen",
                    "pedestrian crossing",
                    "ped crossing",
                    "fussganger",
                    "fussgaenger",
                    "faixa pedestre",
                    "faixa de pedestre",
                    "travessia"))
            {
                crosswalks.Add(
                    entry);
            }
        }

        return new MapStudioOmsiConstructionAssetCatalog(
            roads
                .OrderBy(
                    item =>
                        item.RelativePath,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            junctions
                .OrderBy(
                    item =>
                        item.RelativePath,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            signals
                .OrderBy(
                    item =>
                        item.RelativePath,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            crosswalks
                .OrderBy(
                    item =>
                        item.RelativePath,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    private static bool ContainsAny(
        string text,
        params string[] terms) =>
        terms.Any(
            term =>
                text.Contains(
                    Normalize(
                        term),
                    StringComparison.Ordinal));

    private static string Normalize(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return string.Empty;
        }

        var buffer =
            value
                .Normalize(
                    System.Text.NormalizationForm.FormD)
                .Where(
                    character =>
                        System.Globalization.CharUnicodeInfo
                            .GetUnicodeCategory(
                                character) !=
                        System.Globalization.UnicodeCategory
                            .NonSpacingMark)
                .ToArray();

        return new string(
                buffer)
            .Normalize(
                System.Text.NormalizationForm.FormC)
            .Replace(
                '\\',
                ' ')
            .Replace(
                '/',
                ' ')
            .Replace(
                '_',
                ' ')
            .Replace(
                '-',
                ' ')
            .Replace(
                '.',
                ' ')
            .ToLowerInvariant();
    }
}

public sealed class MapStudioOmsiRoadSplineResolver
{
    private readonly OmsiSplineDefinitionReader
        _reader =
            new();

    public async Task<
        IReadOnlyDictionary<
            string,
            MapStudioOmsiRoadSplineMatch>>
        ResolveAsync(
            string omsiRoot,
            IReadOnlyList<OmsiAssetIndexEntry> candidateSplines,
            IReadOnlyList<MapStudioStandardRoadProfile> desiredProfiles,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            omsiRoot);

        ArgumentNullException.ThrowIfNull(
            candidateSplines);

        ArgumentNullException.ThrowIfNull(
            desiredProfiles);

        var root =
            Path.GetFullPath(
                omsiRoot);

        var measurements =
            new List<
                CandidateMeasurement>();

        foreach (
            var entry
            in candidateSplines
                .Where(
                    item =>
                        item.Kind ==
                            OmsiAssetKind.Spline)
                .Take(
                    2_000))
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            var relativePath =
                entry.RelativePath
                    .Replace(
                        '/',
                        Path.DirectorySeparatorChar)
                    .Replace(
                        '\\',
                        Path.DirectorySeparatorChar);

            var fullPath =
                Path.Combine(
                    root,
                    relativePath);

            if (!File.Exists(
                    fullPath))
            {
                continue;
            }

            OmsiSplineDefinition
                definition;

            try
            {
                definition =
                    await _reader
                        .ReadAsync(
                            fullPath,
                            cancellationToken)
                        .ConfigureAwait(false);
            }
            catch (
                Exception exception)
                when (
                    exception is
                        IOException or
                        UnauthorizedAccessException or
                        InvalidDataException)
            {
                continue;
            }

            if (
                !definition.Exists ||
                definition.Surfaces.Count ==
                    0)
            {
                continue;
            }

            var trafficPathCount =
                definition.Paths.Count(
                    path =>
                        path.Type ==
                        0);

            if (trafficPathCount <= 0)
            {
                continue;
            }

            var minimumX =
                definition.Surfaces
                    .SelectMany(
                        surface =>
                            new[]
                            {
                                surface.From.X,
                                surface.To.X
                            })
                    .Min();

            var maximumX =
                definition.Surfaces
                    .SelectMany(
                        surface =>
                            new[]
                            {
                                surface.From.X,
                                surface.To.X
                            })
                    .Max();

            var width =
                maximumX -
                minimumX;

            if (
                !double.IsFinite(
                    width) ||
                width <
                    2.0 ||
                width >
                    45.0)
            {
                continue;
            }

            var normalizedName =
                entry.RelativePath
                    .ToLowerInvariant();

            measurements.Add(
                new CandidateMeasurement(
                    entry.RelativePath,
                    width,
                    trafficPathCount,
                    LooksOneWay(
                        normalizedName),
                    LooksDivided(
                        normalizedName),
                    definition.Paths.Any(
                        path =>
                            path.Type ==
                            1)));
        }

        var resolved =
            new Dictionary<
                string,
                MapStudioOmsiRoadSplineMatch>(
                    StringComparer.OrdinalIgnoreCase);

        foreach (
            var profile
            in desiredProfiles
                .Where(
                    item =>
                        !item.IsPedestrian))
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            CandidateMeasurement?
                best =
                    null;

            var bestScore =
                double.PositiveInfinity;

            foreach (
                var candidate
                in measurements)
            {
                if (
                    profile.OneWay !=
                        candidate.LooksOneWay &&
                    (
                        profile.OneWay ||
                        candidate.LooksOneWay
                    ))
                {
                    continue;
                }

                if (
                    profile.IsDivided !=
                        candidate.LooksDivided &&
                    candidate.LooksDivided)
                {
                    continue;
                }

                var widthError =
                    Math.Abs(
                        candidate.WidthMeters -
                        profile.TotalWidthMeters);

                var allowedWidthError =
                    Math.Max(
                        1.25,
                        profile.TotalWidthMeters *
                        0.18);

                if (
                    widthError >
                        allowedWidthError)
                {
                    continue;
                }

                var trafficPathError =
                    Math.Abs(
                        candidate.TrafficPathCount -
                        Math.Max(
                            1,
                            profile.LaneCount));

                var sidewalkPenalty =
                    profile.SidewalkWidthMeters >
                        0 &&
                    !candidate.HasPedestrianPath
                        ? 1.5
                        : 0.0;

                var score =
                    widthError *
                        3.0 +
                    trafficPathError *
                        0.75 +
                    sidewalkPenalty;

                if (
                    score >=
                        bestScore)
                {
                    continue;
                }

                best =
                    candidate;

                bestScore =
                    score;
            }

            if (best is null)
            {
                continue;
            }

            resolved[
                profile.Key] =
                new MapStudioOmsiRoadSplineMatch(
                    best.RelativePath,
                    best.WidthMeters,
                    best.TrafficPathCount,
                    bestScore);
        }

        return resolved;
    }

    private static bool LooksOneWay(
        string path) =>
        path.Contains(
            "oneway",
            StringComparison.OrdinalIgnoreCase) ||
        path.Contains(
            "one-way",
            StringComparison.OrdinalIgnoreCase) ||
        path.Contains(
            "einbahn",
            StringComparison.OrdinalIgnoreCase) ||
        path.Contains(
            "1way",
            StringComparison.OrdinalIgnoreCase);

    private static bool LooksDivided(
        string path) =>
        path.Contains(
            "divided",
            StringComparison.OrdinalIgnoreCase) ||
        path.Contains(
            "median",
            StringComparison.OrdinalIgnoreCase) ||
        path.Contains(
            "mittel",
            StringComparison.OrdinalIgnoreCase) ||
        path.Contains(
            "separated",
            StringComparison.OrdinalIgnoreCase);

    private sealed record CandidateMeasurement(
        string RelativePath,
        double WidthMeters,
        int TrafficPathCount,
        bool LooksOneWay,
        bool LooksDivided,
        bool HasPedestrianPath);
}
