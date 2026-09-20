using System.Numerics;
using MapStudio.Renderer.Picking;

namespace MapStudio.Renderer.Scene;

public enum NativeGizmoMode
{
    Move = 0,
    Rotate = 1
}

public enum NativeGizmoHandle
{
    None = 0,
    MoveX = 1,
    MoveY = 2,
    MoveZ = 3,
    RotateX = 4,
    RotateY = 5,
    RotateZ = 6
}

public sealed record NativeGizmoGeometry(
    NativeMapVertex[] Vertices,
    NativeMapVertex[] PickingVertices)
{
    public int TriangleCount =>
        Vertices.Length / 3;
}

public static class NativeGizmoIds
{
    public static PickingId ToPickingId(
        NativeGizmoHandle handle) =>
        handle == NativeGizmoHandle.None
            ? PickingId.None
            : new PickingId(
                PickingKind.Gizmo,
                (int)handle);

    public static bool TryGetHandle(
        PickingId pickingId,
        out NativeGizmoHandle handle)
    {
        handle =
            pickingId.Kind == PickingKind.Gizmo &&
            Enum.IsDefined(
                typeof(NativeGizmoHandle),
                pickingId.Value)
                ? (NativeGizmoHandle)
                    pickingId.Value
                : NativeGizmoHandle.None;

        return
            handle !=
            NativeGizmoHandle.None;
    }
}
