using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace MapStudio.Core.Generation.Vegetation;

public enum MapStudioOsmVegetationKind
{
    Tree,
    Shrub
}

public sealed record MapStudioGeoVegetationPoint(
    string Id,
    double Latitude,
    double Longitude,
    MapStudioOsmVegetationKind Kind,
    string? Species,
    string? Genus,
    string? LeafType,
    string? Name);

public sealed record MapStudioOsmVegetationImportResult(
    IReadOnlyList<MapStudioGeoVegetationPoint> Points,
    int IgnoredNodeCount);

public sealed class MapStudioOsmVegetationImporter
{
    public MapStudioOsmVegetationImportResult Parse(
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

    public MapStudioOsmVegetationImportResult Parse(
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

        var points =
            new List<MapStudioGeoVegetationPoint>();

        var ignoredNodes =
            0;

        foreach (
            var node in
                root.Elements()
                    .Where(
                        item =>
                            item.Name.LocalName ==
                            "node"))
        {
            var tags =
                ReadTags(
                    node);

            if (
                !TryGetKind(
                    tags,
                    out var kind))
            {
                ignoredNodes++;
                continue;
            }

            if (
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
                ignoredNodes++;
                continue;
            }

            var id =
                node.Attribute(
                    "id")?.Value;

            points.Add(
                new MapStudioGeoVegetationPoint(
                    string.IsNullOrWhiteSpace(
                        id)
                        ? "osm-vegetation-" +
                          points.Count
                        : "osm-vegetation-" +
                          id,
                    latitude,
                    longitude,
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

        return new MapStudioOsmVegetationImportResult(
            points,
            ignoredNodes);
    }

    private static bool TryGetKind(
        IReadOnlyDictionary<string, string?>
            tags,
        out MapStudioOsmVegetationKind kind)
    {
        var natural =
            CleanTag(
                tags.GetValueOrDefault(
                    "natural"));

        if (
            string.Equals(
                natural,
                "tree",
                StringComparison.OrdinalIgnoreCase))
        {
            kind =
                MapStudioOsmVegetationKind.Tree;

            return true;
        }

        if (
            string.Equals(
                natural,
                "shrub",
                StringComparison.OrdinalIgnoreCase))
        {
            kind =
                MapStudioOsmVegetationKind.Shrub;

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
