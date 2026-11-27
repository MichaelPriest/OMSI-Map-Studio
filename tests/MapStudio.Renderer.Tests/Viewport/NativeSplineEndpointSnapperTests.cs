using System.Numerics;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Renderer.Picking;
using MapStudio.Renderer.Scene;
using MapStudio.Renderer.Viewport;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class
    NativeSplineEndpointSnapperTests
{
    [Fact]
    public void SnapsToNearestExistingSplineEndpoint()
    {
        var spline =
            new OmsiPlacedSpline(
                "0",
                @"Splines\Road.sli",
                1,
                -1,
                -1,
                0,
                5,
                0,
                0,
                20,
                0,
                0,
                0,
                false,
                []);

        var entity =
            new NativeSplineEntity(
                new PickingId(
                    PickingKind.Spline,
                    1),
                new OmsiTileReference(
                    0,
                    0,
                    "tile_0_0.map"),
                spline,
                0,
                5,
                0);

        var scene =
            new NativeSceneSnapshot(
                Array.Empty<
                    NativeSceneTile>(),
                Array.Empty<
                    NativeObjectEntity>(),
                [entity],
                Array.Empty<
                    NativeTerrainEntity>());

        var snapped =
            NativeSplineEndpointSnapper
                .TrySnap(
                    scene,
                    new Vector3(
                        0.7f,
                        0,
                        19.4f),
                    2.0f,
                    out var point);

        Assert.True(
            snapped);

        Assert.Equal(
            0,
            point.X,
            4);

        Assert.Equal(
            5,
            point.Y,
            4);

        Assert.Equal(
            20,
            point.Z,
            4);
    }

    [Fact]
    public void DoesNotSnapOutsideThreshold()
    {
        var scene =
            new NativeSceneSnapshot(
                Array.Empty<
                    NativeSceneTile>(),
                Array.Empty<
                    NativeObjectEntity>(),
                Array.Empty<
                    NativeSplineEntity>(),
                Array.Empty<
                    NativeTerrainEntity>());

        Assert.False(
            NativeSplineEndpointSnapper
                .TrySnap(
                    scene,
                    new Vector3(
                        5,
                        0,
                        5),
                    2,
                    out _));
    }
}
