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
            Math.Max(
                0.5,
                spacing);

        var segments =
            Math.Max(
                1,
                (int)Math.Ceiling(
                    distance /
                    safeSpacing));

        var count =
            Math.Min(
                256,
                segments + 1);

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
                segments <= 0
                    ? 0
                    : (double)index /
                        segments;

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

        return result;
    }

    public static IReadOnlyList<
        NativeSceneryPatternPlacement>
        BuildArea(
            NativeSceneryPlacementRequest center,
            double radius,
            int count,
            bool randomRotation)
    {
        var safeRadius =
            Math.Max(
                1,
                radius);

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

        return result;
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
            Math.Max(
                0.5,
                spacingX);

        var sz =
            Math.Max(
                0.5,
                spacingZ);

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

        return result;
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
        var safeRadius =
            Math.Max(
                0.5,
                radius);

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

        return result;
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

        var count =
            Math.Clamp(
                (int)Math.Floor(
                    distance /
                    Math.Max(
                        1,
                        spacing)) +
                1,
                1,
                256);

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
                        setback,
                    start.WorldPoint.Z +
                        dz * t +
                        normalZ *
                        setback,
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

        return result;
    }

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
