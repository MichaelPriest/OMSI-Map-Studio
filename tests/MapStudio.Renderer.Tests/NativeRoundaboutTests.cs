using System.Numerics;
using MapStudio.Renderer.Viewport;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeRoundaboutTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RingClosesWithFourTangentQuarterArcs(bool clockwise)
    {
        var center = new Vector3(10, 2, 20);
        Assert.True(NativeSplinePlacementMath.TryCreateRoundabout(center, 15, clockwise, out var shapes));
        Assert.Equal(4, shapes.Length);
        for (var i = 0; i < 4; i++)
        {
            var s = shapes[i];
            var next = shapes[(i+1)%4];
            Assert.Equal(s.End, next.Start);
            Assert.Equal(15 * Math.PI / 2, s.Length, 6);
            Assert.Equal(clockwise ? 15d : -15d, s.Radius, 6);
            Assert.Equal(15, Vector3.Distance(s.Start, center), 3);
            Assert.Equal(2, s.Start.Y, 3);
            var delta = (next.Rotation - s.Rotation - s.Length / s.Radius * 180 / Math.PI) / 360;
            Assert.Equal(Math.Round(delta), delta, 6);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(201)]
    public void InvalidRadiusRejected(double radius) =>
        Assert.False(NativeSplinePlacementMath.TryCreateRoundabout(Vector3.Zero, radius, false, out _));
}
