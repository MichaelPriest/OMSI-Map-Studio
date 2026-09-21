using MapStudio.Renderer.Picking;
using MapStudio.Renderer.Scene;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeSceneVisibilityTests
{
    [Fact]
    public void AllMakesEditableSceneKindsVisible()
    {
        var visibility =
            NativeSceneVisibility.All;

        Assert.True(
            visibility.TerrainVisible);

        Assert.True(
            visibility.IsPickingKindVisible(
                PickingKind.Object));

        Assert.True(
            visibility.IsPickingKindVisible(
                PickingKind.Spline));
    }

    [Fact]
    public void HiddenObjectsAndSplinesAreNotPickable()
    {
        var visibility =
            new NativeSceneVisibility(
                TerrainVisible: true,
                ObjectsVisible: false,
                SplinesVisible: false);

        Assert.False(
            visibility.IsPickingKindVisible(
                PickingKind.Object));

        Assert.False(
            visibility.IsPickingKindVisible(
                PickingKind.Spline));

        Assert.True(
            visibility.IsPickingKindVisible(
                PickingKind.None));
    }

    [Fact]
    public void GizmoVisibilityFollowsEditableCategories()
    {
        var noneEditable =
            new NativeSceneVisibility(
                TerrainVisible: true,
                ObjectsVisible: false,
                SplinesVisible: false);

        var splineOnly =
            noneEditable with
            {
                SplinesVisible = true
            };

        Assert.False(
            noneEditable.IsPickingKindVisible(
                PickingKind.Gizmo));

        Assert.True(
            splineOnly.IsPickingKindVisible(
                PickingKind.Gizmo));
    }
}
