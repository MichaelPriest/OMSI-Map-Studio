namespace MapStudio.Renderer.Viewport;

public sealed record NativeSceneryPatternPlacement(
    double WorldX,
    double WorldZ,
    double Z,
    double Rotation,
    double Pitch,
    double Bank);

public static class NativeSceneryPlacementPatternBuilder
{
    private const double GoldenAngleDegrees =
        137.507764;

    public static IReadOnlyList<
        NativeSceneryPatternPlacement>
        BuildLine(
            NativeSceneryPlacementRequest start,
            NativeSceneryPlacementRequest end,
            double spacing,
            bool randomRotation)
    {
        if (!IsFiniteRequest(start) || !IsFiniteRequest(end))
        {
            return Array.Empty<NativeSceneryPatternPlacement>();
        }

        var dx =
            end.WorldPoint.X -
            start.WorldPoint.X;

        var dz =
            end.WorldPoint.Z -
            start.WorldPoint.Z;

        var distance =
            Math.Sqrt(
                dx * dx +
                dz * dz);

        var safeSpacing =
            double.IsFinite(spacing)
                ? Math.Max(
                    0.5,
                    spacing)
                : 0.5;

        if (!double.IsFinite(distance))
        {
            return Array.Empty<
                NativeSceneryPatternPlacement>();
        }

        // Cap before converting to int so extreme world coordinates
        // cannot overflow the segment count.
        var requestedSegments =
            Math.Ceiling(
                distance /
                safeSpacing);

        var count =
            requestedSegments >= 255
                ? 256
                : Math.Max(
                    2,
                    (int)requestedSegments + 1);

        var heading =
            Math.Atan2(
                dx,
                dz) *
            180.0 /
            Math.PI;

        var result =
            new List<
                NativeSceneryPatternPlacement>(
                    count);

        for (
            var index = 0;
            index < count;
            index++)
        {
            var t =
                count <= 1
                    ? 0
                    : (double)index /
                        (count - 1);

            result.Add(
                new NativeSceneryPatternPlacement(
                    start.WorldPoint.X +
                        dx * t,
                    start.WorldPoint.Z +
                        dz * t,
                    start.Z,
                    randomRotation
                        ? NormalizeDegrees(
                            heading +
                            index *
                            GoldenAngleDegrees)
                        : heading,
                    start.Pitch,
                    start.Bank));
        }

        return result.FindAll(IsFinitePlacement);
    }

    public static IReadOnlyList<
        NativeSceneryPatternPlacement>
        BuildArea(
            NativeSceneryPlacementRequest center,
            double radius,
            int count,
            bool randomRotation)
    {
        if (!IsFiniteRequest(center))
        {
            return Array.Empty<NativeSceneryPatternPlacement>();
        }

        var safeRadius =
            double.IsFinite(radius)
                ? Math.Max(1, radius)
                : 1;

        var safeCount =
            Math.Clamp(
                count,
                1,
                256);

        var seed =
            (
                center.WorldPoint.X +
                center.WorldPoint.Z
            ) %
            17.0;

        var result =
            new List<
                NativeSceneryPatternPlacement>(
                    safeCount);

        for (
            var index = 0;
            index < safeCount;
            index++)
        {
            var progress =
                (
                    index +
                    0.5
                ) /
                safeCount;

            var radial =
                safeRadius *
                Math.Sqrt(
                    progress);

            var angle =
                index *
                    2.399963229728653 +
                seed *
                    0.07;

            result.Add(
                new NativeSceneryPatternPlacement(
                    center.WorldPoint.X +
                        Math.Cos(angle) *
                        radial,
                    center.WorldPoint.Z +
                        Math.Sin(angle) *
                        radial,
                    center.Z,
                    randomRotation
                        ? NormalizeDegrees(
                            angle *
                            180.0 /
                            Math.PI)
                        : center.Rotation,
                    center.Pitch,
                    center.Bank));
        }

        return result.FindAll(IsFinitePlacement);
    }

    public static IReadOnlyList<
        NativeSceneryPatternPlacement>
        BuildMatrix(
            NativeSceneryPlacementRequest center,
            int rows,
            int columns,
            double spacingX,
            double spacingZ,
            bool randomRotation)
    {
        if (!IsFiniteRequest(center))
        {
            return Array.Empty<NativeSceneryPatternPlacement>();
        }

        var safeRows =
            Math.Clamp(
                rows,
                1,
                16);

        var safeColumns =
            Math.Clamp(
                columns,
                1,
                16);

        var sx =
            double.IsFinite(spacingX)
                ? Math.Max(0.5, spacingX)
                : 0.5;

        var sz =
            double.IsFinite(spacingZ)
                ? Math.Max(0.5, spacingZ)
                : 0.5;

        var total =
            Math.Min(
                256,
                safeRows *
                safeColumns);

        var result =
            new List<
                NativeSceneryPatternPlacement>(
                    total);

        for (
            var index = 0;
            index < total;
            index++)
        {
            var row =
                index /
                safeColumns;

            var column =
                index %
                safeColumns;

            var offsetX =
                (
                    column -
                    (
                        safeColumns -
                        1
                    ) /
                    2.0
                ) *
                sx;

            var offsetZ =
                (
                    row -
                    (
                        safeRows -
                        1
                    ) /
                    2.0
                ) *
                sz;

            result.Add(
                new NativeSceneryPatternPlacement(
                    center.WorldPoint.X +
                        offsetX,
                    center.WorldPoint.Z +
                        offsetZ,
                    center.Z,
                    randomRotation
                        ? NormalizeDegrees(
                            center.Rotation +
                            index *
                            GoldenAngleDegrees)
                        : center.Rotation,
                    center.Pitch,
                    center.Bank));
        }

        return result.FindAll(IsFinitePlacement);
    }

