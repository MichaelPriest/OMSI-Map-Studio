using MapStudio.Core.Generation.Roads;

namespace MapStudio.Core.Generation.Vegetation;

public sealed class MapStudioWorldPolygonScatterer
{
    public IReadOnlyList<
        MapStudioRoadPoint>
        Scatter(
            IReadOnlyList<
                MapStudioRoadPoint>
                polygon,
            double spacingMeters,
            int maxPoints =
                256)
    {
        ArgumentNullException.ThrowIfNull(
            polygon);

        if (
            polygon.Count <
                3)
        {
            throw new ArgumentException(
                "polygonRequiresAtLeastThreePoints",
                nameof(
                    polygon));
        }

        if (
            !double.IsFinite(
                spacingMeters) ||
            spacingMeters <=
                0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(
                    spacingMeters));
        }

        if (maxPoints <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(
                    maxPoints));
        }

        var vertices =
            NormalizePolygon(
                polygon);

        if (
            vertices.Count <
                3)
        {
            return Array.Empty<
                MapStudioRoadPoint>();
        }

        var minX =
            vertices.Min(
                point =>
                    point.X);

        var maxX =
            vertices.Max(
                point =>
                    point.X);

        var minZ =
            vertices.Min(
                point =>
                    point.Z);

        var maxZ =
            vertices.Max(
                point =>
                    point.Z);

        if (
            maxX -
                minX <=
                    1e-6 ||
            maxZ -
                minZ <=
                    1e-6)
        {
            return Array.Empty<
                MapStudioRoadPoint>();
        }

        var result =
            new List<
                MapStudioRoadPoint>();

        var startX =
            minX +
            spacingMeters *
                0.5;

        var startZ =
            minZ +
            spacingMeters *
                0.5;

        for (
            var x = startX;
            x <=
                maxX +
                1e-8;
            x +=
                spacingMeters)
        {
            for (
                var z = startZ;
                z <=
                    maxZ +
                    1e-8;
                z +=
                    spacingMeters)
            {
                var point =
                    new MapStudioRoadPoint(
                        x,
                        z);

                if (
                    !ContainsPoint(
                        vertices,
                        point))
                {
                    continue;
                }

                result.Add(
                    point);

                if (
                    result.Count >=
                        maxPoints)
                {
                    return result;
                }
            }
        }

        if (
            result.Count ==
                0)
        {
            var centroid =
                new MapStudioRoadPoint(
                    vertices.Average(
                        point =>
                            point.X),
                    vertices.Average(
                        point =>
                            point.Z));

            if (
                ContainsPoint(
                    vertices,
                    centroid))
            {
                result.Add(
                    centroid);
            }
        }

        return result;
    }

    private static IReadOnlyList<
        MapStudioRoadPoint>
        NormalizePolygon(
            IReadOnlyList<
                MapStudioRoadPoint>
                polygon)
    {
        var result =
            new List<
                MapStudioRoadPoint>(
                    polygon.Count);

        foreach (
            var point in polygon)
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
                1 &&
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

    private static bool ContainsPoint(
        IReadOnlyList<
            MapStudioRoadPoint>
            polygon,
        MapStudioRoadPoint point)
    {
        var inside =
            false;

        for (
            int current = 0,
                previous =
                    polygon.Count -
                    1;
            current <
                polygon.Count;
            previous =
                current++)
        {
            var a =
                polygon[current];

            var b =
                polygon[previous];

            if (
                PointOnSegment(
                    a,
                    b,
                    point))
            {
                return true;
            }

            var crosses =
                (
                    a.Z >
                    point.Z
                ) !=
                (
                    b.Z >
                    point.Z
                );

            if (!crosses)
            {
                continue;
            }

            var intersectionX =
                (
                    b.X -
                    a.X
                ) *
                (
                    point.Z -
                    a.Z
                ) /
                (
                    b.Z -
                    a.Z
                ) +
                a.X;

            if (
                point.X <
                    intersectionX)
            {
                inside =
                    !inside;
            }
        }

        return inside;
    }

    private static bool PointOnSegment(
        MapStudioRoadPoint a,
        MapStudioRoadPoint b,
        MapStudioRoadPoint point)
    {
        const double epsilon =
            1e-7;

        var cross =
            (
                point.Z -
                a.Z
            ) *
            (
                b.X -
                a.X
            ) -
            (
                point.X -
                a.X
            ) *
            (
                b.Z -
                a.Z
            );

        if (
            Math.Abs(
                cross) >
            epsilon)
        {
            return false;
        }

        var dot =
            (
                point.X -
                a.X
            ) *
            (
                b.X -
                a.X
            ) +
            (
                point.Z -
                a.Z
            ) *
            (
                b.Z -
                a.Z
            );

        if (
            dot <
            -epsilon)
        {
            return false;
        }

        var lengthSquared =
            (
                b.X -
                a.X
            ) *
            (
                b.X -
                a.X
            ) +
            (
                b.Z -
                a.Z
            ) *
            (
                b.Z -
                a.Z
            );

        return
            dot <=
            lengthSquared +
            epsilon;
    }
}
