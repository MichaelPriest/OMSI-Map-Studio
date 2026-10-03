using MapStudio.Core.Omsi.Maps;

namespace MapStudio.Core.Generation.Roads;

public static class MapStudioRoadTraceClipper
{
    private const double DefaultBoundaryInsetMeters =
        0.01;

    public static IReadOnlyList<MapStudioRoadTrace>
        ClipToBounds(
            MapStudioRoadTrace trace,
            OmsiTileWorldBounds bounds,
            double boundaryInsetMeters =
                DefaultBoundaryInsetMeters)
    {
        ArgumentNullException.ThrowIfNull(
            trace);

        if (
            trace.Points is null ||
            trace.Points.Count <
                2)
        {
            return Array.Empty<
                MapStudioRoadTrace>();
        }

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

        if (
            !double.IsFinite(
                boundaryInsetMeters) ||
            boundaryInsetMeters <
                0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(
                    boundaryInsetMeters));
        }

        var inset =
            Math.Min(
                boundaryInsetMeters,
                Math.Min(
                    (
                        bounds.MaxX -
                        bounds.MinX
                    ) /
                    4.0,
                    (
                        bounds.MaxZ -
                        bounds.MinZ
                    ) /
                    4.0));

        var minX =
            bounds.MinX +
            inset;

        var minZ =
            bounds.MinZ +
            inset;

        var maxX =
            bounds.MaxX -
            inset;

        var maxZ =
            bounds.MaxZ -
            inset;

        var clippedLines =
            new List<
                IReadOnlyList<
                    MapStudioRoadPoint>>();

        List<MapStudioRoadPoint>?
            current =
                null;

        for (
            var index = 0;
            index <
                trace.Points.Count -
                    1;
            index++)
        {
            var start =
                trace.Points[index];

            var end =
                trace.Points[index + 1];

            if (
                !TryClipSegment(
                    start,
                    end,
                    minX,
                    minZ,
                    maxX,
                    maxZ,
                    out var clippedStart,
                    out var clippedEnd))
            {
                Flush(
                    ref current,
                    clippedLines);

                continue;
            }

            if (
                clippedStart.DistanceTo(
                    clippedEnd) <=
                1e-6)
            {
                continue;
            }

            if (
                current is null ||
                current.Count ==
                    0 ||
                current[^1]
                    .DistanceTo(
                        clippedStart) >
                    0.05)
            {
                Flush(
                    ref current,
                    clippedLines);

                current =
                    [
                        clippedStart,
                        clippedEnd
                    ];

                continue;
            }

            AddIfDistinct(
                current,
                clippedEnd);
        }

        Flush(
            ref current,
            clippedLines);

        if (clippedLines.Count == 0)
        {
            return Array.Empty<
                MapStudioRoadTrace>();
        }

        return clippedLines
            .Select(
                (points, index) =>
                    trace with
                    {
                        Id =
                            clippedLines.Count ==
                                1
                                ? trace.Id
                                : trace.Id +
                                  "-clip-" +
                                  (
                                      index +
                                      1
                                  ),
                        Points =
                            points
                    })
            .ToArray();
    }

    private static void Flush(
        ref List<MapStudioRoadPoint>?
            current,
        ICollection<
            IReadOnlyList<
                MapStudioRoadPoint>>
            destination)
    {
        if (
            current is not null &&
            current.Count >=
                2)
        {
            destination.Add(
                current.ToArray());
        }

        current =
            null;
    }

    private static void AddIfDistinct(
        ICollection<MapStudioRoadPoint>
            points,
        MapStudioRoadPoint point)
    {
        if (
            points is
                List<MapStudioRoadPoint>
                    list &&
            (
                list.Count ==
                    0 ||
                list[^1]
                    .DistanceTo(
                        point) >
                    1e-6
            ))
        {
            list.Add(
                point);
        }
    }

    private static bool TryClipSegment(
        MapStudioRoadPoint start,
        MapStudioRoadPoint end,
        double minX,
        double minZ,
        double maxX,
        double maxZ,
        out MapStudioRoadPoint clippedStart,
        out MapStudioRoadPoint clippedEnd)
    {
        clippedStart =
            default;

        clippedEnd =
            default;

        if (
            !IsFinite(
                start) ||
            !IsFinite(
                end))
        {
            return false;
        }

        var dx =
            end.X -
            start.X;

        var dz =
            end.Z -
            start.Z;

        var t0 =
            0.0;

        var t1 =
            1.0;

        if (
            !ClipTest(
                -dx,
                start.X -
                    minX,
                ref t0,
                ref t1) ||
            !ClipTest(
                dx,
                maxX -
                    start.X,
                ref t0,
                ref t1) ||
            !ClipTest(
                -dz,
                start.Z -
                    minZ,
                ref t0,
                ref t1) ||
            !ClipTest(
                dz,
                maxZ -
                    start.Z,
                ref t0,
                ref t1))
        {
            return false;
        }

        clippedStart =
            MapStudioRoadPoint.Lerp(
                start,
                end,
                t0);

        clippedEnd =
            MapStudioRoadPoint.Lerp(
                start,
                end,
                t1);

        return
            IsFinite(
                clippedStart) &&
            IsFinite(
                clippedEnd);
    }

    private static bool ClipTest(
        double p,
        double q,
        ref double t0,
        ref double t1)
    {
        const double epsilon =
            1e-12;

        if (Math.Abs(p) <= epsilon)
        {
            return q >= 0;
        }

        var r =
            q /
            p;

        if (p < 0)
        {
            if (r > t1)
            {
                return false;
            }

            if (r > t0)
            {
                t0 =
                    r;
            }
        }
        else
        {
            if (r < t0)
            {
                return false;
            }

            if (r < t1)
            {
                t1 =
                    r;
            }
        }

        return true;
    }

    private static bool IsFinite(
        MapStudioRoadPoint point) =>
        double.IsFinite(
            point.X) &&
        double.IsFinite(
            point.Z);
}
