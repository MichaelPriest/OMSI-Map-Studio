using MapStudio.Core.Generation.Vegetation;

namespace MapStudio.Core.Generation.Scene;

public sealed class MapStudioOsmVegetationReconstructionAdapter
{
    public IReadOnlyList<MapStudioSceneFeatureCandidate>
        BuildPointCandidates(
            IReadOnlyList<MapStudioGeoVegetationPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        return points
            .Select(
                point =>
                    new MapStudioSceneFeatureCandidate(
                        point.Id,
                        point.Kind ==
                            MapStudioOsmVegetationKind.Tree
                                ? MapStudioSceneFeatureKind.Tree
                                : MapStudioSceneFeatureKind.Shrub,
                        [
                            new MapStudioSceneEvidence(
                                MapStudioSceneEvidenceSource.Osm,
                                ResolvePointConfidence(point),
                                point.Id,
                                BuildPointNotes(point))
                        ]))
            .ToArray();
    }

    public IReadOnlyList<MapStudioSceneFeatureCandidate>
        BuildLineCandidates(
            IReadOnlyList<MapStudioGeoVegetationLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        return lines
            .Select(
                line =>
                    new MapStudioSceneFeatureCandidate(
                        line.Id,
                        line.Kind ==
                            MapStudioOsmVegetationLineKind.Hedge
                                ? MapStudioSceneFeatureKind.Fence
                                : MapStudioSceneFeatureKind.Tree,
                        [
                            new MapStudioSceneEvidence(
                                MapStudioSceneEvidenceSource.Osm,
                                ResolveLineConfidence(line),
                                line.Id,
                                BuildLineNotes(line))
                        ]))
            .ToArray();
    }

    public IReadOnlyList<MapStudioSceneFeatureCandidate>
        BuildAreaCandidates(
            IReadOnlyList<MapStudioGeoVegetationArea> areas)
    {
        ArgumentNullException.ThrowIfNull(areas);

        return areas
            .Select(
                area =>
                    new MapStudioSceneFeatureCandidate(
                        area.Id,
                        area.Kind ==
                            MapStudioOsmVegetationAreaKind.Forest
                                ? MapStudioSceneFeatureKind.Tree
                                : MapStudioSceneFeatureKind.Shrub,
                        [
                            new MapStudioSceneEvidence(
                                MapStudioSceneEvidenceSource.Osm,
                                ResolveAreaConfidence(area),
                                area.Id,
                                BuildAreaNotes(area))
                        ]))
            .ToArray();
    }

    private static double ResolvePointConfidence(
        MapStudioGeoVegetationPoint point)
    {
        var confidence =
            point.Kind ==
                MapStudioOsmVegetationKind.Tree
                    ? 0.80
                    : 0.72;

        if (!string.IsNullOrWhiteSpace(point.Species))
        {
            confidence += 0.08;
        }

        if (!string.IsNullOrWhiteSpace(point.Genus))
        {
            confidence += 0.04;
        }

        if (!string.IsNullOrWhiteSpace(point.LeafType))
        {
            confidence += 0.03;
        }

        if (!string.IsNullOrWhiteSpace(point.Name))
        {
            confidence += 0.02;
        }

        return Math.Clamp(
            confidence,
            0.0,
            0.97);
    }

    private static double ResolveLineConfidence(
        MapStudioGeoVegetationLine line)
    {
        var confidence =
            line.Kind ==
                MapStudioOsmVegetationLineKind.TreeRow
                    ? 0.94
                    : 0.92;

        if (line.Points.Count >= 3)
        {
            confidence += 0.02;
        }

        if (!string.IsNullOrWhiteSpace(line.Species))
        {
            confidence += 0.05;
        }

        if (!string.IsNullOrWhiteSpace(line.Genus))
        {
            confidence += 0.03;
        }

        if (!string.IsNullOrWhiteSpace(line.LeafType))
        {
            confidence += 0.02;
        }

        return Math.Clamp(
            confidence,
            0.0,
            0.97);
    }

    private static double ResolveAreaConfidence(
        MapStudioGeoVegetationArea area)
    {
        var confidence =
            area.Kind ==
                MapStudioOsmVegetationAreaKind.Forest
                    ? 0.96
                    : 0.94;

        if (area.Points.Count >= 4)
        {
            confidence += 0.01;
        }

        if (!string.IsNullOrWhiteSpace(area.Species))
        {
            confidence += 0.02;
        }

        if (!string.IsNullOrWhiteSpace(area.Genus))
        {
            confidence += 0.01;
        }

        return Math.Clamp(
            confidence,
            0.0,
            0.99);
    }

    private static string BuildAreaNotes(
        MapStudioGeoVegetationArea area)
    {
        var parts =
            new List<string>
            {
                area.Kind ==
                    MapStudioOsmVegetationAreaKind.Forest
                        ? "forest"
                        : "scrub",
                "polygon"
            };

        if (!string.IsNullOrWhiteSpace(area.Species))
        {
            parts.Add("species");
        }

        if (!string.IsNullOrWhiteSpace(area.Genus))
        {
            parts.Add("genus");
        }

        if (!string.IsNullOrWhiteSpace(area.LeafType))
        {
            parts.Add("leaf_type");
        }

        return string.Join(",", parts);
    }

    private static string BuildPointNotes(
        MapStudioGeoVegetationPoint point)
    {
        var parts =
            new List<string>
            {
                point.Kind ==
                    MapStudioOsmVegetationKind.Tree
                        ? "tree"
                        : "shrub"
            };

        if (!string.IsNullOrWhiteSpace(point.Species))
        {
            parts.Add("species");
        }

        if (!string.IsNullOrWhiteSpace(point.Genus))
        {
            parts.Add("genus");
        }

        if (!string.IsNullOrWhiteSpace(point.LeafType))
        {
            parts.Add("leaf_type");
        }

        return string.Join(",", parts);
    }

    private static string BuildLineNotes(
        MapStudioGeoVegetationLine line)
    {
        var parts =
            new List<string>
            {
                line.Kind ==
                    MapStudioOsmVegetationLineKind.Hedge
                        ? "hedge"
                        : "tree_row"
            };

        if (line.Points.Count >= 3)
        {
            parts.Add("geometry");
        }

        if (!string.IsNullOrWhiteSpace(line.Species))
        {
            parts.Add("species");
        }

        if (!string.IsNullOrWhiteSpace(line.Genus))
        {
            parts.Add("genus");
        }

        return string.Join(",", parts);
    }
}
