using System.Numerics;
using MapStudio.Renderer.Viewport;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeSplineEndpointEditMathTests
{
    [Fact]
    public void TryCreateShapeKeepsStraightRoadStraight()
    {
        var ok =
            NativeSplineEndpointEditMath
                .TryCreateShape(
                    new Vector3(
                        0,
                        2,
                        0),
                    new Vector3(
                        20,
                        4,
                        0),
                    preserveCurve:
                        false,
                    curveOffset:
                        0,
                    out var shape);

        Assert.True(ok);
        Assert.NotNull(shape);
        Assert.False(shape!.IsCurved);
        Assert.Equal(
            20,
            shape.Length,
            3);
        Assert.Equal(
            10,
            shape.GradientStart,
            3);
    }

    [Fact]
    public void EstimateCurveOffsetAndRebuildPreserveCurveSide()
    {
        Assert.True(
            NativeSplinePlacementMath
                .TryCreateArcFromOffset(
                    Vector3.Zero,
                    new Vector3(
                        20,
                        0,
                        0),
                    4,
                    out var original));

        Assert.NotNull(original);

        var offset =
            NativeSplineEndpointEditMath
                .EstimateCurveOffset(
                    original!);

        Assert.True(
            offset >
            0);

        Assert.True(
            NativeSplineEndpointEditMath
                .TryCreateShape(
                    new Vector3(
                        2,
                        0,
                        0),
                    new Vector3(
                        24,
                        0,
                        1),
                    preserveCurve:
                        true,
                    offset,
                    out var rebuilt));

        Assert.NotNull(rebuilt);
        Assert.True(
            rebuilt!.IsCurved);
        Assert.True(
            rebuilt.Radius >
            0);
    }

    [Fact]
    public void HorizontalDistanceIgnoresHeight()
    {
        var distance =
            NativeSplineEndpointEditMath
                .HorizontalDistance(
                    new Vector3(
                        1,
                        100,
                        1),
                    new Vector3(
                        4,
                        -100,
                        5));

        Assert.Equal(
            5,
            distance,
            5);
    }
}
