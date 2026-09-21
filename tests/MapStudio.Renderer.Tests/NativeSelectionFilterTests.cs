using MapStudio.Renderer.Picking;
using MapStudio.Renderer.Scene;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeSelectionFilterTests
{
    [Theory]
    [InlineData(
        NativeSelectionFilter.All,
        PickingKind.Object,
        true)]
    [InlineData(
        NativeSelectionFilter.All,
        PickingKind.Spline,
        true)]
    [InlineData(
        NativeSelectionFilter.Objects,
        PickingKind.Object,
        true)]
    [InlineData(
        NativeSelectionFilter.Objects,
        PickingKind.Spline,
        false)]
    [InlineData(
        NativeSelectionFilter.Splines,
        PickingKind.Object,
        false)]
    [InlineData(
        NativeSelectionFilter.Splines,
        PickingKind.Spline,
        true)]
    [InlineData(
        NativeSelectionFilter.Terrain,
        PickingKind.Object,
        false)]
    [InlineData(
        NativeSelectionFilter.Terrain,
        PickingKind.Spline,
        false)]
    [InlineData(
        NativeSelectionFilter.Terrain,
        PickingKind.Gizmo,
        false)]
    public void FilterControlsEditablePickingKinds(
        NativeSelectionFilter filter,
        PickingKind kind,
        bool expected)
    {
        Assert.Equal(
            expected,
            filter.Allows(kind));
    }

    [Fact]
    public void GizmoRemainsAllowedForSingleCategoryFilters()
    {
        Assert.True(
            NativeSelectionFilter.Objects
                .Allows(
                    PickingKind.Gizmo));

        Assert.True(
            NativeSelectionFilter.Splines
                .Allows(
                    PickingKind.Gizmo));
    }
}
