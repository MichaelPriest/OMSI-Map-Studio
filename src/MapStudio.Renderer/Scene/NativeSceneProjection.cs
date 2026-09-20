using System.Numerics;

namespace MapStudio.Renderer.Scene;

public readonly record struct NativeSceneProjection(
    double CenterX,
    double CenterZ,
    double HalfWidth,
    double HalfHeight)
{
    public static NativeSceneProjection FromScene(
        NativeSceneSnapshot scene)
    {
        ArgumentNullException.ThrowIfNull(
            scene);

        if (scene.Tiles.Count == 0)
        {
            return new NativeSceneProjection(
                0,
                0,
                1,
                1);
        }

        var minX =
            scene.Tiles.Min(
                tile =>
                    tile.Reference.X *
                    300.0);

        var maxX =
            scene.Tiles.Max(
                tile =>
                    tile.Reference.X *
                    300.0 +
                    300.0);

        var minZ =
            scene.Tiles.Min(
                tile =>
                    tile.Reference.Y *
                    300.0);

        var maxZ =
            scene.Tiles.Max(
                tile =>
                    tile.Reference.Y *
                    300.0 +
                    300.0);

        return new NativeSceneProjection(
            CenterX:
                (minX + maxX) * 0.5,
            CenterZ:
                (minZ + maxZ) * 0.5,
            HalfWidth:
                Math.Max(
                    1.0,
                    (maxX - minX) *
                    0.55),
            HalfHeight:
                Math.Max(
                    1.0,
                    (maxZ - minZ) *
                    0.55));
    }

    public Vector3 ProjectTopDown(
        double worldX,
        double worldZ,
        float depth = 0.5f)
    {
        return new Vector3(
            (float)(
                (worldX - CenterX) /
                HalfWidth),
            (float)(
                (worldZ - CenterZ) /
                HalfHeight),
            depth);
    }
}
