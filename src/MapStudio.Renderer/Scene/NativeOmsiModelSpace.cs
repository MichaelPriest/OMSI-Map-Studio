using System.Numerics;

namespace MapStudio.Renderer.Scene;

/// <summary>
/// Renderer-boundary compatibility helpers for OMSI model data.
/// O3D vertex positions/normals are already in the Y-up runtime axes
/// used by the native renderer. Only texture V needs conversion from
/// the Core/editor convention back to Direct3D sampling orientation.
/// </summary>
public static class NativeOmsiModelSpace
{
    public static Vector2 ToRendererUv(
        Vector2 coreUv) =>
        new(
            coreUv.X,
            1.0f -
            coreUv.Y);
}
