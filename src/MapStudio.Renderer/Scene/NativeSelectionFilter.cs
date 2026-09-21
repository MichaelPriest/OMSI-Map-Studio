using MapStudio.Renderer.Picking;

namespace MapStudio.Renderer.Scene;

public enum NativeSelectionFilter
{
    All = 0,
    Objects = 1,
    Splines = 2
}

public static class NativeSelectionFilterExtensions
{
    public static bool Allows(
        this NativeSelectionFilter filter,
        PickingKind kind) =>
        filter switch
        {
            NativeSelectionFilter.Objects =>
                kind is
                    PickingKind.Object or
                    PickingKind.Gizmo,

            NativeSelectionFilter.Splines =>
                kind is
                    PickingKind.Spline or
                    PickingKind.Gizmo,

            _ =>
                kind is
                    PickingKind.Object or
                    PickingKind.Spline or
                    PickingKind.Gizmo
        };
}
