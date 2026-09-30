using MapStudio.Core.AI;
using MapStudio.Core.Generation.Buildings;
using MapStudio.Core.Generation.Roads;

namespace MapStudio.Core.Generation.Scene;

public sealed class MapStudioOsmBuildingReconstructionAdapter
{
    public IReadOnlyList<MapStudioSceneFeatureCandidate>
        BuildCandidates(
            IReadOnlyList<
                MapStudioProjectedBuildingFootprint>
                buildings)
    {
        ArgumentNullException.ThrowIfNull(
            buildings);

        return buildings
            .Select(
                BuildCandidate)
            .ToArray();
    }

    private static MapStudioSceneFeatureCandidate
        BuildCandidate(
            MapStudioProjectedBuildingFootprint building)
    {
        var bounds =
            ResolveBounds(
                building.Points);

        var confidence =
            0.66;

        if (
            building.FloorCount >
            1)
        {
            confidence +=
                0.08;
        }

        if (
            building.WallHeightMeters >
            3.01)
        {
            confidence +=
                0.08;
        }

        if (
            building.RoofType !=
            MapStudioBuildingRoofType.Flat ||
            building.RoofHeightMeters >
            0.01)
        {
            confidence +=
                0.06;
        }

        if (
            !string.IsNullOrWhiteSpace(
                building.BuildingType) &&
            !string.Equals(
                building.BuildingType,
                "yes",
                StringComparison.OrdinalIgnoreCase))
        {
            confidence +=
                0.05;
        }

        if (
            !string.IsNullOrWhiteSpace(
                building.Street) ||
            !string.IsNullOrWhiteSpace(
                building.HouseNumber))
        {
            confidence +=
                0.04;
        }

        confidence =
            Math.Clamp(
                confidence,
                0.0,
                0.97);

        return new MapStudioSceneFeatureCandidate(
            building.Id,
            MapStudioSceneFeatureKind
                .Building,
            [
                new MapStudioSceneEvidence(
                    MapStudioSceneEvidenceSource
                        .Osm,
                    confidence,
                    building.Id,
                    BuildEvidenceNotes(
                        building))
            ],
            SourceBuildingId:
                building.Id,
            WidthMeters:
                bounds.Width,
            HeightMeters:
                building.WallHeightMeters +
                building.RoofHeightMeters,
            DepthMeters:
                bounds.Depth);
    }

    private static (
        double Width,
        double Depth)
        ResolveBounds(
            IReadOnlyList<
                MapStudioRoadPoint> points)
    {
        if (
            points.Count ==
            0)
        {
            return (
                0,
                0);
        }

        var minX =
            points.Min(
                point =>
                    point.X);

        var maxX =
            points.Max(
                point =>
                    point.X);

        var minZ =
            points.Min(
                point =>
                    point.Z);

        var maxZ =
            points.Max(
                point =>
                    point.Z);

        return (
            Math.Max(
                0,
                maxX -
                minX),
            Math.Max(
                0,
                maxZ -
                minZ));
    }

    private static string BuildEvidenceNotes(
        MapStudioProjectedBuildingFootprint building)
    {
        var parts =
            new List<string>
            {
                "footprint"
            };

        if (
            building.FloorCount >
            1)
        {
            parts.Add(
                "levels");
        }

        if (
            building.WallHeightMeters >
            3.01)
        {
            parts.Add(
                "height");
        }

        if (
            building.RoofType !=
            MapStudioBuildingRoofType.Flat ||
            building.RoofHeightMeters >
            0.01)
        {
            parts.Add(
                "roof");
        }

        if (
            !string.IsNullOrWhiteSpace(
                building.Street) ||
            !string.IsNullOrWhiteSpace(
                building.HouseNumber))
        {
            parts.Add(
                "address");
        }

        return string.Join(
            ",",
            parts);
    }
}

public sealed class MapStudioStreetLevelBuildingRefiner
{
    public const double MinimumRefinementConfidence =
        0.82;

    public MapStudioProjectedBuildingFootprint Refine(
        MapStudioProjectedBuildingFootprint building,
        MapStudioBuildingReferenceAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(
            building);

        ArgumentNullException.ThrowIfNull(
            analysis);

        var normalized =
            analysis.Normalize();

        if (
            normalized.Confidence <
            MinimumRefinementConfidence)
        {
            return building;
        }

        var roofType =
            normalized.RoofType switch
            {
                MapStudioBuildingRoofType
                    .Gable =>
                    MapStudioBuildingRoofType
                        .Gable,
                MapStudioBuildingRoofType
                    .Hip =>
                    MapStudioBuildingRoofType
                        .Hip,
                MapStudioBuildingRoofType
                    .Shed =>
                    MapStudioBuildingRoofType
                        .Shed,
                MapStudioBuildingRoofType
                    .Flat =>
                    MapStudioBuildingRoofType
                        .Flat,
                _ =>
                    building.RoofType
            };

        var roofHeight =
            roofType is
                MapStudioBuildingRoofType
                    .Gable or
                MapStudioBuildingRoofType
                    .Hip or
                MapStudioBuildingRoofType
                    .Shed
                ? normalized
                    .RoofHeightMeters ??
                  Math.Max(
                      0.5,
                      building
                          .RoofHeightMeters)
                : 0.0;

        var floorCount =
            normalized
                .FloorCount ??
            building
                .FloorCount;

        var totalHeight =
            normalized
                .HeightMeters ??
            (
                building
                    .WallHeightMeters +
                building
                    .RoofHeightMeters
            );

        var wallHeight =
            Math.Max(
                2.2,
                totalHeight -
                roofHeight);

        return building with
        {
            FloorCount =
                Math.Clamp(
                    floorCount,
                    1,
                    300),
            WallHeightMeters =
                wallHeight,
            RoofType =
                roofType,
            RoofHeightMeters =
                roofHeight,
            FacadeMaterial =
                string.IsNullOrWhiteSpace(
                    normalized.FacadeMaterial)
                    ? building.FacadeMaterial
                    : normalized.FacadeMaterial.Trim(),
            RoofMaterial =
                string.IsNullOrWhiteSpace(
                    normalized.RoofMaterial)
                    ? building.RoofMaterial
                    : normalized.RoofMaterial.Trim()
        };
    }
}
