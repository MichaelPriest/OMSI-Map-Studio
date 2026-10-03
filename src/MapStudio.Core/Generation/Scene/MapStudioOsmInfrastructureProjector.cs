using MapStudio.Core.Generation.Roads;

namespace MapStudio.Core.Generation.Scene;

public sealed record MapStudioProjectedInfrastructureFeature(
    string Id,
    MapStudioOsmInfrastructureKind Kind,
    IReadOnlyList<MapStudioRoadPoint> Points,
    MapStudioRoadPoint Center,
    string? Name,
    string? Surface,
    double? WidthMeters,
    bool IsArea);

public sealed class MapStudioOsmInfrastructureProjector
{
    public IReadOnlyList<MapStudioProjectedInfrastructureFeature>
        Project(
            IReadOnlyList<MapStudioGeoInfrastructureFeature> features,
            MapStudioGeographicAnchor anchor)
    {
        ArgumentNullException.ThrowIfNull(features);

        var result =
            new List<MapStudioProjectedInfrastructureFeature>(
                features.Count);

        foreach (var feature in features)
        {
            var points =
                NormalizePoints(
                    feature.Points
                        .Select(
                            point =>
                                MapStudioGeographicProjection
                                    .Project(
                                        anchor,
                                        new MapStudioGeoRoadPoint(
                                            point.Latitude,
                                            point.Longitude)))
                        .ToArray());

            var minimumPoints =
                feature.IsArea
                    ? 3
                    : 2;

            if (points.Count < minimumPoints)
            {
                continue;
            }

            if (
                feature.IsArea &&
                Math.Abs(SignedArea(points)) < 0.01)
            {
                continue;
            }

            if (
                !feature.IsArea &&
                PolylineLength(points) < 0.10)
            {
                continue;
            }

            var center =
                feature.IsArea
                    ? PolygonCentroid(points)
                    : new MapStudioRoadPoint(
                        points.Average(point => point.X),
                        points.Average(point => point.Z));

            result.Add(
                new MapStudioProjectedInfrastructureFeature(
                    feature.Id,
                    feature.Kind,
                    points,
                    center,
                    feature.Name,
                    feature.Surface,
                    feature.WidthMeters,
                    feature.IsArea));
        }

        return result;
    }

    private static IReadOnlyList<MapStudioRoadPoint> NormalizePoints(
        IReadOnlyList<MapStudioRoadPoint> points)
    {
        var result =
            new List<MapStudioRoadPoint>(
                points.Count);

        foreach (var point in points)
        {
            if (
                result.Count == 0 ||
                result[^1].DistanceTo(point) > 0.001)
            {
                result.Add(point);
            }
        }

        if (
            result.Count > 2 &&
            result[0].DistanceTo(result[^1]) <= 0.001)
        {
            result.RemoveAt(result.Count - 1);
        }

        return result;
    }

    private static double PolylineLength(
        IReadOnlyList<MapStudioRoadPoint> points)
    {
        double length = 0;

        for (var index = 1; index < points.Count; index++)
        {
            length +=
                points[index - 1]
                    .DistanceTo(points[index]);
        }

        return length;
    }

    private static double SignedArea(
        IReadOnlyList<MapStudioRoadPoint> points)
    {
        double twiceArea = 0;

        for (var index = 0; index < points.Count; index++)
        {
            var next =
                (index + 1) %
                points.Count;

            twiceArea +=
                points[index].X *
                    points[next].Z -
                points[next].X *
                    points[index].Z;
        }

        return twiceArea / 2.0;
    }

    private static MapStudioRoadPoint PolygonCentroid(
        IReadOnlyList<MapStudioRoadPoint> points)
    {
        double twiceArea = 0;
        double centerX = 0;
        double centerZ = 0;

        for (var index = 0; index < points.Count; index++)
        {
            var next =
                (index + 1) %
                points.Count;

            var cross =
                points[index].X *
                    points[next].Z -
                points[next].X *
                    points[index].Z;

            twiceArea += cross;

            centerX +=
                (points[index].X + points[next].X) *
                cross;

            centerZ +=
                (points[index].Z + points[next].Z) *
                cross;
        }

        if (Math.Abs(twiceArea) < 1e-9)
        {
            return new MapStudioRoadPoint(
                points.Average(point => point.X),
                points.Average(point => point.Z));
        }

        return new MapStudioRoadPoint(
            centerX / (3.0 * twiceArea),
            centerZ / (3.0 * twiceArea));
    }
}
