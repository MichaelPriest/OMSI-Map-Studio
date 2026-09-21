using MapStudio.Core.Generation.Roads;

namespace MapStudio.Core.Generation.Vegetation;

public sealed class MapStudioVegetationAreaScatterer
{
    public IReadOnlyList<
        MapStudioProjectedVegetationPoint>
        ProjectAndScatter(
            IReadOnlyList<
                MapStudioGeoVegetationArea>
                areas,
            MapStudioGeographicAnchor anchor,
            double forestSpacingMeters = 14.0,
            double scrubSpacingMeters = 7.0,
            int maxPoints = 10_000)
    {
        ArgumentNullException.ThrowIfNull(
            areas);

        ValidateSpacing(
            forestSpacingMeters,
            nameof(
                forestSpacingMeters));

        ValidateSpacing(
            scrubSpacingMeters,
            nameof(
                scrubSpacingMeters));

        if (maxPoints <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(
                    maxPoints));
        }

        var result =
            new List<
                MapStudioProjectedVegetationPoint>();

        foreach (
            var area in areas)
        {
            if (
                area.Points.Count <
                    3)
            {
                continue;
            }

            var polygon =
                area.Points
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
                polygon.Length <
                    3)
            {
                continue;
            }

            var spacing =
                area.Kind ==
                    MapStudioOsmVegetationAreaKind
                        .Forest
                    ? forestSpacingMeters
                    : scrubSpacingMeters;

            var minX =
                polygon.Min(
                    point =>
                        point.X);

            var maxX =
                polygon.Max(
                    point =>
                        point.X);

            var minZ =
                polygon.Min(
                    point =>
                        point.Z);

            var maxZ =
                polygon.Max(
                    point =>
                        point.Z);

            if (
                !double.IsFinite(
                    minX) ||
                !double.IsFinite(
                    maxX) ||
                !double.IsFinite(
                    minZ) ||
                !double.IsFinite(
                    maxZ) ||
                maxX - minX <=
                    1e-6 ||
                maxZ - minZ <=
                    1e-6)
            {
                continue;
            }

            var offsetX =
                StableUnit(
                    area.Id +
                    "|x") *
                spacing;

            var offsetZ =
                StableUnit(
                    area.Id +
                    "|z") *
                spacing;

            var sampleIndex =
                0;

            for (
                var x =
                    minX +
                    offsetX;
                x <=
                    maxX +
                    1e-8;
                x +=
                    spacing)
            {
                for (
                    var z =
                        minZ +
                        offsetZ;
                    z <=
                        maxZ +
                        1e-8;
                    z +=
                        spacing)
                {
                    var point =
                        new MapStudioRoadPoint(
                            x,
                            z);

                    if (
                        !ContainsPoint(
                            polygon,
                            point))
                    {
                        continue;
                    }

                    result.Add(
                        new MapStudioProjectedVegetationPoint(
                            area.Id +
                            "-scatter-" +
                            sampleIndex++,
                            point,
                            area.Kind ==
                                MapStudioOsmVegetationAreaKind
                                    .Forest
                                ? MapStudioOsmVegetationKind
                                    .Tree
                                : MapStudioOsmVegetationKind
                                    .Shrub,
                            area.Species,
                            area.Genus,
                            area.LeafType,
                            area.Name));

                    if (
                        result.Count >=
                            maxPoints)
                    {
                        return result;
                    }
                }
            }
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
            var current = 0,
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

    private static void ValidateSpacing(
        double value,
        string parameterName)
    {
        if (
            !double.IsFinite(
                value) ||
            value <=
                0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName);
        }
    }

    private static double StableUnit(
        string value)
    {
        unchecked
        {
            uint hash =
                2166136261;

            foreach (
                var character in value)
            {
                hash ^=
                    character;

                hash *=
                    16777619;
            }

            return
                hash /
                (
                    (double)uint.MaxValue +
                    1.0
                );
        }
    }
}
