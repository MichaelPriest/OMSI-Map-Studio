using System.Numerics;
using MapStudio.Renderer.Viewport;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeRoadNumericTests
{
    [Theory]
    [InlineData(0, 0, 50, 0)]
    [InlineData(0, 90, 0, 50)]
    [InlineData(50, 0, 50, 50)]
    [InlineData(-50, 0, 50, -50)]
    public void ExactStraightAndQuarterCircle(double radius, double heading, double z, double x)
    {
        var length = radius == 0 ? 50 : Math.PI * 25;
        Assert.True(NativeSplinePlacementMath.TryCreateNumeric(Vector3.Zero, length, radius, heading, 5, out var shape));
        Assert.NotNull(shape);
        Assert.Equal(x, shape!.End.X, 3);
        Assert.Equal(z, shape.End.Z, 3);
        Assert.Equal(length, shape.Length, 6);
        Assert.Equal(radius, shape.Radius, 6);
        Assert.Equal(500 / length, shape.GradientStart, 6);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(100, 2)]
    [InlineData(200, 10)]
    [InlineData(4000, 0)]
    public void RejectsInvalidGeometry(double length, double radius)
    {
        Assert.False(NativeSplinePlacementMath.TryCreateNumeric(Vector3.Zero, length, radius, 0, 0, out _));
    }
}
