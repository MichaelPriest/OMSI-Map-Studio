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
                        MapStudioSceneFeatureKind.Tree,
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
                    ? 0.82
                    : 0.78;

        if (line.Points.Count >= 3)
        {
            confidence += 0.04;
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
