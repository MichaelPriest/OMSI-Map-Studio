using System.Numerics;
using MapStudio.Renderer.Viewport;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class
    NativeSplinePlacementMathTests
{
    [Fact]
    public void StraightSplineUsesOmsiYawAndGradient()
    {
        var ok =
            NativeSplinePlacementMath
                .TryCreateStraight(
                    new Vector3(
                        0,
                        10,
                        0),
                    new Vector3(
                        10,
                        12,
                        0),
                    out var shape);

        Assert.True(
            ok);

        Assert.NotNull(
            shape);

        Assert.Equal(
            90,
            shape!.Rotation,
            6);

        Assert.Equal(
            10,
            shape.Length,
            6);

        Assert.Equal(
            20,
            shape.GradientStart,
            6);

        Assert.Equal(
            0,
            shape.Radius,
            6);
    }

    [Fact]
    public void ArcThroughControlPointProducesFiniteRadius()
    {
        var ok =
            NativeSplinePlacementMath
                .TryCreateArc(
                    new Vector3(
                        0,
                        0,
                        0),
                    new Vector3(
                        10,
                        0,
                        10),
                    new Vector3(
                        8,
                        0,
                        2),
                    out var shape);

        Assert.True(
            ok);

        Assert.NotNull(
            shape);

        Assert.True(
            shape!.IsCurved);

        Assert.True(
            Math.Abs(
                shape.Radius) >
            0.1);

        Assert.True(
            shape.Length >
            10);
    }

    [Fact]
    public void CurveOffsetCreatesTwoPointEasyRoadArc()
    {
        var ok =
            NativeSplinePlacementMath
                .TryCreateArcFromOffset(
                    new Vector3(
                        0,
                        10,
                        0),
                    new Vector3(
                        40,
                        14,
                        0),
                    8,
                    out var shape);

        Assert.True(
            ok);

        Assert.NotNull(
            shape);

        Assert.True(
            shape!.IsCurved);

        Assert.True(
            shape.Radius >
            0);

        Assert.True(
            shape.Length >
            40);

        Assert.InRange(
            shape.GradientStart,
            0,
            10);
    }

    [Fact]
    public void NearZeroCurveOffsetFallsBackToStraight()
    {
        var ok =
            NativeSplinePlacementMath
                .TryCreateArcFromOffset(
                    Vector3.Zero,
                    new Vector3(
                        0,
                        0,
                        30),
                    0.01,
                    out var shape);

        Assert.True(
            ok);

        Assert.NotNull(
            shape);

        Assert.False(
            shape!.IsCurved);

        Assert.Equal(
            30,
            shape.Length,
            6);
    }

    [Fact]
    public void CollinearCurveFallsBackToStraight()
    {
        var ok =
            NativeSplinePlacementMath
                .TryCreateArc(
                    Vector3.Zero,
                    new Vector3(
                        10,
                        0,
                        0),
                    new Vector3(
                        5,
                        0,
                        0),
                    out var shape);

        Assert.True(
            ok);

        Assert.False(
            shape!.IsCurved);

        Assert.Equal(
            0,
            shape.Radius,
            6);
    }
}
