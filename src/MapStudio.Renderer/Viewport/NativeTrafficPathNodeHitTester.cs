using System.Numerics;
using MapStudio.Renderer.Scene;

namespace MapStudio.Renderer.Viewport;

public static class NativeTrafficPathNodeHitTester
{
    public static bool TryHit(
        IReadOnlyList<NativeTrafficPathNode> nodes,
        Matrix4x4 viewProjection,
        uint viewportWidth,
        uint viewportHeight,
        float pixelX,
        float pixelY,
        float radiusPixels,
        out NativeTrafficPathNode? node)
    {
        ArgumentNullException.ThrowIfNull(
            nodes);

        node =
            null;

        if (
            nodes.Count == 0 ||
            viewportWidth == 0 ||
            viewportHeight == 0 ||
            radiusPixels <= 0)
        {
            return false;
        }

        var bestDistanceSquared =
            radiusPixels *
            radiusPixels;

        var bestDepth =
            float.PositiveInfinity;

        foreach (
            var candidate in nodes)
        {
            if (
                !TryProject(
                    candidate.Position,
                    viewProjection,
                    viewportWidth,
                    viewportHeight,
                    out var screenX,
                    out var screenY,
                    out var depth))
            {
                continue;
            }

            var dx =
                screenX -
                pixelX;

            var dy =
                screenY -
                pixelY;

            var distanceSquared =
                dx * dx +
                dy * dy;

            if (
                distanceSquared >
                    bestDistanceSquared ||
                (
                    Math.Abs(
                        distanceSquared -
                        bestDistanceSquared) <
                    0.001f &&
                    depth >=
                        bestDepth
                ))
            {
                continue;
            }

            bestDistanceSquared =
                distanceSquared;

            bestDepth =
                depth;

            node =
                candidate;
        }

        return
            node is not null;
    }

    private static bool TryProject(
        Vector3 world,
        Matrix4x4 viewProjection,
        uint viewportWidth,
        uint viewportHeight,
        out float screenX,
        out float screenY,
        out float depth)
    {
        screenX =
            0;

        screenY =
            0;

        depth =
            0;

        var clip =
            Vector4.Transform(
                new Vector4(
                    world,
                    1.0f),
                viewProjection);

        if (
            !float.IsFinite(
                clip.W) ||
            clip.W <=
                0.00001f)
        {
            return false;
        }

        var inverseW =
            1.0f /
            clip.W;

        var ndcX =
            clip.X *
            inverseW;

        var ndcY =
            clip.Y *
            inverseW;

        var ndcZ =
            clip.Z *
            inverseW;

        if (
            !float.IsFinite(
                ndcX) ||
            !float.IsFinite(
                ndcY) ||
            !float.IsFinite(
                ndcZ) ||
            ndcX < -1.10f ||
            ndcX > 1.10f ||
            ndcY < -1.10f ||
            ndcY > 1.10f ||
            ndcZ < -0.05f ||
            ndcZ > 1.05f)
        {
            return false;
        }

        screenX =
            (
                ndcX +
                1.0f
            ) *
            0.5f *
            viewportWidth;

        screenY =
            (
                1.0f -
                ndcY
            ) *
            0.5f *
            viewportHeight;

        depth =
            ndcZ;

        return true;
    }
}
