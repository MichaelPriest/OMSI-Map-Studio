using System.Numerics;
using MapStudio.Renderer.Scene;
using MapStudio.Renderer.Viewport;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeGizmoManipulationMathTests
{
    [Fact]
    public void MoveYUsesVerticalPointerMotion()
    {
        var delta =
            NativeGizmoManipulationMath
                .GetMoveDelta(
                    NativeGizmoHandle.MoveY,
                    new Vector3(
                        0,
                        100,
                        -100),
                    Vector3.Zero,
                    100,
                    1000,
                    0,
                    -10);

        Assert.True(
            delta.Y >
            0);

        Assert.Equal(
            0,
            delta.X);

        Assert.Equal(
            0,
            delta.Z);
    }

    [Fact]
    public void MoveXStaysConstrainedToWorldXAxis()
    {
        var delta =
            NativeGizmoManipulationMath
                .GetMoveDelta(
                    NativeGizmoHandle.MoveX,
                    new Vector3(
                        -100,
                        100,
                        -100),
                    Vector3.Zero,
                    150,
                    900,
                    20,
                    5);

        Assert.NotEqual(
            0,
            delta.X);

        Assert.Equal(
            0,
            delta.Y);

        Assert.Equal(
            0,
            delta.Z);
    }

    [Fact]
    public void MoveXZUsesCameraPlaneWithoutChangingHeight()
    {
        var delta =
            NativeGizmoManipulationMath
                .GetMoveDelta(
                    NativeGizmoHandle.MoveXZ,
                    new Vector3(
                        -100,
                        100,
                        -100),
                    Vector3.Zero,
                    150,
                    900,
                    20,
                    10);

        Assert.NotEqual(
            Vector3.Zero,
            delta);

        Assert.Equal(
            0,
            delta.Y);
    }

    [Fact]
    public void SnapTranslationRoundsEachWorldAxis()
    {
        var snapped =
            NativeGizmoManipulationMath
                .SnapTranslation(
                    new Vector3(
                        1.12f,
                        -0.61f,
                        2.37f),
                    0.25f);

        Assert.Equal(
            new Vector3(
                1.0f,
                -0.5f,
                2.25f),
            snapped);
    }

    [Fact]
    public void SnapRotationUsesConfiguredIncrement()
    {
        Assert.Equal(
            15.0f,
            NativeGizmoManipulationMath
                .SnapRotation(
                    13.2f,
                    5.0f));
    }

    [Fact]
    public void RotationPreviewKeepsAnchorFixed()
    {
        var anchor =
            new Vector3(
                12,
                4,
                20);

        var matrix =
            NativeGizmoManipulationMath
                .CreateRotationPreview(
                    NativeGizmoHandle.RotateY,
                    anchor,
                    45);

        var transformed =
            Vector3.Transform(
                anchor,
                matrix);

        Assert.True(
            Vector3.Distance(
                anchor,
                transformed) <
            0.001f);
    }
}
