using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Omsi.Maps;

namespace MapStudio.Core.Generation.Scene;

public static class MapStudioInfrastructureFeatureClipper
{
    private const double BoundaryInsetMeters =
        0.01;

    public static IReadOnlyList<
        MapStudioProjectedInfrastructureFeature>
        ClipToBounds(
            MapStudioProjectedInfrastructureFeature feature,
            OmsiTileWorldBounds bounds)
    {
        ArgumentNullException.ThrowIfNull(
            feature);

        ValidateBounds(
            bounds);

        var clippedBounds =
            InsetBounds(
                bounds);

        if (!feature.IsArea)
        {
            var trace =
                new MapStudioRoadTrace(
                    feature.Id,
                    feature.Points,
                    "__mapstudio_infrastructure_clip__",
                    WidthMeters:
                        feature.WidthMeters);

            var clipped =
                MapStudioRoadTraceClipper
                    .ClipToBounds(
                        trace,
                        clippedBounds,
                        boundaryInsetMeters:
                            0);

            return clipped
                .Select(
                    (item, index) =>
                        feature with
                        {
                            Id =
                                clipped.Count ==
                                    1
                                    ? feature.Id
                                    : feature.Id +
                                      "-clip-" +
                                      (
                                          index +
                                          1
                                      ),
                            Points =
                                item.Points,
                            Center =
                                ResolvePolylineCenter(
                                    item.Points)
                        })
                .ToArray();
        }

        var polygon =
            NormalizePolygon(
                feature.Points);

        if (polygon.Count < 3)
        {
            return Array.Empty<
                MapStudioProjectedInfrastructureFeature>();
        }

        polygon =
            ClipAgainstBoundary(
                polygon,
                point =>
                    point.X >=
                    clippedBounds.MinX,
                (from, to) =>
                    IntersectVertical(
                        from,
                        to,
                        clippedBounds.MinX));

        polygon =
            ClipAgainstBoundary(
                polygon,
                point =>
                    point.X <=
                    clippedBounds.MaxX,
                (from, to) =>
                    IntersectVertical(
                        from,
                        to,
                        clippedBounds.MaxX));

        polygon =
            ClipAgainstBoundary(
                polygon,
                point =>
                    point.Z >=
                    clippedBounds.MinZ,
                (from, to) =>
                    IntersectHorizontal(
                        from,
                        to,
                        clippedBounds.MinZ));

        polygon =
            ClipAgainstBoundary(
                polygon,
                point =>
                    point.Z <=
                    clippedBounds.MaxZ,
                (from, to) =>
                    IntersectHorizontal(
                        from,
                        to,
                        clippedBounds.MaxZ));

        polygon =
            NormalizePolygon(
                polygon);

        if (
            polygon.Count <
                3 ||
            Math.Abs(
                SignedArea(
                    polygon)) <
                0.01)
        {
            return Array.Empty<
                MapStudioProjectedInfrastructureFeature>();
        }

        return
            [
                feature with
                {
                    Points =
                        polygon,
                    Center =
                        ResolvePolygonCentroid(
                            polygon)
                }
            ];
    }

    private static OmsiTileWorldBounds
        InsetBounds(
            OmsiTileWorldBounds bounds)
    {
        var inset =
            Math.Min(
                BoundaryInsetMeters,
                Math.Min(
                    (
                        bounds.MaxX -
                        bounds.MinX
                    ) /
                    4,
                    (
                        bounds.MaxZ -
                        bounds.MinZ
                    ) /
                    4));

        return new OmsiTileWorldBounds(
            bounds.MinX +
                inset,
            bounds.MinZ +
                inset,
            bounds.MaxX -
                inset,
            bounds.MaxZ -
                inset);
    }

    private static List<MapStudioRoadPoint>
        ClipAgainstBoundary(
            IReadOnlyList<MapStudioRoadPoint>
                input,
            Func<MapStudioRoadPoint, bool>
                isInside,
            Func<
                MapStudioRoadPoint,
                MapStudioRoadPoint,
                MapStudioRoadPoint>
                intersect)
    {
        if (input.Count == 0)
        {
            return [];
        }

        var output =
            new List<MapStudioRoadPoint>();

        var previous =
            input[^1];

        var previousInside =
            isInside(
                previous);

        foreach (var current in input)
        {
            var currentInside =
                isInside(
                    current);

            if (currentInside)
            {
                if (!previousInside)
                {
                    output.Add(
                        intersect(
                            previous,
                            current));
                }

                output.Add(
                    current);
            }
            else if (previousInside)
            {
                output.Add(
                    intersect(
                        previous,
                        current));
            }

            previous =
                current;

            previousInside =
                currentInside;
        }

        return output;
    }

