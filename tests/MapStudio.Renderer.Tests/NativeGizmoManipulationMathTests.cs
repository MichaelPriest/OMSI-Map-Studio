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
    public void MoveXZDraggingScreenRightMovesRightForForwardFacingCamera()
    {
        var delta =
            NativeGizmoManipulationMath
                .GetMoveDelta(
                    NativeGizmoHandle.MoveXZ,
                    new Vector3(
                        0,
                        100,
                        -100),
                    Vector3.Zero,
                    100,
                    1000,
                    25,
                    0);

        Assert.True(
            delta.X >
            0);

        Assert.Equal(
            0,
            delta.Y);

        Assert.True(
            MathF.Abs(
                delta.Z) <
            0.0001f);
    }

    [Fact]
    public void MoveXZDraggingScreenUpMovesForwardForForwardFacingCamera()
    {
        var delta =
            NativeGizmoManipulationMath
                .GetMoveDelta(
                    NativeGizmoHandle.MoveXZ,
                    new Vector3(
                        0,
                        100,
                        -100),
                    Vector3.Zero,
                    100,
                    1000,
                    0,
                    -25);

        Assert.True(
            delta.Z >
            0);

        Assert.Equal(
            0,
            delta.Y);

        Assert.True(
            MathF.Abs(
                delta.X) <
            0.0001f);
    }

    [Fact]
    public void PointerPlaneMoveFollowsPointerWorldDirection()
    {
        var delta =
            NativeGizmoManipulationMath
                .GetPointerPlaneMoveDelta(
                    new Vector3(
                        10,
                        3,
                        20),
                    new Vector3(
                        16,
                        3,
                        27));

        Assert.Equal(
            new Vector3(
                6,
                0,
                7),
            delta);
    }

    [Fact]
    public void GrabPanMovesCameraTargetOppositeToPointerWorldDirection()
    {
        var delta =
            NativeGizmoManipulationMath
                .GetGrabPanTargetDelta(
                    new Vector3(
                        10,
                        3,
                        20),
                    new Vector3(
                        16,
                        3,
                        27));

        Assert.Equal(
            new Vector3(
                -6,
                0,
                -7),
            delta);
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
