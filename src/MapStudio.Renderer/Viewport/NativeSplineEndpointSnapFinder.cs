using System.Numerics;
using MapStudio.Renderer.Scene;

namespace MapStudio.Renderer.Viewport;

public static class NativeSplineEndpointSnapFinder
{
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