    private static MapStudioRoadPoint
        IntersectVertical(
            MapStudioRoadPoint from,
            MapStudioRoadPoint to,
            double x)
    {
        var dx =
            to.X -
            from.X;

        if (Math.Abs(dx) < 1e-12)
        {
            return new MapStudioRoadPoint(
                x,
                from.Z);
        }

        var t =
            Math.Clamp(
                (
                    x -
                    from.X
                ) /
                dx,
                0,
                1);

        return MapStudioRoadPoint.Lerp(
            from,
            to,
            t);
    }

    private static MapStudioRoadPoint
        IntersectHorizontal(
            MapStudioRoadPoint from,
            MapStudioRoadPoint to,
            double z)
    {
        var dz =
            to.Z -
            from.Z;

        if (Math.Abs(dz) < 1e-12)
        {
            return new MapStudioRoadPoint(
                from.X,
                z);
        }

        var t =
            Math.Clamp(
                (
                    z -
                    from.Z
                ) /
                dz,
                0,
                1);

        return MapStudioRoadPoint.Lerp(
            from,
            to,
            t);
    }

    private static IReadOnlyList<MapStudioRoadPoint>
        NormalizePolygon(
            IReadOnlyList<MapStudioRoadPoint>
                source)
    {
        var result =
            new List<MapStudioRoadPoint>(
                source.Count);

        foreach (var point in source)
        {
            if (
                !double.IsFinite(
                    point.X) ||
                !double.IsFinite(
                    point.Z))
            {
                continue;
            }

            if (
                result.Count ==
                    0 ||
                result[^1]
                    .DistanceTo(
                        point) >
                    1e-6)
            {
                result.Add(
                    point);
            }
        }

        if (
            result.Count >
                2 &&
            result[0]
                .DistanceTo(
                    result[^1]) <=
                1e-6)
        {
            result.RemoveAt(
                result.Count -
                    1);
        }

        return result;
    }

    private static MapStudioRoadPoint
        ResolvePolylineCenter(
            IReadOnlyList<MapStudioRoadPoint>
                points) =>
        new(
            points.Average(
                point =>
                    point.X),
            points.Average(
                point =>
                    point.Z));

    private static MapStudioRoadPoint
        ResolvePolygonCentroid(
            IReadOnlyList<MapStudioRoadPoint>
                points)
    {
        var areaFactor =
            0.0;

        var x =
            0.0;

        var z =
            0.0;

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

            var cross =
                points[index].X *
                    points[next].Z -
                points[next].X *
                    points[index].Z;

            areaFactor +=
                cross;

            x +=
                (
                    points[index].X +
                    points[next].X
                ) *
                cross;

            z +=
                (
                    points[index].Z +
                    points[next].Z
                ) *
                cross;
        }

        if (Math.Abs(areaFactor) < 1e-9)
        {
            return ResolvePolylineCenter(
                points);
        }

        return new MapStudioRoadPoint(
            x /
                (
                    3 *
                    areaFactor
                ),
            z /
                (
                    3 *
                    areaFactor
                ));
    }

    private static double SignedArea(
        IReadOnlyList<MapStudioRoadPoint>
            points)
    {
        var sum =
            0.0;

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

            sum +=
                points[index].X *
                    points[next].Z -
                points[next].X *
                    points[index].Z;
        }

        return sum /
            2.0;
    }

    private static void ValidateBounds(
        OmsiTileWorldBounds bounds)
    {
        if (
            !double.IsFinite(
                bounds.MinX) ||
            !double.IsFinite(
                bounds.MinZ) ||
            !double.IsFinite(
                bounds.MaxX) ||
            !double.IsFinite(
                bounds.MaxZ) ||
            bounds.MaxX <=
                bounds.MinX ||
            bounds.MaxZ <=
                bounds.MinZ)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bounds));
        }
    }
}
