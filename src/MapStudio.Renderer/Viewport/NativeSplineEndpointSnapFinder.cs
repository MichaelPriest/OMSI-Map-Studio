using System.Numerics;
using MapStudio.Renderer.Scene;

namespace MapStudio.Renderer.Viewport;

public static class NativeSplineEndpointSnapFinder
{
    // The next road starts from the persisted geometry, never a cached
    // mouse position or the requested (pre-serialization) endpoint.
    public static bool TryResolveContinuationStart(
        NativeSceneSnapshot scene, int previousSplineId, out Vector3 start)
    {
        ArgumentNullException.ThrowIfNull(scene);
        start = default;
        var matches = scene.Splines.Where(s => s.Spline.SplineId == previousSplineId).ToArray();
        if (matches.Length != 1 || matches[0].Spline.IsHeightSpline ||
            matches[0].Spline.NextSplineId != -1 ||
            !double.IsFinite(matches[0].Spline.Length) || matches[0].Spline.Length <= 0)
        {
            return false;
        }
        var spline = matches[0];
        if (!float.IsFinite(spline.WorldX) || !float.IsFinite(spline.WorldY) ||
            !float.IsFinite(spline.WorldZ) || !double.IsFinite(spline.Spline.Rotation) ||
            !double.IsFinite(spline.Spline.Radius) ||
            !double.IsFinite(spline.Spline.GradientStart) || !double.IsFinite(spline.Spline.GradientEnd))
        {
            return false;
        }
        start = NativeSplinePathMath.GetFrame(spline, spline.Spline.Length).Center;
        return float.IsFinite(start.X) && float.IsFinite(start.Y) && float.IsFinite(start.Z);
    }

    public static NativeSplineEndpointSnap?
        FindFreeEndpoint(
            NativeSceneSnapshot scene,
            Vector3 point,
            NativeSplineEndpointKind endpoint,
            double maximumDistance,
            int excludeSplineId = -1)
    {
        ArgumentNullException.ThrowIfNull(
            scene);

        if (
            !float.IsFinite(point.X) || !float.IsFinite(point.Y) || !float.IsFinite(point.Z) ||
            !double.IsFinite(
                maximumDistance) ||
            maximumDistance <= 0)
        {
            return null;
        }

        NativeSplineEndpointSnap?
            best =
                null;

        foreach (
            var spline in
                scene.Splines)
        {
            var placed =
                spline.Spline;

            if (
                placed.SplineId ==
                    excludeSplineId ||
                placed.IsHeightSpline)
            {
                continue;
            }

            if (
                endpoint ==
                    NativeSplineEndpointKind.End &&
                placed.NextSplineId !=
                    -1 &&
                scene.Splines.Any(
                    candidate =>
                        candidate.Spline.SplineId ==
                        placed.NextSplineId))
            {
                continue;
            }

            if (
                endpoint ==
                    NativeSplineEndpointKind.Start &&
                placed.PreviousSplineId !=
                    -1 &&
                scene.Splines.Any(
                    candidate =>
                        candidate.Spline.SplineId ==
                        placed.PreviousSplineId))
            {
                continue;
            }

            var candidate =
                endpoint ==
                    NativeSplineEndpointKind.Start
                    ? new Vector3(
                        spline.WorldX,
                        spline.WorldY,
                        spline.WorldZ)
                    : NativeSplinePathMath
                        .GetFrame(
                            spline,
                            placed.Length)
                        .Center;

            if (!float.IsFinite(candidate.X) || !float.IsFinite(candidate.Y) || !float.IsFinite(candidate.Z))
            {
                continue;
            }

            var dx =
                candidate.X -
                point.X;

            var dz =
                candidate.Z -
                point.Z;

            var distance =
                Math.Sqrt(
                    dx * dx +
                    dz * dz);

            if (
                distance >
                maximumDistance)
            {
                continue;
            }

            if (
                best is null ||
                distance <
                    best.Distance)
            {
                best =
                    new NativeSplineEndpointSnap(
                        placed.SplineId,
                        endpoint,
                        candidate,
                        distance);
            }
        }

        return best;
    }
}
