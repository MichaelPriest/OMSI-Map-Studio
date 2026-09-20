using System.Numerics;
using MapStudio.Renderer.Viewport;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeViewportNavigationTests
{
    [Fact]
    public void WheelZoomChangesCameraDistanceAndProjection()
    {
        var navigation =
            new NativeViewportNavigation();

        var beforeDistance =
            navigation.Distance;

        var before =
            navigation.GetViewProjection(
                1280,
                720);

        navigation.ZoomByWheel(
            120);

        var after =
            navigation.GetViewProjection(
                1280,
                720);

        Assert.True(
            navigation.Distance <
            beforeDistance);

        Assert.True(
            navigation.Zoom >
            1.0f);

        Assert.NotEqual(
            before,
            after);
    }

    [Fact]
    public void PanMovesCameraTargetInWorldSpace()
    {
        var navigation =
            new NativeViewportNavigation();

        var before =
            navigation.Target;

        navigation.PanPixels(
            100,
            50,
            1000,
            500);

        Assert.NotEqual(
            before,
            navigation.Target);
    }

    [Fact]
    public void OrbitChangesYawAndPitch()
    {
        var navigation =
            new NativeViewportNavigation();

        var yaw =
            navigation.Yaw;

        var pitch =
            navigation.Pitch;

        navigation.OrbitPixels(
            50,
            -25);

        Assert.NotEqual(
            yaw,
            navigation.Yaw);

        Assert.NotEqual(
            pitch,
            navigation.Pitch);
    }

    [Fact]
    public void ResetRestoresPerspectiveHomeCamera()
    {
        var navigation =
            new NativeViewportNavigation();

        var homeTarget =
            navigation.Target;

        var homeDistance =
            navigation.Distance;

        navigation.ZoomByWheel(
            240);

        navigation.PanPixels(
            100,
            -50,
            1000,
            500);

        navigation.OrbitPixels(
            20,
            20);

        navigation.Reset();

        Assert.Equal(
            homeTarget,
            navigation.Target);

        Assert.Equal(
            homeDistance,
            navigation.Distance);

        Assert.NotEqual(
            Matrix4x4.Identity,
            navigation
                .GetViewProjection(
                    1280,
                    720));
    }

    [Fact]
    public void FocusOnCentersCameraOnRequestedWorldPoint()
    {
        var navigation =
            new NativeViewportNavigation();

        var target =
            new Vector3(
                120,
                14,
                -80);

        navigation.FocusOn(
            target,
            75);

        Assert.Equal(
            target,
            navigation.Target);

        Assert.Equal(
            75,
            navigation.Distance);
    }

    [Fact]
    public void TopViewUsesNearVerticalCameraWithoutMovingTarget()
    {
        var navigation =
            new NativeViewportNavigation();

        var target =
            navigation.Target;

        var distance =
            navigation.Distance;

        navigation.SetTopView();

        Assert.Equal(
            target,
            navigation.Target);

        Assert.Equal(
            distance,
            navigation.Distance);

        Assert.InRange(
            navigation.Pitch,
            1.54f,
            1.56f);

        Assert.True(
            float.IsFinite(
                navigation
                    .CameraPosition
                    .Y));
    }

    [Fact]
    public void PerspectiveViewRestoresDefaultOrientationAfterTopView()
    {
        var navigation =
            new NativeViewportNavigation();

        var defaultYaw =
            navigation.Yaw;

        var defaultPitch =
            navigation.Pitch;

        navigation.SetTopView();
        navigation.SetPerspectiveView();

        Assert.Equal(
            defaultYaw,
            navigation.Yaw);

        Assert.Equal(
            defaultPitch,
            navigation.Pitch);
    }

}
