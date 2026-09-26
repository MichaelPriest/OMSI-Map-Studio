using System.Numerics;
using MapStudio.Renderer.Scene;

namespace MapStudio.Renderer.Viewport;

public static class NativeSplineEndpointSnapper
{
    public static bool TrySnap(
        NativeSceneSnapshot scene,
        Vector3 candidate,
        float maximumHorizontalDistance,
        out Vector3 snapped)
    {
        ArgumentNullException.ThrowIfNull(scene);

        var bestPoint =
            candidate;

        if (
            maximumHorizontalDistance <=
            0)
        {
            snapped =
                candidate;

            return false;
        }

        var bestDistanceSquared =
            maximumHorizontalDistance *
            maximumHorizontalDistance;

        var found =
            false;

        foreach (
            var entity in
                scene.Splines)
        {
            var start =
                new Vector3(
                    entity.WorldX,
                    entity.WorldY,
                    entity.WorldZ);

            var end =
                NativeSplinePathMath
                    .GetFrame(
                        entity,
                        Math.Max(
                            0.0,
                            entity.Spline
                                .Length))
                    .Center;

            Consider(
                start);

            Consider(
                end);
        }

        snapped =
            bestPoint;

        return found;

        void Consider(
            Vector3 endpoint)
        {
            var dx =
                endpoint.X -
                candidate.X;

            var dz =
                endpoint.Z -
                candidate.Z;

            var distanceSquared =
                dx * dx +
                dz * dz;

            if (
                distanceSquared >
                bestDistanceSquared)
            {
                return;
            }

            bestDistanceSquared =
                distanceSquared;

            bestPoint =
                endpoint;

            found =
                true;
        }
    }
}
