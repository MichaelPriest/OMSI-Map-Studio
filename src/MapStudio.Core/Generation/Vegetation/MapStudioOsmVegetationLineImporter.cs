using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace MapStudio.Core.Generation.Vegetation;

public enum MapStudioOsmVegetationLineKind
{
    Hedge,
    TreeRow
}

public sealed record MapStudioGeoVegetationLinePoint(
    double Latitude,
    double Longitude);

public sealed record MapStudioGeoVegetationLine(
    string Id,
    IReadOnlyList<MapStudioGeoVegetationLinePoint> Points,
    MapStudioOsmVegetationLineKind Kind,
    string? Species,
    string? Genus,
    string? LeafType,
    string? Name);

public sealed record MapStudioOsmVegetationLineImportResult(
    IReadOnlyList<MapStudioGeoVegetationLine> Lines,
    int IgnoredWayCount,
    int MissingNodeReferenceCount);

public sealed class MapStudioOsmVegetationLineImporter
{
    public MapStudioOsmVegetationLineImportResult Parse(
        string xml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            xml);

        using var textReader =
            new StringReader(
                xml);

        using var reader =
            XmlReader.Create(
                textReader,
                new XmlReaderSettings
                {
                    DtdProcessing =
                        DtdProcessing.Prohibit,
                    XmlResolver =
                        null,
                    MaxCharactersInDocument =
                        128L *
                        1024 *
                        1024
                });

        return Parse(
            XDocument.Load(
                reader,
                LoadOptions.None));
    }

    public MapStudioOsmVegetationLineImportResult Parse(
        XDocument document)
    {
        ArgumentNullException.ThrowIfNull(
            document);

        var root =
            document.Root;

        if (
            root is null ||
            !string.Equals(
                root.Name.LocalName,
                "osm",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "osmRootInvalid");
        }

        var nodes =
            new Dictionary<
                string,
                MapStudioGeoVegetationLinePoint>(
                    StringComparer.Ordinal);

        foreach (
            var node in
                root.Elements()
                    .Where(
                        item =>
                            item.Name.LocalName ==
                            "node"))
        {
            var id =
                node.Attribute(
                    "id")?.Value;

            if (
                string.IsNullOrWhiteSpace(
                    id) ||
                !TryDouble(
                    node.Attribute(
                        "lat")?.Value,
                    out var latitude) ||
                !TryDouble(
                    node.Attribute(
                        "lon")?.Value,
                    out var longitude) ||
                latitude is
                    < -90 or > 90 ||
                longitude is
                    < -180 or > 180)
            {
                continue;
            }

            nodes[id] =
                new MapStudioGeoVegetationLinePoint(
                    latitude,
                    longitude);
        }

        var lines =
            new List<
                MapStudioGeoVegetationLine>();

        var ignoredWays =
            0;

        var missingReferences =
            0;

        foreach (
            var way in
                root.Elements()
                    .Where(
                        item =>
                            item.Name.LocalName ==
                            "way"))
        {
            var tags =
                ReadTags(
                    way);

            if (
                !TryGetKind(
                    tags,
                    out var kind))
            {
                ignoredWays++;
                continue;
            }

            var points =
                new List<
                    MapStudioGeoVegetationLinePoint>();

            var valid =
                true;

            foreach (
                var nd in
                    way.Elements()
                        .Where(
                            item =>
                                item.Name.LocalName ==
                                "nd"))
            {
                var reference =
                    nd.Attribute(
                        "ref")?.Value;

                if (
                    string.IsNullOrWhiteSpace(
                        reference) ||
                    !nodes.TryGetValue(
                        reference,
                        out var point))
                {
                    missingReferences++;
                    valid =
                        false;
                    continue;
                }

                points.Add(
                    point);
            }

            if (
                !valid ||
                points.Count <
                    2)
            {
                ignoredWays++;
                continue;
            }

            var id =
                way.Attribute(
                    "id")?.Value;

            lines.Add(
                new MapStudioGeoVegetationLine(
                    string.IsNullOrWhiteSpace(
                        id)
                        ? "osm-vegetation-line-" +
                          lines.Count
                        : "osm-vegetation-line-" +
                          id,
                    points,
                    kind,
                    CleanTag(
                        tags.GetValueOrDefault(
                            "species")),
                    CleanTag(
                        tags.GetValueOrDefault(
                            "genus")),
                    CleanTag(
                        tags.GetValueOrDefault(
                            "leaf_type")),
                    CleanTag(
                        tags.GetValueOrDefault(
                            "name"))));
        }

        return new MapStudioOsmVegetationLineImportResult(
            lines,
            ignoredWays,
            missingReferences);
    }

    private static bool TryGetKind(
        IReadOnlyDictionary<string, string?>
            tags,
        out MapStudioOsmVegetationLineKind kind)
    {
        var barrier =
            CleanTag(
                tags.GetValueOrDefault(
                    "barrier"));

        if (
            string.Equals(
                barrier,
                "hedge",
                StringComparison.OrdinalIgnoreCase))
        {
            kind =
                MapStudioOsmVegetationLineKind.Hedge;

            return true;
        }

        var natural =
            CleanTag(
                tags.GetValueOrDefault(
                    "natural"));

        if (
            string.Equals(
                natural,
                "tree_row",
                StringComparison.OrdinalIgnoreCase))
        {
            kind =
                MapStudioOsmVegetationLineKind.TreeRow;

            return true;
        }

        kind =
            default;

        return false;
    }

    private static Dictionary<string, string?>
        ReadTags(
            XElement element) =>
        element.Elements()
            .Where(
                item =>
                    item.Name.LocalName ==
                    "tag")
            .Select(
                item =>
                    (
                        Key:
                            item.Attribute(
                                "k")?.Value,
                        Value:
                            item.Attribute(
                                "v")?.Value
                    ))
            .Where(
                item =>
                    !string.IsNullOrWhiteSpace(
                        item.Key))
            .GroupBy(
                item =>
                    item.Key!,
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group =>
                    group.Key,
                group =>
                    group.Last()
                        .Value,
                StringComparer.OrdinalIgnoreCase);

    private static string? CleanTag(
        string? value)
    {
        var normalized =
            value?.Trim();

        return string.IsNullOrWhiteSpace(
            normalized)
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
        double.IsFinite(
            result);
}
