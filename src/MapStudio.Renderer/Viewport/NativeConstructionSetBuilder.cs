using MapStudio.Renderer.Scene;

namespace MapStudio.Renderer.Viewport;

public static class NativeConstructionSetBuilder
{
    public static IReadOnlyList<
        NativeConstructionSetPlacementGroup>
        Build(
            NativeSplineEntity spline,
            NativeConstructionSetDefinition set)
    {
        ArgumentNullException.ThrowIfNull(
            spline);

        ArgumentNullException.ThrowIfNull(
            set);

        if (
            set.SplinePath is
                { Length: > 0 } &&
            !string.Equals(
                set.SplinePath,
                spline.Spline.SplinePath,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "constructionSetSplineMismatch");
        }

        var groups =
            new List<
                NativeConstructionSetPlacementGroup>();

        var remainingTotal =
            512;

        foreach (
            var companion in
                set.Companions
                    .Take(16))
        {
            if (remainingTotal <= 0)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(
                    companion
                        .SceneryObjectPath))
            {
                continue;
            }

            var spacing =
                Math.Max(
                    1.0,
                    companion.Spacing);

            var count =
                Math.Clamp(
                    (int)Math.Floor(
                        spline.Spline.Length /
                        spacing) +
                    1,
                    1,
                    256);

            var placements =
                new List<
                    NativeSceneryPatternPlacement>(
                        Math.Min(
                            count * 2,
                            256));

            for (
                var index = 0;
                index < count &&
                placements.Count < 256 &&
                remainingTotal > 0;
                index++)
            {
                var progress =
                    count <= 1
                        ? 0
                        : (double)index /
                            (
                                count -
                                1
                            );

                var distance =
                    spline.Spline.Length *
                    progress;

                var frame =
                    NativeSplinePathMath
                        .GetFrame(
                            spline,
                            distance);

                var heading =
                    Math.Atan2(
                        frame.Forward.X,
                        frame.Forward.Z) *
                    180.0 /
                    Math.PI +
                    companion
                        .RotationOffset;

                double[] sideSigns =
                    companion.Side switch
                    {
                        NativeConstructionSetSide
                            .Left =>
                            [-1.0],
                        NativeConstructionSetSide
                            .Right =>
                            [1.0],
                        _ =>
                            [-1.0, 1.0]
                    };

                foreach (
                    var sideSign in
                        sideSigns)
                {
                    if (
                        placements.Count >=
                            256 ||
                        remainingTotal <=
                            0)
                    {
                        break;
                    }

                    var point =
                        frame.Center +
                        frame.Lateral *
                            (float)(
                                companion
                                    .LateralOffset *
                                sideSign);

                    placements.Add(
                        new NativeSceneryPatternPlacement(
                            point.X,
                            point.Z,
                            point.Y,
                            heading,
                            0,
                            0));

                    remainingTotal--;
                }
            }

            if (placements.Count > 0)
            {
                groups.Add(
                    new NativeConstructionSetPlacementGroup(
                        companion
                            .SceneryObjectPath,
                        placements));
            }
        }

        return groups;
    }
}
