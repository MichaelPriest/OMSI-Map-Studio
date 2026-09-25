using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace MapStudio.Core.Generation.Roads;

public sealed record MapStudioOsmRoadImportResult(
    IReadOnlyList<MapStudioGeoRoadTrace> Traces,
    int IgnoredWayCount,
    int MissingNodeReferenceCount);

public sealed class MapStudioOsmRoadImporter
{
    public MapStudioOsmRoadImportResult Parse(
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

        var document =
            XDocument.Load(
                reader,
                LoadOptions.None);

        return Parse(
            document);
    }

    public MapStudioOsmRoadImportResult Parse(
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
                MapStudioGeoRoadPoint>();

        foreach (
            var element in
                root.Elements()
                    .Where(
                        item =>
                            item.Name.LocalName ==
                            "node"))
        {
            if (
                !TryLong(
                    element.Attribute(
                        "id")?.Value,
                    out var id) ||
                !TryDouble(
                    element.Attribute(
                        "lat")?.Value,
                    out var latitude) ||
                !TryDouble(
                    element.Attribute(
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
                new MapStudioGeoRoadPoint(
                    latitude,
                    longitude);
        }

        var traces =
            new List<
                MapStudioGeoRoadTrace>();

        var ignoredWays =
            0;

        var missingNodeReferences =
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
                way.Elements()
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
                        StringComparer
                            .OrdinalIgnoreCase)
                    .ToDictionary(
                        group =>
                            group.Key,
                        group =>
                            group.Last()
                                .Value,
                        StringComparer
                            .OrdinalIgnoreCase);

            if (
                !tags.TryGetValue(
                    "highway",
                    out var highway) ||
                string.IsNullOrWhiteSpace(
                    highway))
            {
                ignoredWays++;
                continue;
            }

            var points =
                new List<
                    MapStudioGeoRoadPoint>();

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
                    missingNodeReferences++;
                    continue;
                }

                if (
                    points.Count ==
                        0 ||
                    Math.Abs(
                        points[^1]
                            .Latitude -
                        point.Latitude) >
                        1e-12 ||
                    Math.Abs(
                        points[^1]
                            .Longitude -
                        point.Longitude) >
                        1e-12)
                {
                    points.Add(
                        point);
                }
            }

            if (points.Count < 2)
            {
                ignoredWays++;
                continue;
            }

            var oneWayText =
                tags.GetValueOrDefault(
                    "oneway")
                    ?.Trim();

            var reverse =
                string.Equals(
                    oneWayText,
                    "-1",
                    StringComparison.OrdinalIgnoreCase);

            var explicitlyTwoWay =
                string.Equals(
                    oneWayText,
                    "no",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    oneWayText,
                    "false",
                    StringComparison.OrdinalIgnoreCase) ||
                oneWayText ==
                    "0";

            var junction =
                tags.GetValueOrDefault(
                    "junction")
                    ?.Trim();

            var implicitJunctionOneWay =
                !explicitlyTwoWay &&
                junction is not null &&
                (
                    string.Equals(
                        junction,
                        "roundabout",
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        junction,
                        "circular",
                        StringComparison.OrdinalIgnoreCase)
                );

            var oneWay =
                reverse ||
                IsTrue(
                    oneWayText) ||
                implicitJunctionOneWay;

            if (reverse)
            {
                points.Reverse();
            }

            var forwardLanes =
                TryPositiveInt(
                    tags.GetValueOrDefault(
                        "lanes:forward"));

            var backwardLanes =
                TryPositiveInt(
                    tags.GetValueOrDefault(
                        "lanes:backward"));

            if (reverse)
            {
                (
                    forwardLanes,
                    backwardLanes
                ) =
                    (
                        backwardLanes,
                        forwardLanes
                    );
            }

            var lanes =
                TryPositiveInt(
                    tags.GetValueOrDefault(
                        "lanes")) ??
                SumDirectionalLanes(
                    forwardLanes,
                    backwardLanes);

            if (
                oneWay &&
                lanes is > 0 &&
                forwardLanes is null)
            {
                forwardLanes =
                    lanes;
            }

            var width =
                TryPositiveDouble(
                    tags.GetValueOrDefault(
                        "width"));

            var layer =
                TrySignedInt(
                    tags.GetValueOrDefault(
                        "layer"));

            var bridge =
                IsAffirmativeFeature(
                    tags.GetValueOrDefault(
                        "bridge"));

            var tunnel =
                IsAffirmativeFeature(
                    tags.GetValueOrDefault(
                        "tunnel"));

            var id =
                way.Attribute(
                    "id")?.Value;

            traces.Add(
                new MapStudioGeoRoadTrace(
                    string.IsNullOrWhiteSpace(
                        id)
                        ? "osm-way-" +
                          traces.Count
                        : "osm-way-" +
                          id,
                    points,
                    highway,
                    lanes,
                    oneWayText is null &&
                    !implicitJunctionOneWay
                        ? null
                        : oneWay,
                    width,
                    tags.GetValueOrDefault(
                        "name"),
                    forwardLanes,
                    backwardLanes,
                    layer,
                    bridge,
                    tunnel));
        }

        return new MapStudioOsmRoadImportResult(
            traces,
            ignoredWays,
            missingNodeReferences);
    }

    private static bool IsTrue(
        string? value) =>
        string.Equals(
            value,
            "yes",
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            value,
            "true",
            StringComparison.OrdinalIgnoreCase) ||
        value ==
            "1";

    private static int? SumDirectionalLanes(
        int? forward,
        int? backward)
    {
        if (
            forward is null &&
            backward is null)
        {
            return null;
        }

        return Math.Clamp(
            (
                forward ??
                0
            ) +
            (
                backward ??
                0
            ),
            1,
            32);
    }

    private static int? TrySignedInt(
        string? value)
    {
        if (
            string.IsNullOrWhiteSpace(
                value))
        {
            return null;
        }

        return
            int.TryParse(
                value.Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsed) &&
            parsed is
                >= -20 and <= 20
                ? parsed
                : null;
    }

    private static bool IsAffirmativeFeature(
        string? value)
    {
        if (
            string.IsNullOrWhiteSpace(
                value))
        {
            return false;
        }

        var normalized =
            value.Trim();

        return
            !string.Equals(
                normalized,
                "no",
                StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(
                normalized,
                "false",
                StringComparison.OrdinalIgnoreCase) &&
            normalized !=
                "0";
    }

    private static int? TryPositiveInt(
        string? value)
    {
        if (
            string.IsNullOrWhiteSpace(
                value))
        {
            return null;
        }

        var first =
            value
                .Split(
                    [';', '|'],
                    StringSplitOptions
                        .RemoveEmptyEntries)
                .FirstOrDefault()
                ?.Trim();

        return
            int.TryParse(
                first,
                NumberStyles.Integer,
                CultureInfo
                    .InvariantCulture,
                out var parsed) &&
            parsed is
                > 0 and <= 32
                ? parsed
                : null;
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
                    StringComparison
                        .OrdinalIgnoreCase)
                .Replace(
                    "meter",
                    string.Empty,
                    StringComparison
                        .OrdinalIgnoreCase)
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
                CultureInfo
                    .InvariantCulture,
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
