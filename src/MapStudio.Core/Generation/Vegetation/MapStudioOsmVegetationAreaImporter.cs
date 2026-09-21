using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace MapStudio.Core.Generation.Vegetation;

public enum MapStudioOsmVegetationAreaKind
{
    Forest,
    Scrub
}

public sealed record MapStudioGeoVegetationAreaPoint(
    double Latitude,
    double Longitude);

public sealed record MapStudioGeoVegetationArea(
    string Id,
    IReadOnlyList<MapStudioGeoVegetationAreaPoint> Points,
    MapStudioOsmVegetationAreaKind Kind,
    string? Species,
    string? Genus,
    string? LeafType,
    string? Name);

public sealed record MapStudioOsmVegetationAreaImportResult(
    IReadOnlyList<MapStudioGeoVegetationArea> Areas,
    int IgnoredWayCount,
    int MissingNodeReferenceCount,
    int IgnoredRelationCount = 0);

public sealed class MapStudioOsmVegetationAreaImporter
{
    public MapStudioOsmVegetationAreaImportResult Parse(
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

    public MapStudioOsmVegetationAreaImportResult Parse(
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
                MapStudioGeoVegetationAreaPoint>(
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
                new MapStudioGeoVegetationAreaPoint(
                    latitude,
                    longitude);
        }

        var relationAssembly =
            new MapStudioOsmVegetationAreaRelationAssembler()
                .Assemble(
                    root,
                    nodes);

        var areas =
            new List<
                MapStudioGeoVegetationArea>(
                    relationAssembly.Areas);

        var ignoredWays =
            0;

        var missingReferences =
            relationAssembly
                .MissingNodeReferenceCount;

        foreach (
            var way in
                root.Elements()
                    .Where(
                        item =>
                            item.Name.LocalName ==
                            "way"))
        {
            var wayId =
                way.Attribute(
                    "id")?.Value;

            if (
                !string.IsNullOrWhiteSpace(
                    wayId) &&
                relationAssembly
                    .ConsumedWayIds
                    .Contains(
                        wayId))
            {
                continue;
            }

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

            var references =
                way.Elements()
                    .Where(
                        item =>
                            item.Name.LocalName ==
                                "nd")
                    .Select(
                        item =>
                            item.Attribute(
                                "ref")?.Value)
                    .ToArray();

            if (
                references.Length <
                    4 ||
                string.IsNullOrWhiteSpace(
                    references[0]) ||
                !string.Equals(
                    references[0],
                    references[^1],
                    StringComparison.Ordinal))
            {
                ignoredWays++;
                continue;
            }

            var points =
                new List<
                    MapStudioGeoVegetationAreaPoint>();

            var valid =
                true;

            for (
                var index = 0;
                index <
                    references.Length - 1;
                index++)
            {
                var reference =
                    references[index];

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
                    3)
            {
                ignoredWays++;
                continue;
            }

            var id =
                way.Attribute(
                    "id")?.Value;

            areas.Add(
                new MapStudioGeoVegetationArea(
                    string.IsNullOrWhiteSpace(
                        id)
                        ? "osm-vegetation-area-" +
                          areas.Count
                        : "osm-vegetation-area-" +
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

        return new MapStudioOsmVegetationAreaImportResult(
            areas,
            ignoredWays,
            missingReferences,
            relationAssembly
                .IgnoredRelationCount);
    }

    private static bool TryGetKind(
        IReadOnlyDictionary<string, string?>
            tags,
        out MapStudioOsmVegetationAreaKind kind)
    {
        var natural =
            CleanTag(
                tags.GetValueOrDefault(
                    "natural"));

        var landuse =
            CleanTag(
                tags.GetValueOrDefault(
                    "landuse"));

        if (
            string.Equals(
                natural,
                "wood",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                landuse,
                "forest",
                StringComparison.OrdinalIgnoreCase))
        {
            kind =
                MapStudioOsmVegetationAreaKind.Forest;

            return true;
        }

        if (
            string.Equals(
                natural,
                "scrub",
                StringComparison.OrdinalIgnoreCase))
        {
            kind =
                MapStudioOsmVegetationAreaKind.Scrub;

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
