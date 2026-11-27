using System.Numerics;
using MapStudio.Renderer.Picking;
using MapStudio.Renderer.Scene;
using MapStudio.Renderer.Viewport;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeTrafficPathNodeHitTesterTests
{
    [Fact]
    public void TryHitSelectsNearestVisibleNode()
    {
        var near =
            new NativeTrafficPathNode(
                Vector3.Zero,
                new PickingId(
                    PickingKind.Spline,
                    4),
                2,
                true,
                0);

        var far =
            new NativeTrafficPathNode(
                new Vector3(
                    0.8f,
                    0,
                    0),
                new PickingId(
                    PickingKind.Object,
                    8),
                1,
                false,
                1);

        var result =
            NativeTrafficPathNodeHitTester
                .TryHit(
                    [near, far],
                    Matrix4x4.Identity,
                    100,
                    100,
                    52,
                    51,
                    12,
                    out var selected);

        Assert.True(
            result);

        Assert.Same(
            near,
            selected);
    }

    [Fact]
    public void TryHitRejectsNodesOutsideRadius()
    {
        var node =
            new NativeTrafficPathNode(
                Vector3.Zero,
                new PickingId(
                    PickingKind.Spline,
                    1),
                0,
                true,
                0);

        var result =
            NativeTrafficPathNodeHitTester
                .TryHit(
                    [node],
                    Matrix4x4.Identity,
                    100,
                    100,
                    92,
                    92,
                    8,
                    out var selected);

        Assert.False(
            result);

        Assert.Null(
            selected);
    }

    [Fact]
    public void TryHitRejectsNodeBehindCamera()
    {
        var node =
            new NativeTrafficPathNode(
                new Vector3(
                    0,
                    0,
                    0),
                new PickingId(
                    PickingKind.Spline,
                    1),
                0,
                true,
                0);

        var viewProjection =
            Matrix4x4.Identity;

        viewProjection.M44 =
            -1.0f;

        var result =
            NativeTrafficPathNodeHitTester
                .TryHit(
                    [node],
                    viewProjection,
                    100,
                    100,
                    50,
                    50,
                    10,
                    out _);

        Assert.False(
            result);
    }
}
