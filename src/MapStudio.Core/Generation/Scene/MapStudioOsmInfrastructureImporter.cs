using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace MapStudio.Core.Generation.Scene;

public enum MapStudioOsmInfrastructureKind
{
    Wall,
    Fence,
    GuardRail,
    Sidewalk,
    Driveway,
    Parking
}

public sealed record MapStudioGeoInfrastructurePoint(
    double Latitude,
    double Longitude);

public sealed record MapStudioGeoInfrastructureFeature(
    string Id,
    MapStudioOsmInfrastructureKind Kind,
    IReadOnlyList<MapStudioGeoInfrastructurePoint> Points,
    string? Name,
    string? Surface,
    double? WidthMeters,
    bool IsArea);

public sealed record MapStudioOsmInfrastructureImportResult(
    IReadOnlyList<MapStudioGeoInfrastructureFeature> Features,
    int IgnoredWayCount);

public sealed class MapStudioOsmInfrastructureImporter
{
    public MapStudioOsmInfrastructureImportResult Parse(string xml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);

        using var textReader = new StringReader(xml);
        using var reader =
            XmlReader.Create(
                textReader,
                new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersInDocument = 128L * 1024 * 1024
                });

        return Parse(XDocument.Load(reader, LoadOptions.None));
    }

    public MapStudioOsmInfrastructureImportResult Parse(XDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var root = document.Root;

        if (
            root is null ||
            !string.Equals(
                root.Name.LocalName,
                "osm",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("osmRootInvalid");
        }

        var nodes = ReadNodes(root);
        var features =
            new List<MapStudioGeoInfrastructureFeature>();
        var ignored = 0;

        foreach (
            var way in
                root.Elements()
                    .Where(
                        item =>
                            item.Name.LocalName == "way"))
        {
            var tags = ReadTags(way);

            if (!TryGetKind(tags, out var kind))
            {
                ignored++;
                continue;
            }

            var refs =
                way.Elements()
                    .Where(
                        item =>
                            item.Name.LocalName == "nd")
                    .Select(
                        item =>
                            Clean(item.Attribute("ref")?.Value))
                    .Where(
                        value =>
                            value is not null)
                    .Cast<string>()
                    .ToArray();

            var points =
                refs
                    .Where(nodes.ContainsKey)
                    .Select(reference => nodes[reference])
                    .ToArray();

            var isClosed =
                refs.Length >= 4 &&
                string.Equals(
                    refs[0],
                    refs[^1],
                    StringComparison.Ordinal);

            var isArea =
                isClosed ||
                string.Equals(
                    Clean(tags.GetValueOrDefault("area")),
                    "yes",
                    StringComparison.OrdinalIgnoreCase);

            if (
                points.Length < 2 ||
                (kind == MapStudioOsmInfrastructureKind.Parking &&
                 (!isClosed || points.Length < 4)))
            {
                ignored++;
                continue;
            }

            var id = Clean(way.Attribute("id")?.Value);

            features.Add(
                new MapStudioGeoInfrastructureFeature(
                    id is null
                        ? "osm-infrastructure-" + features.Count
                        : "osm-infrastructure-" + id,
                    kind,
                    points,
                    Clean(tags.GetValueOrDefault("name")),
                    Clean(tags.GetValueOrDefault("surface")),
                    TryWidthMeters(
                        tags.GetValueOrDefault("width")),
                    isArea));
        }

        return new MapStudioOsmInfrastructureImportResult(
            features,
            ignored);
    }

    private static Dictionary<string, MapStudioGeoInfrastructurePoint>
        ReadNodes(XElement root)
    {
        var result =
            new Dictionary<string, MapStudioGeoInfrastructurePoint>(
                StringComparer.Ordinal);

        foreach (
            var node in
                root.Elements()
                    .Where(
                        item =>
                            item.Name.LocalName == "node"))
        {
            var id =
                Clean(node.Attribute("id")?.Value);

            if (
                id is null ||
                !TryDouble(
                    node.Attribute("lat")?.Value,
                    out var latitude) ||
                !TryDouble(
                    node.Attribute("lon")?.Value,
                    out var longitude) ||
                latitude is < -90 or > 90 ||
                longitude is < -180 or > 180)
            {
                continue;
            }

            result[id] =
                new MapStudioGeoInfrastructurePoint(
                    latitude,
                    longitude);
        }

        return result;
    }

    private static bool TryGetKind(
        IReadOnlyDictionary<string, string?> tags,
        out MapStudioOsmInfrastructureKind kind)
    {
        var barrier =
            Clean(tags.GetValueOrDefault("barrier"));

        var highway =
            Clean(tags.GetValueOrDefault("highway"));

        var footway =
            Clean(tags.GetValueOrDefault("footway"));

        var service =
            Clean(tags.GetValueOrDefault("service"));

        var amenity =
            Clean(tags.GetValueOrDefault("amenity"));

        if (
            string.Equals(
                barrier,
                "wall",
                StringComparison.OrdinalIgnoreCase))
        {
            kind = MapStudioOsmInfrastructureKind.Wall;
            return true;
        }

        if (
            string.Equals(
                barrier,
                "fence",
                StringComparison.OrdinalIgnoreCase))
        {
            kind = MapStudioOsmInfrastructureKind.Fence;
            return true;
        }

        if (
            string.Equals(
                barrier,
                "guard_rail",
                StringComparison.OrdinalIgnoreCase))
        {
            kind = MapStudioOsmInfrastructureKind.GuardRail;
            return true;
        }

        if (
            string.Equals(
                highway,
                "footway",
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                footway,
                "sidewalk",
                StringComparison.OrdinalIgnoreCase))
        {
            kind = MapStudioOsmInfrastructureKind.Sidewalk;
            return true;
        }

        if (
            string.Equals(
                highway,
                "service",
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                service,
                "driveway",
                StringComparison.OrdinalIgnoreCase))
        {
            kind = MapStudioOsmInfrastructureKind.Driveway;
            return true;
        }

        if (
            string.Equals(
                amenity,
                "parking",
                StringComparison.OrdinalIgnoreCase))
        {
            kind = MapStudioOsmInfrastructureKind.Parking;
            return true;
        }

        kind = default;
        return false;
    }

    private static Dictionary<string, string?> ReadTags(
        XElement element) =>
        element.Elements()
            .Where(
                item =>
                    item.Name.LocalName == "tag")
            .Select(
                item =>
                    (
                        Key: item.Attribute("k")?.Value,
                        Value: item.Attribute("v")?.Value
                    ))
            .Where(
                item =>
                    !string.IsNullOrWhiteSpace(item.Key))
            .GroupBy(
                item => item.Key!,
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Last().Value,
                StringComparer.OrdinalIgnoreCase);

    private static double? TryWidthMeters(string? value)
    {
        var normalized =
            Clean(value)?
                .Replace("metres", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("meters", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("meter", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("m", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Trim()
                .Replace(',', '.');

        if (
            normalized is null ||
            !TryDouble(normalized, out var width) ||
            width <= 0)
        {
            return null;
        }

        return width;
    }

    private static string? Clean(string? value)
    {
        var normalized = value?.Trim();

        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : normalized;
    }

    private static bool TryDouble(
        string? value,
        out double result) =>
        double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out result) &&
        double.IsFinite(result);
}

public sealed class MapStudioOsmInfrastructureReconstructionAdapter
{
    public IReadOnlyList<MapStudioSceneFeatureCandidate>
        BuildCandidates(
            IReadOnlyList<MapStudioGeoInfrastructureFeature> features)
    {
        ArgumentNullException.ThrowIfNull(features);

        return features
            .Select(BuildCandidate)
            .ToArray();
    }

    private static MapStudioSceneFeatureCandidate BuildCandidate(
        MapStudioGeoInfrastructureFeature feature)
    {
        var confidence =
            feature.Kind switch
            {
                MapStudioOsmInfrastructureKind.Wall => 0.87,
                MapStudioOsmInfrastructureKind.Fence => 0.85,
                MapStudioOsmInfrastructureKind.GuardRail => 0.91,
                MapStudioOsmInfrastructureKind.Sidewalk => 0.86,
                MapStudioOsmInfrastructureKind.Driveway => 0.85,
                MapStudioOsmInfrastructureKind.Parking => 0.88,
                _ => 0.60
            };

        if (!string.IsNullOrWhiteSpace(feature.Name))
        {
            confidence += 0.02;
        }

        if (!string.IsNullOrWhiteSpace(feature.Surface))
        {
            confidence += 0.03;
        }

        if (feature.WidthMeters is > 0)
        {
            confidence += 0.03;
        }

        if (feature.IsArea)
        {
            confidence += 0.02;
        }

        return new MapStudioSceneFeatureCandidate(
            feature.Id,
            MapKind(feature.Kind),
            [
                new MapStudioSceneEvidence(
                    MapStudioSceneEvidenceSource.Osm,
                    Math.Clamp(confidence, 0.0, 0.97),
                    feature.Id,
                    BuildNotes(feature))
            ],
            WidthMeters: feature.WidthMeters);
    }

    private static MapStudioSceneFeatureKind MapKind(
        MapStudioOsmInfrastructureKind kind) =>
        kind switch
        {
            MapStudioOsmInfrastructureKind.Wall =>
                MapStudioSceneFeatureKind.Wall,
            MapStudioOsmInfrastructureKind.Fence =>
                MapStudioSceneFeatureKind.Fence,
            MapStudioOsmInfrastructureKind.GuardRail =>
                MapStudioSceneFeatureKind.GuardRail,
            MapStudioOsmInfrastructureKind.Sidewalk =>
                MapStudioSceneFeatureKind.Sidewalk,
            MapStudioOsmInfrastructureKind.Driveway =>
                MapStudioSceneFeatureKind.Driveway,
            MapStudioOsmInfrastructureKind.Parking =>
                MapStudioSceneFeatureKind.Parking,
            _ =>
                MapStudioSceneFeatureKind.Unknown
        };

    private static string BuildNotes(
        MapStudioGeoInfrastructureFeature feature)
    {
        var parts =
            new List<string>
            {
                feature.Kind
                    .ToString()
                    .ToLowerInvariant()
            };

        if (!string.IsNullOrWhiteSpace(feature.Surface))
        {
            parts.Add("surface");
        }

        if (feature.WidthMeters is > 0)
        {
            parts.Add("width");
        }

        if (feature.IsArea)
        {
            parts.Add("area");
        }

        return string.Join(",", parts);
    }
}
