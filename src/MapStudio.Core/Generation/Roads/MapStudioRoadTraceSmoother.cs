namespace MapStudio.Core.Generation.Roads;

public sealed class MapStudioRoadTraceSmoother
{
    private const double Epsilon =
        1e-9;

    public IReadOnlyList<MapStudioRoadTrace> Smooth(
        IReadOnlyList<MapStudioRoadTrace> traces,
        double smoothness = 0.65,
        double maximumSampleSpacingMeters = 8.0,
        int maximumSubdivisionsPerSegment = 16)
    {
        ArgumentNullException.ThrowIfNull(
            traces);

        if (
            !double.IsFinite(
                smoothness) ||
            smoothness < 0 ||
            smoothness > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(smoothness));
        }

        if (
            !double.IsFinite(
                maximumSampleSpacingMeters) ||
            maximumSampleSpacingMeters <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(
                    maximumSampleSpacingMeters));
        }

        if (
            maximumSubdivisionsPerSegment <
                2 ||
            maximumSubdivisionsPerSegment >
                128)
        {
            throw new ArgumentOutOfRangeException(
                nameof(
                    maximumSubdivisionsPerSegment));
        }

        if (
            smoothness <=
            Epsilon)
        {
            return traces
                .Select(
                    trace =>
                        trace with
                        {
                            Points =
                                trace.Points
                                    ?.ToArray() ??
                                Array.Empty<
                                    MapStudioRoadPoint>()
                        })
                .ToArray();
        }

        var result =
            new List<MapStudioRoadTrace>(
                traces.Count);

        foreach (
            var trace in
                traces)
        {
            ArgumentNullException.ThrowIfNull(
                trace);

            if (
                trace.Points is null ||
                trace.Points.Count <
                    3)
            {
                result.Add(
                    trace with
                    {
                        Points =
                            trace.Points
                                ?.ToArray() ??
                            Array.Empty<
                                MapStudioRoadPoint>()
                    });

                continue;
            }

            var points =
                trace.Points;

            var smoothed =
                new List<MapStudioRoadPoint>();

            AddIfDistinct(
                smoothed,
                points[0]);

            for (
                var index = 0;
                index <
                    points.Count - 1;
                index++)
            {
                var p0 =
                    index == 0
                        ? points[index]
                        : points[index - 1];

                var p1 =
                    points[index];

                var p2 =
                    points[index + 1];

                var p3 =
                    index + 2 <
                        points.Count
                        ? points[index + 2]
                        : points[index + 1];

                var segmentLength =
                    p1.DistanceTo(
                        p2);

                if (
                    !double.IsFinite(
                        segmentLength) ||
                    segmentLength <=
                        Epsilon)
                {
                    AddIfDistinct(
                        smoothed,
                        p2);

                    continue;
                }

                var subdivisions =
                    Math.Clamp(
                        (int)Math.Ceiling(
                            segmentLength /
                            maximumSampleSpacingMeters),
                        1,
                        maximumSubdivisionsPerSegment);

                var tangentScale =
                    smoothness *
                    0.5;

                var m1X =
                    (
                        p2.X -
                        p0.X
                    ) *
                    tangentScale;

                var m1Z =
                    (
                        p2.Z -
                        p0.Z
                    ) *
                    tangentScale;

                var m2X =
                    (
                        p3.X -
                        p1.X
                    ) *
                    tangentScale;

                var m2Z =
                    (
                        p3.Z -
                        p1.Z
                    ) *
                    tangentScale;

                for (
                    var step = 1;
                    step <=
                        subdivisions;
                    step++)
                {
                    if (
                        step ==
                        subdivisions)
                    {
                        AddIfDistinct(
                            smoothed,
                            p2);

                        continue;
                    }

                    var t =
                        (double)step /
                        subdivisions;

                    var t2 =
                        t *
                        t;

                    var t3 =
                        t2 *
                        t;

                    var h00 =
                        2 *
                        t3 -
                        3 *
                        t2 +
                        1;

                    var h10 =
                        t3 -
                        2 *
                        t2 +
                        t;

                    var h01 =
                        -2 *
                        t3 +
                        3 *
                        t2;

                    var h11 =
                        t3 -
                        t2;

                    var point =
                        new MapStudioRoadPoint(
                            h00 *
                                p1.X +
                            h10 *
                                m1X +
                            h01 *
                                p2.X +
                            h11 *
                                m2X,
                            h00 *
                                p1.Z +
                            h10 *
                                m1Z +
                            h01 *
                                p2.Z +
                            h11 *
                                m2Z);

                    if (
                        IsFinite(
                            point))
                    {
                        AddIfDistinct(
                            smoothed,
                            point);
                    }
                }
            }

            result.Add(
                trace with
                {
                    Points =
                        smoothed
                });
        }

        return result;
    }

    private static bool IsFinite(
        MapStudioRoadPoint point) =>
        double.IsFinite(
            point.X) &&
        double.IsFinite(
            point.Z);

    private static void AddIfDistinct(
        ICollection<MapStudioRoadPoint> output,
        MapStudioRoadPoint point)
    {
        if (
            output is List<MapStudioRoadPoint>
                list &&
            list.Count >
                0 &&
            list[^1]
                .DistanceTo(
                    point) <=
                Epsilon)
        {
            return;
        }

        output.Add(
            point);
    }
}
