using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace MapStudio.Core.Generation.Buildings;

public sealed record MapStudioGeoBuildingPoint(
    double Latitude,
    double Longitude);

public sealed record MapStudioOsmBuildingFootprint(
    string Id,
    IReadOnlyList<MapStudioGeoBuildingPoint> Points,
    string BuildingType,
    string? Name,
    int? Levels,
    double? HeightMeters,
    string? RoofShape,
    double? RoofHeightMeters,
    string? Street,
    string? HouseNumber);

public sealed record MapStudioOsmBuildingImportResult(
    IReadOnlyList<MapStudioOsmBuildingFootprint> Buildings,
    int IgnoredWayCount,
    int MissingNodeReferenceCount,
    int IgnoredRelationCount);

public sealed class MapStudioOsmBuildingImporter
{
    public MapStudioOsmBuildingImportResult Parse(
        string xml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            xml);

        using var textReader =
            new StringReader(xml);

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

    public MapStudioOsmBuildingImportResult Parse(
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
                long,
                MapStudioGeoBuildingPoint>();

        foreach (
            var node in
                root.Elements()
                    .Where(
                        item =>
                            item.Name.LocalName ==
                            "node"))
        {
            if (
                !TryLong(
                    node.Attribute(
                        "id")?.Value,
                    out var id) ||
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
                new MapStudioGeoBuildingPoint(
                    latitude,
                    longitude);
        }

        var buildings =
            new List<
                MapStudioOsmBuildingFootprint>();

        var ignoredWays =
            0;

        var missingNodes =
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
                !tags.TryGetValue(
                    "building",
                    out var buildingType) ||
                string.IsNullOrWhiteSpace(
                    buildingType) ||
                string.Equals(
                    buildingType,
                    "no",
                    StringComparison.OrdinalIgnoreCase))
            {
                ignoredWays++;
                continue;
            }

            var points =
                new List<
                    MapStudioGeoBuildingPoint>();

            foreach (
                var reference in
                    way.Elements()
                        .Where(
                            item =>
                                item.Name.LocalName ==
                                "nd"))
            {
                if (
                    !TryLong(
                        reference.Attribute(
                            "ref")?.Value,
                        out var nodeId) ||
                    !nodes.TryGetValue(
                        nodeId,
                        out var point))
                {
                    missingNodes++;
                    continue;
                }

                if (
                    points.Count ==
                        0 ||
                    !SamePoint(
                        points[^1],
                        point))
                {
                    points.Add(
                        point);
                }
            }

            if (
                points.Count >=
                    2 &&
                SamePoint(
                    points[0],
                    points[^1]))
            {
                points.RemoveAt(
                    points.Count -
                    1);
            }

            if (
                points.Count <
                    3 ||
                Math.Abs(
                    SignedArea(
                        points)) <
                    1e-16)
            {
                ignoredWays++;
                continue;
            }

            var id =
                way.Attribute(
                    "id")?.Value;

            buildings.Add(
                new MapStudioOsmBuildingFootprint(
                    string.IsNullOrWhiteSpace(
                        id)
                        ? "osm-building-" +
                          buildings.Count
                        : "osm-building-" +
                          id,
                    points,
                    buildingType,
                    tags.GetValueOrDefault(
                        "name"),
                    TryPositiveInt(
                        tags.GetValueOrDefault(
                            "building:levels")),
                    TryPositiveDouble(
                        tags.GetValueOrDefault(
                            "height")),
                    tags.GetValueOrDefault(
                        "roof:shape"),
                    TryPositiveDouble(
                        tags.GetValueOrDefault(
                            "roof:height")),
                    tags.GetValueOrDefault(
                        "addr:street"),
                    tags.GetValueOrDefault(
                        "addr:housenumber")));
        }

        var ignoredRelations =
            root.Elements()
                .Count(
                    item =>
                        item.Name.LocalName ==
                            "relation" &&
                        ReadTags(
                            item)
                            .ContainsKey(
                                "building"));

        return new MapStudioOsmBuildingImportResult(
            buildings,
            ignoredWays,
            missingNodes,
            ignoredRelations);
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

    private static double SignedArea(
        IReadOnlyList<
            MapStudioGeoBuildingPoint>
            points)
    {
        double area =
            0;

        for (
            var index = 0;
            index <
                points.Count;
            index++)
        {
            var next =
                (
                    index +
                    1
                ) %
                points.Count;

            area +=
                points[index]
                    .Longitude *
                points[next]
                    .Latitude -
                points[next]
                    .Longitude *
                points[index]
                    .Latitude;
        }

        return area /
            2.0;
    }

    private static bool SamePoint(
        MapStudioGeoBuildingPoint left,
        MapStudioGeoBuildingPoint right) =>
        Math.Abs(
            left.Latitude -
            right.Latitude) <=
            1e-12 &&
        Math.Abs(
            left.Longitude -
            right.Longitude) <=
            1e-12;

    private static int? TryPositiveInt(
        string? value)
    {
        if (
            int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsed) &&
            parsed is
                > 0 and <= 300)
        {
            return parsed;
        }

        return null;
    }

    private static double? TryPositiveDouble(
        string? value)
    {
        if (
            string.IsNullOrWhiteSpace(
                value))
        {
            return null;
        }

        var normalized =
            value
                .Trim()
                .Replace(
                    "meters",
                    string.Empty,
                    StringComparison.OrdinalIgnoreCase)
                .Replace(
                    "meter",
                    string.Empty,
                    StringComparison.OrdinalIgnoreCase)
                .Trim();

        if (
            normalized.EndsWith(
                "m",
                StringComparison.OrdinalIgnoreCase))
        {
            normalized =
                normalized[..^1]
                    .Trim();
        }

        return
            double.TryParse(
                normalized,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed) &&
            double.IsFinite(
                parsed) &&
            parsed >
                0
                ? parsed
                : null;
    }

    private static bool TryLong(
        string? value,
        out long result) =>
        long.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out result);

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
