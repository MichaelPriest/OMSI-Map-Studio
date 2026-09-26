using System.Numerics;
using MapStudio.Renderer.Scene;

namespace MapStudio.Renderer.Viewport;

public sealed record NativeSceneryRoadSnap(
    Vector3 WorldPoint,
    double Rotation,
    double Distance,
    int SplineId);

public static class NativeSceneryRoadSnapper
{
    public static NativeSceneryRoadSnap?
        FindNearest(
            NativeSceneSnapshot scene,
            Vector3 worldPoint,
            double maximumDistance)
    {
        ArgumentNullException.ThrowIfNull(
            scene);

        if (
            !float.IsFinite(
                worldPoint.X) ||
            !float.IsFinite(
                worldPoint.Z) ||
            !double.IsFinite(
                maximumDistance) ||
            maximumDistance <=
                0)
        {
            return null;
        }

        NativeSceneryRoadSnap? best =
            null;

        foreach (
            var spline in
                scene.Splines)
        {
            if (
                spline.Spline
                    .IsHeightSpline ||
                !double.IsFinite(
                    spline.Spline
                        .Length) ||
                spline.Spline.Length <=
                    0.001)
            {
                continue;
            }

            var length =
                spline.Spline.Length;

            var coarseSegments =
                Math.Clamp(
                    (int)Math.Ceiling(
                        length /
                        4.0),
                    8,
                    128);

            var step =
                length /
                coarseSegments;

            var bestAlong =
                0.0;

            var bestSquared =
                double.PositiveInfinity;

            for (
                var index = 0;
                index <=
                    coarseSegments;
                index++)
            {
                var along =
                    step *
                    index;

                var frame =
                    NativeSplinePathMath
                        .GetFrame(
                            spline,
                            along);

                var squared =
                    DistanceSquaredXZ(
                        worldPoint,
                        frame.Center);

                if (
                    squared <
                    bestSquared)
                {
                    bestSquared =
                        squared;

                    bestAlong =
                        along;
                }
            }

            var lower =
                Math.Max(
                    0.0,
                    bestAlong -
                    step);

            var upper =
                Math.Min(
                    length,
                    bestAlong +
                    step);

            for (
                var iteration = 0;
                iteration < 24;
                iteration++)
            {
                var third =
                    (
                        upper -
                        lower
                    ) /
                    3.0;

                var left =
                    lower +
                    third;

                var right =
                    upper -
                    third;

                var leftSquared =
                    DistanceSquaredXZ(
                        worldPoint,
                        NativeSplinePathMath
                            .GetFrame(
                                spline,
                                left)
                            .Center);

                var rightSquared =
                    DistanceSquaredXZ(
                        worldPoint,
                        NativeSplinePathMath
                            .GetFrame(
                                spline,
                                right)
                            .Center);

                if (
                    leftSquared <=
                    rightSquared)
                {
                    upper =
                        right;
                }
                else
                {
                    lower =
                        left;
                }
            }

            var nearestAlong =
                (
                    lower +
                    upper
                ) /
                2.0;

            var nearestFrame =
                NativeSplinePathMath
                    .GetFrame(
                        spline,
                        nearestAlong);

            var distance =
                Math.Sqrt(
                    DistanceSquaredXZ(
                        worldPoint,
                        nearestFrame
                            .Center));

            if (
                distance >
                    maximumDistance ||
                (
                    best is not null &&
                    distance >=
                        best.Distance
                ))
            {
                continue;
            }

            var rotation =
                NormalizeDegrees(
                    Math.Atan2(
                        nearestFrame
                            .Forward.X,
                        nearestFrame
                            .Forward.Z) *
                    180.0 /
                    Math.PI);

            best =
                new NativeSceneryRoadSnap(
                    new Vector3(
                        nearestFrame
                            .Center.X,
                        worldPoint.Y,
                        nearestFrame
                            .Center.Z),
                    rotation,
                    distance,
                    spline.Spline
                        .SplineId);
        }

        return best;
    }

    private static double DistanceSquaredXZ(
        Vector3 a,
        Vector3 b)
    {
        var dx =
            (double)a.X -
            b.X;

        var dz =
            (double)a.Z -
            b.Z;

        return
            dx * dx +
            dz * dz;
    }

    private static double NormalizeDegrees(
        double value)
    {
        var normalized =
            value %
            360.0;

        if (normalized < 0)
        {
            normalized +=
                360.0;
        }

        return normalized;
    }
}
