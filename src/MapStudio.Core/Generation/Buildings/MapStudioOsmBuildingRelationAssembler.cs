using System.Globalization;
using System.Xml.Linq;

namespace MapStudio.Core.Generation.Buildings;

public sealed record MapStudioOsmBuildingRelationAssemblyResult(
    IReadOnlyList<MapStudioOsmBuildingFootprint> Buildings,
    IReadOnlySet<long> ConsumedWayIds,
    int IgnoredRelationCount,
    int MissingNodeReferenceCount);

public sealed class MapStudioOsmBuildingRelationAssembler
{
    public MapStudioOsmBuildingRelationAssemblyResult Assemble(
        XElement root,
        IReadOnlyDictionary<long, MapStudioGeoBuildingPoint> nodes)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(nodes);

        var ways =
            root.Elements()
                .Where(
                    element =>
                        element.Name.LocalName ==
                        "way")
                .Select(
                    element =>
                        (
                            Element:
                                element,
                            Id:
                                TryLong(
                                    element.Attribute(
                                        "id")?.Value,
                                    out var id)
                                    ? id
                                    : (long?)null
                        ))
                .Where(
                    item =>
                        item.Id is not null)
                .ToDictionary(
                    item =>
                        item.Id!.Value,
                    item =>
                        item.Element.Elements()
                            .Where(
                                child =>
                                    child.Name.LocalName ==
                                    "nd")
                            .Select(
                                child =>
                                    TryLong(
                                        child.Attribute(
                                            "ref")?.Value,
                                        out var nodeId)
                                        ? nodeId
                                        : (long?)null)
                            .Where(
                                nodeId =>
                                    nodeId is not null)
                            .Select(
                                nodeId =>
                                    nodeId!.Value)
                            .ToArray());

        var buildings =
            new List<MapStudioOsmBuildingFootprint>();

        var consumedWayIds =
            new HashSet<long>();

        var ignoredRelations =
            0;

        var missingNodeReferences =
            0;

        var fallbackRelationIndex =
            0;

        foreach (
            var relation in
                root.Elements()
                    .Where(
                        element =>
                            element.Name.LocalName ==
                            "relation"))
        {
            var tags =
                ReadTags(
                    relation);

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
                continue;
            }

            fallbackRelationIndex++;

            if (
                !string.Equals(
                    tags.GetValueOrDefault(
                        "type"),
                    "multipolygon",
                    StringComparison.OrdinalIgnoreCase))
            {
                ignoredRelations++;
                continue;
            }

            var members =
                relation.Elements()
                    .Where(
                        element =>
                            element.Name.LocalName ==
                            "member")
                    .Select(
                        element =>
                            (
                                Type:
                                    element.Attribute(
                                        "type")?.Value,
                                Role:
                                    element.Attribute(
                                        "role")?.Value,
                                Ref:
                                    TryLong(
                                        element.Attribute(
                                            "ref")?.Value,
                                        out var memberId)
                                        ? memberId
                                        : (long?)null
                            ))
                    .ToArray();

            if (
                members.Any(
                    member =>
                        string.Equals(
                            member.Role,
                            "inner",
                            StringComparison.OrdinalIgnoreCase)))
            {
                ignoredRelations++;
                continue;
            }

            var outerWayIds =
                members
                    .Where(
                        member =>
                            string.Equals(
                                member.Type,
                                "way",
                                StringComparison.OrdinalIgnoreCase) &&
                            (
                                string.IsNullOrWhiteSpace(
                                    member.Role) ||
                                string.Equals(
                                    member.Role,
                                    "outer",
                                    StringComparison.OrdinalIgnoreCase)
                            ) &&
                            member.Ref is not null)
                    .Select(
                        member =>
                            member.Ref!.Value)
                    .Distinct()
                    .ToArray();

            if (
                outerWayIds.Length ==
                    0 ||
                outerWayIds.Any(
                    wayId =>
                        !ways.ContainsKey(
                            wayId)) ||
                !TryAssembleRings(
                    outerWayIds,
                    ways,
                    out var rings))
            {
                ignoredRelations++;
                continue;
            }

            var relationId =
                relation.Attribute(
                    "id")?.Value;

