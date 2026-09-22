using System.Numerics;

namespace MapStudio.Renderer.Scene;

/// <summary>
/// Converts OMSI model-space geometry (X/Y ground plane, Z up)
/// into native Direct3D editor space (X/Z ground plane, Y up).
/// </summary>
public static class NativeOmsiModelSpace
{
    private static readonly Matrix4x4
        SourceToRendererBasis =
            new(
                1, 0, 0, 0,
                0, 0, 1, 0,
                0, 1, 0, 0,
                0, 0, 0, 1);

    public static Vector3 ToRendererPosition(
        Vector3 source) =>
        new(
            source.X,
            source.Z,
            source.Y);

    public static Vector2 ToRendererUv(
        Vector2 coreUv) =>
        new(
            coreUv.X,
            1.0f -
            coreUv.Y);

    public static Vector3 ToRendererNormal(
        Vector3 source)
    {
        var converted =
            ToRendererPosition(
                source);

        return
            converted.LengthSquared() >
                0.0000001f
                ? Vector3.Normalize(
                    converted)
                : Vector3.UnitY;
    }

    public static Matrix4x4 ToRendererTransform(
        Matrix4x4 sourceTransform) =>
        SourceToRendererBasis *
        sourceTransform *
        SourceToRendererBasis;

    public static int SourceCornerForRendererCorner(
        int rendererCorner) =>
        rendererCorner switch
        {
            0 => 0,
            1 => 2,
            2 => 1,
            _ => throw new ArgumentOutOfRangeException(
                nameof(rendererCorner))
        };
}
