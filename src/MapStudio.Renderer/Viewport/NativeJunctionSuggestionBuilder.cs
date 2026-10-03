using System.Numerics;
using MapStudio.Renderer.Scene;

namespace MapStudio.Renderer.Viewport;

public sealed class NativeJunctionSuggestionBuilder
{
    private sealed record SamplePoint(
        float X,
        float Z,
        double Progress);

    private sealed record Candidate(
        NativeSplineEntity Spline,
        IReadOnlyList<SamplePoint> Points);

    public IReadOnlyList<
        NativeJunctionSuggestion>
        Build(
            NativeSceneSnapshot scene)
    {
        ArgumentNullException.ThrowIfNull(
            scene);

        var candidates =
            scene.Splines
                .Where(
                    spline =>
                        !spline.Spline
                            .IsHeightSpline &&
                        spline.Spline.Length >
                            1)
                .Take(400)
                .Select(
                    spline =>
                        new Candidate(
                            spline,
                            SampleSplineAxis(
                                spline)))
                .ToArray();

        var result =
            new List<
                NativeJunctionSuggestion>();

        for (
            var aIndex = 0;
            aIndex < candidates.Length;
            aIndex++)
        {
            var a =
                candidates[aIndex];

            for (
                var bIndex = aIndex + 1;
                bIndex < candidates.Length;
                bIndex++)
            {
                var b =
                    candidates[bIndex];

                if (
                    a.Spline.Spline.SplineId ==
                    b.Spline.Spline.SplineId)
                {
                    continue;
                }

                for (
                    var ai = 0;
                    ai < a.Points.Count - 1;
                    ai++)
                {
                    for (
                        var bi = 0;
                        bi < b.Points.Count - 1;
                        bi++)
                    {
                        if (
                            !TryIntersect(
                                a.Points[ai],
                                a.Points[ai + 1],
                                b.Points[bi],
                                b.Points[bi + 1],
                                out var hit))
                        {
                            continue;
                        }

                        var angleDifference =
                            Math.Abs(
                                Math.Atan2(
                                    Math.Sin(
                                        hit.AngleA -
                                        hit.AngleB),
                                    Math.Cos(
                                        hit.AngleA -
                                        hit.AngleB)));

                        var acuteAngle =
                            Math.Min(
                                angleDifference,
                                Math.PI -
                                angleDifference);

                        if (
                            acuteAngle <
                            15.0 *
                            Math.PI /
                            180.0)
                        {
                            continue;
                        }

                        var progressA =
                            a.Points[ai]
                                .Progress +
                            (
                                a.Points[ai + 1]
                                    .Progress -
                                a.Points[ai]
                                    .Progress
                            ) *
                            hit.Ta;

                        var progressB =
                            b.Points[bi]
                                .Progress +
                            (
                                b.Points[bi + 1]
                                    .Progress -
                                b.Points[bi]
                                    .Progress
                            ) *
                            hit.Tb;

                        var interiorA =
                            progressA >
                                0.03 &&
                            progressA <
                                0.97;

                        var interiorB =
                            progressB >
                                0.03 &&
                            progressB <
                                0.97;

                        if (
                            !interiorA &&
                            !interiorB)
                        {
                            continue;
                        }

                        if (
                            result.Any(
                                current =>
                                {
                                    var currentX =
                                        current.TileX *
                                            300.0 +
                                        current.X;

                                    var currentZ =
                                        current.TileY *
                                            300.0 +
                                        current.Y;

                                    return Math.Sqrt(
                                        Math.Pow(
                                            currentX -
                                            hit.X,
                                            2) +
                                        Math.Pow(
                                            currentZ -
                                            hit.Z,
                                            2)) <
                                        3.0;
                                }))
                        {
                            continue;
                        }

                        var tileX =
                            (int)Math.Floor(
                                hit.X /
                                300.0);

                        var tileY =
                            (int)Math.Floor(
                                hit.Z /
                                300.0);

                        var height =
                            NativeTerrainSampler
                                .GetHeightAtWorldPoint(
                                    scene,
                                    hit.X,
                                    hit.Z);

                        result.Add(
                            new NativeJunctionSuggestion(
                                $"{a.Spline.Spline.SplineId}:{b.Spline.Spline.SplineId}:{Math.Round(hit.X)}:{Math.Round(hit.Z)}",
                                tileX,
                                tileY,
                                hit.X -
                                    tileX *
                                    300.0,
                                hit.Z -
                                    tileY *
                                    300.0,
                                hit.AngleA *
                                    180.0 /
                                    Math.PI,
                                a.Spline.Spline.SplineId,
                                b.Spline.Spline.SplineId,
                                new Vector3(
                                    (float)hit.X,
                                    (float)height,
                                    (float)hit.Z)));

                        if (
                            result.Count >=
                            64)
                        {
                            return result;
                        }
                    }
                }
            }
        }

        return result;
    }

    private static IReadOnlyList<
        SamplePoint>
        SampleSplineAxis(
            NativeSplineEntity spline)
    {
        var radius =
            spline.Spline.Radius;

        var curveRadians =
            Math.Abs(radius) >
            0.001
                ? Math.Abs(
                    spline.Spline.Length /
                    radius)
                : 0.0;

        var segmentCount =
            Math.Max(
                1,
                Math.Min(
                    24,
                    (int)Math.Ceiling(
                        curveRadians *
                        6.0)));

        var result =
            new SamplePoint[
                segmentCount +
                1];

        for (
            var index = 0;
            index <= segmentCount;
            index++)
        {
            var progress =
                (double)index /
                segmentCount;

            var frame =
                NativeSplinePathMath
                    .GetFrame(
                        spline,
                        spline.Spline.Length *
                        progress);

            result[index] =
                new SamplePoint(
                    frame.Center.X,
                    frame.Center.Z,
                    progress);
        }

        return result;
    }

    private static bool TryIntersect(
        SamplePoint a0,
        SamplePoint a1,
        SamplePoint b0,
        SamplePoint b1,
        out Intersection hit)
    {
        hit =
            default;

        var ax =
            a1.X -
            a0.X;

        var az =
            a1.Z -
            a0.Z;

        var bx =
            b1.X -
            b0.X;

        var bz =
            b1.Z -
            b0.Z;

        var denominator =
            ax *
                bz -
            az *
                bx;

        if (
            Math.Abs(
                denominator) <
            0.0001)
        {
            return false;
        }

        var dx =
            b0.X -
            a0.X;

        var dz =
            b0.Z -
            a0.Z;

        var ta =
            (
                dx *
                    bz -
                dz *
                    bx
            ) /
            denominator;

        var tb =
            (
                dx *
                    az -
                dz *
                    ax
            ) /
            denominator;

        if (
            ta <
                -0.001 ||
            ta >
                1.001 ||
            tb <
                -0.001 ||
            tb >
                1.001)
        {
            return false;
        }

        hit =
            new Intersection(
                a0.X +
                    ax *
                    ta,
                a0.Z +
                    az *
                    ta,
                ta,
                tb,
                Math.Atan2(
                    ax,
                    az),
                Math.Atan2(
                    bx,
                    bz));

        return true;
    }

    private readonly record struct Intersection(
        double X,
        double Z,
        double Ta,
        double Tb,
        double AngleA,
        double AngleB);
}
