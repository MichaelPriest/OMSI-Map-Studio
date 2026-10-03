using System.Numerics;
using MapStudio.Renderer.Viewport;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class
    NativeViewportNavigationRayTests
{
    [Fact]
    public void CenterPixelRayPointsTowardCameraTarget()
    {
        var navigation =
            new NativeViewportNavigation();

        navigation.FitToBounds(
            new Vector3(
                -20,
                -2,
                -20),
            new Vector3(
                20,
                8,
                20));

        var success =
            navigation.TryGetWorldRay(
                400,
                300,
                800,
                600,
                out var origin,
                out var direction);

        Assert.True(
            success);

        var expected =
            Vector3.Normalize(
                navigation.Target -
                origin);

        Assert.True(
            Vector3.Dot(
                expected,
                direction) >
            0.995f);
    }

    [Fact]
    public void InvalidViewportDoesNotProduceRay()
    {
        var navigation =
            new NativeViewportNavigation();

        Assert.False(
            navigation.TryGetWorldRay(
                0,
                0,
                0,
                600,
                out _,
                out _));
    }
}
