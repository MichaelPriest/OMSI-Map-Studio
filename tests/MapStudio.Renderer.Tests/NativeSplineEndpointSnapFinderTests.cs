using MapStudio.Core.Omsi.Maps;
using MapStudio.Renderer.Picking;
using MapStudio.Renderer.Scene;
using MapStudio.Renderer.Viewport;
using System.Numerics;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeSplineEndpointSnapFinderTests
{
    [Fact]
    public void FindsNearestFreeEndForRoadStart()
    {
        var tile =
            new OmsiTileReference(
                0,
                0,
                "tile.map");

        var spline =
            new NativeSplineEntity(
                new PickingId(
                    PickingKind.Spline,
                    10),
                tile,
                new OmsiPlacedSpline(
                    "0",
                    @"Splines\Roads\road.sli",
                    10,
                    -1,
                    -1,
                    0,
                    0,
                    0,
                    0,
                    20,
                    0,
                    0,
                    0,
                    false,
                    []),
                0,
                0,
                0);

        var scene =
            new NativeSceneSnapshot(
                [],
                [],
                [spline],
                []);

        var snap =
            NativeSplineEndpointSnapFinder
                .FindFreeEndpoint(
                    scene,
                    new Vector3(
                        0.3f,
                        0,
                        20.4f),
                    NativeSplineEndpointKind.End,
                    5);

        Assert.NotNull(
            snap);

        Assert.Equal(
            10,
            snap!.SplineId);

        Assert.InRange(
            snap.Distance,
            0,
            1);
    }

    [Fact]
    public void IgnoresAlreadyLinkedEndpoint()
    {
        var tile =
            new OmsiTileReference(
                0,
                0,
                "tile.map");

        var spline =
            new NativeSplineEntity(
                new PickingId(
                    PickingKind.Spline,
                    10),
                tile,
                new OmsiPlacedSpline(
                    "0",
                    @"Splines\Roads\road.sli",
                    10,
                    -1,
                    99,
                    0,
                    0,
                    0,
                    0,
                    20,
                    0,
                    0,
                    0,
                    false,
                    []),
                0,
                0,
                0);

        var linkedTarget =
            new NativeSplineEntity(
                new PickingId(
                    PickingKind.Spline,
                    99),
                tile,
                new OmsiPlacedSpline(
                    "0",
                    @"Splines\Roads\road.sli",
                    99,
                    10,
                    -1,
                    0,
                    20,
                    0,
                    0,
                    20,
                    0,
                    0,
                    0,
                    false,
                    []),
                0,
                0,
                20);

        var scene =
            new NativeSceneSnapshot(
                [],
                [],
                [
                    spline,
                    linkedTarget
                ],
                []);

        Assert.Null(
            NativeSplineEndpointSnapFinder
                .FindFreeEndpoint(
                    scene,
                    new Vector3(
                        0,
                        0,
                        20),
                    NativeSplineEndpointKind.End,
                    5));
    }

    [Fact]
    public void TreatsBrokenLinkAsRepairableEndpoint()
    {
        var tile =
            new OmsiTileReference(
                0,
                0,
                "tile.map");

        var spline =
            new NativeSplineEntity(
                new PickingId(
                    PickingKind.Spline,
                    10),
                tile,
                new OmsiPlacedSpline(
                    "0",
                    @"Splines\Roads\road.sli",
                    10,
                    -1,
                    999,
                    0,
                    0,
                    0,
                    0,
                    20,
                    0,
                    0,
                    0,
                    false,
                    []),
                0,
                0,
                0);

        var scene =
            new NativeSceneSnapshot(
                [],
                [],
                [spline],
                []);

        var snap =
            NativeSplineEndpointSnapFinder
                .FindFreeEndpoint(
                    scene,
                    new Vector3(
                        0,
                        0,
                        20),
                    NativeSplineEndpointKind.End,
                    5);

        Assert.NotNull(
            snap);

        Assert.Equal(
            10,
            snap!.SplineId);
    }
}