            var baseId =
                string.IsNullOrWhiteSpace(
                    relationId)
                    ? $"osm-building-relation-{fallbackRelationIndex}"
                    : $"osm-building-relation-{relationId}";

            var assembled =
                new List<MapStudioOsmBuildingFootprint>();

            var relationMissingNodes =
                0;

            for (
                var ringIndex = 0;
                ringIndex <
                    rings.Count;
                ringIndex++)
            {
                var points =
                    new List<MapStudioGeoBuildingPoint>(
                        rings[ringIndex].Count);

                var valid =
                    true;

                foreach (
                    var nodeId in
                        rings[ringIndex])
                {
                    if (
                        !nodes.TryGetValue(
                            nodeId,
                            out var point))
                    {
                        relationMissingNodes++;
                        valid =
                            false;
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
                    !valid ||
                    points.Count <
                        3 ||
                    Math.Abs(
                        SignedArea(
                            points)) <
                        1e-16)
                {
                    assembled.Clear();
                    break;
                }

                assembled.Add(
                    new MapStudioOsmBuildingFootprint(
                        rings.Count ==
                            1
                            ? baseId
                            : $"{baseId}-part-{ringIndex + 1}",
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

            missingNodeReferences +=
                relationMissingNodes;

            if (
                assembled.Count ==
                0)
            {
                ignoredRelations++;
                continue;
            }

            buildings.AddRange(
                assembled);

            foreach (
                var wayId in
                    outerWayIds)
            {
                consumedWayIds.Add(
                    wayId);
            }
        }

        return new MapStudioOsmBuildingRelationAssemblyResult(
            buildings,
            consumedWayIds,
            ignoredRelations,
            missingNodeReferences);
    }

    private static bool TryAssembleRings(
        IReadOnlyList<long> wayIds,
        IReadOnlyDictionary<long, long[]> ways,
        out IReadOnlyList<IReadOnlyList<long>> rings)
    {
        var pending =
            wayIds
                .Select(
                    wayId =>
                        ways[wayId]
                            .ToList())
                .Where(
                    nodes =>
                        nodes.Count >=
                        2)
                .ToList();

        var output =
            new List<IReadOnlyList<long>>();

        while (
            pending.Count >
            0)
        {
            var chain =
                pending[0];

            pending.RemoveAt(
                0);

            while (
                chain.Count <
                    2 ||
                chain[0] !=
                    chain[^1])
            {
                if (
                    chain.Count <
                    2)
                {
                    rings =
                        Array.Empty<
                            IReadOnlyList<long>>();

                    return false;
                }

                var tail =
                    chain[^1];

                var matchIndex =
                    pending.FindIndex(
                        candidate =>
                            candidate.Count >=
                                2 &&
                            (
                                candidate[0] ==
                                    tail ||
                                candidate[^1] ==
                                    tail
                            ));

                if (
                    matchIndex <
                    0)
                {
                    rings =
                        Array.Empty<
                            IReadOnlyList<long>>();

                    return false;
                }

                var next =
                    pending[
                        matchIndex];

                pending.RemoveAt(
                    matchIndex);

                if (
                    next[^1] ==
                    tail)
                {
                    next.Reverse();
                }

                chain.AddRange(
                    next.Skip(
                        1));
            }

            if (
                chain.Count <
                    4)
            {
                rings =
                    Array.Empty<
                        IReadOnlyList<long>>();

                return false;
            }

            chain.RemoveAt(
                chain.Count -
                1);

            output.Add(
                chain);
        }

        rings =
            output;

        return
            output.Count >
            0;
    }

    private static Dictionary<string, string?>
        ReadTags(
            XElement element) =>
        element.Elements()
            .Where(
                child =>
                    child.Name.LocalName ==
                    "tag")
            .Select(
                child =>
                    (
                        Key:
                            child.Attribute(
                                "k")?.Value,
                        Value:
                            child.Attribute(
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
        IReadOnlyList<MapStudioGeoBuildingPoint> points)
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

        return
            area /
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
        string? value) =>
        int.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsed) &&
        parsed is
            > 0 and <= 300
            ? parsed
            : null;

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
}
