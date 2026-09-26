using System.Xml.Linq;

namespace MapStudio.Core.Generation.Vegetation;

public sealed record MapStudioOsmVegetationAreaRelationAssemblyResult(
    IReadOnlyList<MapStudioGeoVegetationArea> Areas,
    IReadOnlySet<string> ConsumedWayIds,
    int IgnoredRelationCount,
    int MissingNodeReferenceCount);

public sealed class MapStudioOsmVegetationAreaRelationAssembler
{
    public MapStudioOsmVegetationAreaRelationAssemblyResult Assemble(
        XElement root,
        IReadOnlyDictionary<string, MapStudioGeoVegetationAreaPoint> nodes)
    {
        ArgumentNullException.ThrowIfNull(
            root);

        ArgumentNullException.ThrowIfNull(
            nodes);

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
                                element.Attribute(
                                    "id")?.Value
                        ))
                .Where(
                    item =>
                        !string.IsNullOrWhiteSpace(
                            item.Id))
                .ToDictionary(
                    item =>
                        item.Id!,
                    item =>
                        item.Element.Elements()
                            .Where(
                                child =>
                                    child.Name.LocalName ==
                                    "nd")
                            .Select(
                                child =>
                                    child.Attribute(
                                        "ref")?.Value)
                            .Where(
                                reference =>
                                    !string.IsNullOrWhiteSpace(
                                        reference))
                            .Select(
                                reference =>
                                    reference!)
                            .ToArray(),
                    StringComparer.Ordinal);

        var areas =
            new List<
                MapStudioGeoVegetationArea>();

        var consumedWayIds =
            new HashSet<string>(
                StringComparer.Ordinal);

        var ignoredRelations =
            0;

        var missingReferences =
            0;

        var fallbackIndex =
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
                !TryGetKind(
                    tags,
                    out var kind))
            {
                continue;
            }

            fallbackIndex++;

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
                                    element.Attribute(
                                        "ref")?.Value
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
                            !string.IsNullOrWhiteSpace(
                                member.Ref))
                    .Select(
                        member =>
                            member.Ref!)
                    .Distinct(
                        StringComparer.Ordinal)
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
                    ? $"osm-vegetation-area-relation-{fallbackIndex}"
                    : $"osm-vegetation-area-relation-{relationId}";

            var assembled =
                new List<
                    MapStudioGeoVegetationArea>();

            var relationMissingReferences =
                0;

            for (
                var ringIndex = 0;
                ringIndex <
                    rings.Count;
                ringIndex++)
            {
                var points =
                    new List<
                        MapStudioGeoVegetationAreaPoint>(
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
                        relationMissingReferences++;
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
                    new MapStudioGeoVegetationArea(
                        rings.Count ==
                            1
                            ? baseId
                            : $"{baseId}-part-{ringIndex + 1}",
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

            missingReferences +=
                relationMissingReferences;

            if (
                assembled.Count ==
                    0)
            {
                ignoredRelations++;
                continue;
            }

            areas.AddRange(
                assembled);

            foreach (
                var wayId in
                    outerWayIds)
            {
                consumedWayIds.Add(
                    wayId);
            }
        }

        return new MapStudioOsmVegetationAreaRelationAssemblyResult(
            areas,
            consumedWayIds,
            ignoredRelations,
            missingReferences);
    }

    private static bool TryAssembleRings(
        IReadOnlyList<string> wayIds,
        IReadOnlyDictionary<string, string[]> ways,
        out IReadOnlyList<IReadOnlyList<string>> rings)
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
            new List<
                IReadOnlyList<string>>();

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
                !string.Equals(
                    chain[0],
                    chain[^1],
                    StringComparison.Ordinal))
            {
                if (
                    chain.Count <
                        2)
                {
                    rings =
                        Array.Empty<
                            IReadOnlyList<string>>();

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
                                string.Equals(
                                    candidate[0],
                                    tail,
                                    StringComparison.Ordinal) ||
                                string.Equals(
                                    candidate[^1],
                                    tail,
                                    StringComparison.Ordinal)
                            ));

                if (
                    matchIndex <
                        0)
                {
                    rings =
                        Array.Empty<
                            IReadOnlyList<string>>();

                    return false;
                }

                var next =
                    pending[
                        matchIndex];

                pending.RemoveAt(
                    matchIndex);

                if (
                    string.Equals(
                        next[^1],
                        tail,
                        StringComparison.Ordinal))
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
                        IReadOnlyList<string>>();

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
        IReadOnlyList<MapStudioGeoVegetationAreaPoint> points)
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
        MapStudioGeoVegetationAreaPoint left,
        MapStudioGeoVegetationAreaPoint right) =>
        Math.Abs(
            left.Latitude -
            right.Latitude) <=
            1e-12 &&
        Math.Abs(
            left.Longitude -
            right.Longitude) <=
            1e-12;

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
}
