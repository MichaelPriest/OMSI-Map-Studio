using System.Numerics;
using MapStudio.Renderer.Viewport;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeSplineCurveHandleMathTests
{
    [Fact]
    public void CreateHandlePointUsesLateralSagitta()
    {
        var point =
            NativeSplineCurveHandleMath
                .CreateHandlePoint(
                    Vector3.Zero,
                    new Vector3(
                        20,
                        0,
                        0),
                    4);

        Assert.Equal(
            10,
            point.X,
            4);

        Assert.Equal(
            -4,
            point.Z,
            4);
    }

    [Fact]
    public void HandlePointRoundTripsThroughCurveOffsetMath()
    {
        var start =
            new Vector3(
                2,
                0,
                4);

        var end =
            new Vector3(
                17,
                0,
                15);

        var handle =
            NativeSplineCurveHandleMath
                .CreateHandlePoint(
                    start,
                    end,
                    -3.25);

        Assert.True(
            NativeSplinePlacementMath
                .TryGetCurveOffsetFromControlPoint(
                    start,
                    end,
                    handle,
                    out var offset));

        Assert.Equal(
            -3.25,
            offset,
            4);
    }

    [Fact]
    public void SnapOffsetUsesViewportMoveStep()
    {
        var snapped =
            NativeSplineCurveHandleMath
                .SnapOffset(
                    2.37,
                    enabled:
                        true,
                    step:
                        0.25);

        Assert.Equal(
            2.25,
            snapped,
            6);
    }
}