    public static IReadOnlyList<
        NativeSceneryPatternPlacement>
        BuildCircle(
            NativeSceneryPlacementRequest center,
            double radius,
            int count,
            bool tangentRotation,
            bool randomRotation)
    {
        if (!IsFiniteRequest(center))
        {
            return Array.Empty<NativeSceneryPatternPlacement>();
        }

        var safeRadius =
            double.IsFinite(radius)
                ? Math.Max(0.5, radius)
                : 0.5;

        var safeCount =
            Math.Clamp(
                count,
                1,
                256);

        var result =
            new List<
                NativeSceneryPatternPlacement>(
                    safeCount);

        for (
            var index = 0;
            index < safeCount;
            index++)
        {
            var angle =
                (
                    (double)index /
                    safeCount
                ) *
                Math.PI *
                2.0;

            var baseRotation =
                tangentRotation
                    ? NormalizeDegrees(
                        angle *
                        180.0 /
                        Math.PI +
                        90.0)
                    : center.Rotation;

            result.Add(
                new NativeSceneryPatternPlacement(
                    center.WorldPoint.X +
                        Math.Cos(angle) *
                        safeRadius,
                    center.WorldPoint.Z +
                        Math.Sin(angle) *
                        safeRadius,
                    center.Z,
                    randomRotation
                        ? NormalizeDegrees(
                            baseRotation +
                            index *
                            GoldenAngleDegrees)
                        : baseRotation,
                    center.Pitch,
                    center.Bank));
        }

        return result.FindAll(IsFinitePlacement);
    }

    public static IReadOnlyList<
        NativeSceneryPatternPlacement>
        BuildLot(
            NativeSceneryPlacementRequest start,
            NativeSceneryPlacementRequest end,
            double spacing,
            double setback,
            bool randomRotation)
    {
        if (!IsFiniteRequest(start) || !IsFiniteRequest(end))
        {
            return Array.Empty<NativeSceneryPatternPlacement>();
        }

        var dx =
            end.WorldPoint.X -
            start.WorldPoint.X;

        var dz =
            end.WorldPoint.Z -
            start.WorldPoint.Z;

        var distance =
            Math.Sqrt(
                dx * dx +
                dz * dz);

        if (!double.IsFinite(distance))
        {
            return Array.Empty<NativeSceneryPatternPlacement>();
        }

        if (distance < 0.5)
        {
            return
                [
                    new NativeSceneryPatternPlacement(
                        start.WorldPoint.X,
                        start.WorldPoint.Z,
                        start.Z,
                        start.Rotation,
                        start.Pitch,
                        start.Bank)
                ];
        }

        var safeSpacing =
            double.IsFinite(spacing)
                ? Math.Max(1, spacing)
                : 1;

        var requestedCount =
            Math.Floor(distance / safeSpacing) + 1;

        var count =
            requestedCount >= 256
                ? 256
                : Math.Clamp((int)requestedCount, 1, 256);

        var safeSetback =
            double.IsFinite(setback)
                ? setback
                : 0;

        var normalX =
            -dz /
            distance;

        var normalZ =
            dx /
            distance;

        var heading =
            Math.Atan2(
                dx,
                dz) *
            180.0 /
            Math.PI;

        var result =
            new List<
                NativeSceneryPatternPlacement>(
                    count);

        for (
            var index = 0;
            index < count;
            index++)
        {
            var t =
                count == 1
                    ? 0
                    : (double)index /
                        (
                            count -
                            1
                        );

            result.Add(
                new NativeSceneryPatternPlacement(
                    start.WorldPoint.X +
                        dx * t +
                        normalX *
                        safeSetback,
                    start.WorldPoint.Z +
                        dz * t +
                        normalZ *
                        safeSetback,
                    start.Z,
                    randomRotation
                        ? NormalizeDegrees(
                            heading +
                            index *
                            GoldenAngleDegrees)
                        : heading,
                    start.Pitch,
                    start.Bank));
        }

        return result.FindAll(IsFinitePlacement);
    }

    private static bool IsFinitePlacement(
        NativeSceneryPatternPlacement placement) =>
        double.IsFinite(placement.WorldX) &&
        double.IsFinite(placement.WorldZ) &&
        double.IsFinite(placement.Z) &&
        double.IsFinite(placement.Rotation) &&
        double.IsFinite(placement.Pitch) &&
        double.IsFinite(placement.Bank);

    private static bool IsFiniteRequest(
        NativeSceneryPlacementRequest? request) =>
        request is not null &&
        float.IsFinite(request.WorldPoint.X) &&
        float.IsFinite(request.WorldPoint.Y) &&
        float.IsFinite(request.WorldPoint.Z) &&
        double.IsFinite(request.X) &&
        double.IsFinite(request.Y) &&
        double.IsFinite(request.Z) &&
        double.IsFinite(request.Rotation) &&
        double.IsFinite(request.Pitch) &&
        double.IsFinite(request.Bank);

    private static double NormalizeDegrees(
        double value)
    {
        var result =
            value %
            360.0;

        return result < 0
            ? result + 360.0
            : result;
    }
}
