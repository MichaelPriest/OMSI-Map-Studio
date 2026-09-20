using MapStudio.Renderer.Viewport;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeViewportNavigationTests
{
    [Fact]
    public void WheelZoomChangesGpuTransformWithoutChangingSceneGeometry()
    {
        var navigation =
            new NativeViewportNavigation();

        navigation.ZoomByWheel(
            120);

        Assert.True(
            navigation.Zoom > 1.0f);

        Assert.Equal(
            navigation.Zoom,
            navigation
                .ShaderTransform.Z);
    }

    [Fact]
    public void PanConvertsPhysicalPixelsToClipSpace()
    {
        var navigation =
            new NativeViewportNavigation();

        navigation.PanPixels(
            100,
            50,
            1000,
            500);

        Assert.InRange(
            navigation.OffsetX,
            0.199f,
            0.201f);

        Assert.InRange(
            navigation.OffsetY,
            -0.201f,
            -0.199f);
    }

    [Fact]
    public void ResetRestoresDefaultViewportTransform()
    {
        var navigation =
            new NativeViewportNavigation();

        navigation.ZoomByWheel(
            240);

        navigation.PanPixels(
            100,
            -50,
            1000,
            500);

        navigation.Reset();

        Assert.Equal(
            1.0f,
            navigation.Zoom);

        Assert.Equal(
            0.0f,
            navigation.OffsetX);

        Assert.Equal(
            0.0f,
            navigation.OffsetY);
    }
}
