using MapStudio.Renderer.Picking;

namespace MapStudio.Renderer.Scene;

public readonly record struct NativeSceneVisibility(
    bool TerrainVisible,
    bool ObjectsVisible,
    bool SplinesVisible)
{
    public static NativeSceneVisibility All =>
        new(
            TerrainVisible: true,
            ObjectsVisible: true,
            SplinesVisible: true);

    public bool IsPickingKindVisible(
        PickingKind kind) =>
        kind switch
        {
            PickingKind.Object =>
                ObjectsVisible,

            PickingKind.Spline =>
                SplinesVisible,

            PickingKind.Gizmo =>
                ObjectsVisible ||
                SplinesVisible,

            _ =>
                true
        };
}
