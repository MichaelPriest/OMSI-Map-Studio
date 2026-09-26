using System.Numerics;

namespace MapStudio.Core.Omsi.Maps;

public static class OmsiTerrainTextureMaskPainter
{
    private const double TileSizeMeters =
        300.0;

    public static OmsiTerrainTexturePaintResult
        PaintCircularBrush(
            OmsiTerrainTextureMaskData source,
            double localX,
            double localY,
            double radiusMeters,
            byte targetAlpha,
            double feather)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        if (
            source.Width <= 0 ||
            source.Height <= 0 ||
            source.AlphaPixels.Length !=
                checked(
                    source.Width *
                    source.Height))
        {
            throw new InvalidDataException(
                "terrainMaskInvalidData");
        }

        if (
            !double.IsFinite(localX) ||
            !double.IsFinite(localY) ||
            !double.IsFinite(radiusMeters) ||
            !double.IsFinite(feather) ||
            radiusMeters <= 0 ||
            feather < 0 ||
            feather > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(radiusMeters));
        }

        var output =
            source.AlphaPixels
                .ToArray();

        var innerRadius =
            radiusMeters *
            (1.0 -
                feather);

        var changed =
            0;

        for (
            var row = 0;
            row < source.Height;
            row++)
        {
            var worldY =
                source.Height == 1
                    ? 0
                    : row /
                        (double)(
                            source.Height -
                            1) *
                        TileSizeMeters;

            for (
                var column = 0;
                column < source.Width;
                column++)
            {
                var worldX =
                    source.Width == 1
                        ? 0
                        : column /
                            (double)(
                                source.Width -
                                1) *
                            TileSizeMeters;

                var dx =
                    worldX -
                    localX;

                var dy =
                    worldY -
                    localY;

                var distance =
                    Math.Sqrt(
                        dx * dx +
                        dy * dy);

                if (
                    distance >
                    radiusMeters)
                {
                    continue;
                }

                double weight;

                if (
                    feather <= 0 ||
                    distance <=
                        innerRadius)
                {
                    weight = 1.0;
                }
                else
                {
                    var denominator =
                        Math.Max(
                            0.000001,
                            radiusMeters -
                            innerRadius);

                    weight =
                        Math.Clamp(
                            (
                                radiusMeters -
                                distance
                            ) /
                            denominator,
                            0,
                            1);
                }

                var index =
                    row *
                    source.Width +
                    column;

                var original =
                    output[index];

                var painted =
                    (byte)Math.Clamp(
                        Math.Round(
                            original +
                            (
                                targetAlpha -
                                original
                            ) *
                            weight),
                        0,
                        255);

                if (
                    painted ==
                    original)
                {
                    continue;
                }

                output[index] =
                    painted;

                changed++;
            }
        }

        return new OmsiTerrainTexturePaintResult(
            new OmsiTerrainTextureMaskData(
                source.Width,
                source.Height,
                output),
            changed);
    }
    public static OmsiTerrainTexturePaintResult
        PaintPolygon(
            OmsiTerrainTextureMaskData source,
            IReadOnlyList<Vector2> polygon,
            byte targetAlpha,
            double edgeFeatherMeters = 0)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        ArgumentNullException.ThrowIfNull(
            polygon);

        if (
            source.Width <= 0 ||
            source.Height <= 0 ||
            source.AlphaPixels.Length !=
                checked(
                    source.Width *
                    source.Height))
        {
            throw new InvalidDataException(
                "terrainMaskInvalidData");
        }

        if (
            polygon.Count < 3 ||
            polygon.Any(
                point =>
                    !float.IsFinite(point.X) ||
                    !float.IsFinite(point.Y)) ||
            !double.IsFinite(
                edgeFeatherMeters) ||
            edgeFeatherMeters < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(polygon));
        }

        var output =
            source.AlphaPixels
                .ToArray();

        var changed =
            0;

        for (
            var row = 0;
            row < source.Height;
            row++)
        {
            var localY =
                source.Height == 1
                    ? 0
                    : row /
                        (double)(
                            source.Height -
                            1) *
                        TileSizeMeters;

            for (
                var column = 0;
                column < source.Width;
                column++)
            {
                var localX =
                    source.Width == 1
                        ? 0
                        : column /
                            (double)(
                                source.Width -
                                1) *
                            TileSizeMeters;

                var point =
                    new Vector2(
                        (float)localX,
                        (float)localY);

                if (!ContainsPoint(
                    polygon,
                    point))
                {
                    continue;
                }

                var weight =
                    edgeFeatherMeters <= 0
                        ? 1.0
                        : Math.Clamp(
                            DistanceToBoundary(
                                polygon,
                                point) /
                            edgeFeatherMeters,
                            0,
                            1);

                var index =
                    row *
                    source.Width +
                    column;

                var original =
                    output[index];

                var painted =
                    (byte)Math.Clamp(
                        Math.Round(
                            original +
                            (
                                targetAlpha -
                                original
                            ) *
                            weight),
                        0,
                        255);

                if (
                    painted ==
                    original)
                {
                    continue;
                }

                output[index] =
                    painted;

                changed++;
            }
        }

        return new OmsiTerrainTexturePaintResult(
            new OmsiTerrainTextureMaskData(
                source.Width,
                source.Height,
                output),
            changed);
    }

    private static bool ContainsPoint(
        IReadOnlyList<Vector2> polygon,
        Vector2 point)
    {
        var inside =
            false;

        for (
            var current = 0,
                previous =
                    polygon.Count -
                    1;
            current < polygon.Count;
            previous = current++)
        {
            var a =
                polygon[current];

            var b =
                polygon[previous];

            if (
                DistanceToSegment(
                    point,
                    a,
                    b) <=
                0.0001f)
            {
                return true;
            }

            var intersects =
                (
                    a.Y >
                    point.Y
                ) !=
                (
                    b.Y >
                    point.Y
                ) &&
                point.X <
                    (
                        b.X -
                        a.X
                    ) *
                    (
                        point.Y -
                        a.Y
                    ) /
                    Math.Max(
                        0.000001f,
                        b.Y -
                        a.Y) +
                    a.X;

            if (intersects)
            {
                inside =
                    !inside;
            }
        }

        return inside;
    }

    private static double DistanceToBoundary(
        IReadOnlyList<Vector2> polygon,
        Vector2 point)
    {
        var best =
            double.PositiveInfinity;

        for (
            var current = 0,
                previous =
                    polygon.Count -
                    1;
            current < polygon.Count;
            previous = current++)
        {
            best =
                Math.Min(
                    best,
                    DistanceToSegment(
                        point,
                        polygon[previous],
                        polygon[current]));
        }

        return best;
    }

    private static float DistanceToSegment(
        Vector2 point,
        Vector2 start,
        Vector2 end)
    {
        var segment =
            end -
            start;

        var lengthSquared =
            segment.LengthSquared();

        if (
            lengthSquared <=
            float.Epsilon)
        {
            return Vector2.Distance(
                point,
                start);
        }

        var amount =
            Math.Clamp(
                Vector2.Dot(
                    point -
                    start,
                    segment) /
                lengthSquared,
                0,
                1);

        return Vector2.Distance(
            point,
            start +
            segment *
            amount);
    }

}
