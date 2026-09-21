using MapStudio.Core.AI;
using MapStudio.Core.Generation.Roads;

namespace MapStudio.Core.Generation.Buildings;

public sealed record MapStudioProjectedBuildingFootprint(
    string Id,
    IReadOnlyList<MapStudioRoadPoint> Points,
    MapStudioRoadPoint Center,
    string BuildingType,
    string? Name,
    int FloorCount,
    double WallHeightMeters,
    MapStudioBuildingRoofType RoofType,
    double RoofHeightMeters,
    string? Street,
    string? HouseNumber);

public sealed class MapStudioOsmBuildingProjector
{
    public IReadOnlyList<MapStudioProjectedBuildingFootprint>
        Project(
            IReadOnlyList<MapStudioOsmBuildingFootprint> buildings,
            MapStudioGeographicAnchor anchor)
    {
        ArgumentNullException.ThrowIfNull(
            buildings);

        var result =
            new List<MapStudioProjectedBuildingFootprint>(
                buildings.Count);

        foreach (var building in buildings)
        {
            var points =
                building.Points
                    .Select(
                        point =>
                            MapStudioGeographicProjection
                                .Project(
                                    anchor,
                                    new MapStudioGeoRoadPoint(
                                        point.Latitude,
                                        point.Longitude)))
                    .ToArray();

            if (
                points.Length < 3 ||
                Math.Abs(
                    SignedArea(
                        points)) <
                    0.01)
            {
                continue;
            }

            var center =
                PolygonCentroid(
                    points);

            var floors =
                building.Levels ??
                InferFloorCount(
                    building.HeightMeters);

            var roofType =
                MapRoofType(
                    building.RoofShape);

            var roofHeight =
                roofType is
                    MapStudioBuildingRoofType.Gable or
                    MapStudioBuildingRoofType.Hip or
                    MapStudioBuildingRoofType.Shed
                    ? building.RoofHeightMeters ??
                      2.0
                    : 0.0;

            var totalHeight =
                building.HeightMeters ??
                Math.Max(
                    3.0,
                    floors *
                    3.0);

            var wallHeight =
                Math.Max(
                    2.2,
                    totalHeight -
                    roofHeight);

            result.Add(
                new MapStudioProjectedBuildingFootprint(
                    building.Id,
                    points,
                    center,
                    building.BuildingType,
                    building.Name,
                    floors,
                    wallHeight,
                    roofType,
                    roofHeight,
                    building.Street,
                    building.HouseNumber));
        }

        return result;
    }

    private static int InferFloorCount(
        double? heightMeters)
    {
        if (
            heightMeters is not { } height ||
            !double.IsFinite(height) ||
            height <= 0)
        {
            return 1;
        }

        return Math.Clamp(
            (int)Math.Round(
                height /
                3.0,
                MidpointRounding.AwayFromZero),
            1,
            300);
    }

    private static MapStudioBuildingRoofType MapRoofType(
        string? roofShape)
    {
        var normalized =
            roofShape
                ?.Trim()
                .ToLowerInvariant();

        return normalized switch
        {
            "gabled" or
            "gable" or
            "pyramidal" =>
                MapStudioBuildingRoofType.Gable,

            "hipped" or
            "hip" or
            "half-hipped" =>
                MapStudioBuildingRoofType.Hip,

            "skillion" or
            "shed" =>
                MapStudioBuildingRoofType.Shed,

            _ =>
                MapStudioBuildingRoofType.Flat
        };
    }

    private static double SignedArea(
        IReadOnlyList<MapStudioRoadPoint> points)
    {
        double twiceArea =
            0;

        for (
            var index = 0;
            index < points.Count;
            index++)
        {
            var next =
                (
                    index +
                    1
                ) %
                points.Count;

            twiceArea +=
                points[index].X *
                    points[next].Z -
                points[next].X *
                    points[index].Z;
        }

        return twiceArea /
            2.0;
    }

    private static MapStudioRoadPoint PolygonCentroid(
        IReadOnlyList<MapStudioRoadPoint> points)
    {
        double twiceArea =
            0;

        double centerX =
            0;

        double centerZ =
            0;

        for (
            var index = 0;
            index < points.Count;
            index++)
        {
            var next =
                (
                    index +
                    1
                ) %
                points.Count;

            var cross =
                points[index].X *
                    points[next].Z -
                points[next].X *
                    points[index].Z;

            twiceArea +=
                cross;

            centerX +=
                (
                    points[index].X +
                    points[next].X
                ) *
                cross;

            centerZ +=
                (
                    points[index].Z +
                    points[next].Z
                ) *
                cross;
        }

        if (
            Math.Abs(
                twiceArea) <
            1e-9)
        {
            return new MapStudioRoadPoint(
                points.Average(
                    point =>
                        point.X),
                points.Average(
                    point =>
                        point.Z));
        }

        return new MapStudioRoadPoint(
            centerX /
                (
                    3.0 *
                    twiceArea
                ),
            centerZ /
                (
                    3.0 *
                    twiceArea
                ));
    }
}
