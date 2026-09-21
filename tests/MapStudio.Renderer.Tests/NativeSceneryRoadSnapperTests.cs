using System.Numerics;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Renderer.Picking;
using MapStudio.Renderer.Scene;
using MapStudio.Renderer.Viewport;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeSceneryRoadSnapperTests
{
    [Fact]
    public void SnapsToStraightRoadAndAlignsHeading()
    {
        var spline =
            CreateSpline(
                id: 10,
                length: 100,
                radius: 0,
                rotation: 0,
                heightSpline: false);

        var scene =
            new NativeSceneSnapshot(
                [],
                [],
                [spline],
                []);

        var snap =
            NativeSceneryRoadSnapper
                .FindNearest(
                    scene,
                    new Vector3(
                        3,
                        0,
                        40),
                    8);

        Assert.NotNull(
            snap);

        Assert.Equal(
            10,
            snap!.SplineId);

        Assert.InRange(
            snap.WorldPoint.X,
            -0.01f,
            0.01f);

        Assert.InRange(
            snap.WorldPoint.Z,
            39.9f,
            40.1f);

        Assert.InRange(
            snap.Rotation,
            0,
            0.1);

        Assert.InRange(
            snap.Distance,
            2.9,
            3.1);
    }

    [Fact]
    public void SnapsToCurvedRoadUsingRealSplineTangent()
    {
        var length =
            Math.PI *
            25.0;

        var spline =
            CreateSpline(
                id: 11,
                length:
                    length,
                radius:
                    50,
                rotation:
                    0,
                heightSpline:
                    false);

        var target =
            NativeSplinePathMath
                .GetFrame(
                    spline,
                    length /
                    2.0);

        var probe =
            target.Center +
            target.Lateral *
            2.0f;

        var scene =
            new NativeSceneSnapshot(
                [],
                [],
                [spline],
                []);

        var snap =
            NativeSceneryRoadSnapper
                .FindNearest(
                    scene,
                    probe,
                    5);

        Assert.NotNull(
            snap);

        Assert.InRange(
            Vector2.Distance(
                new Vector2(
                    snap!.WorldPoint.X,
                    snap.WorldPoint.Z),
                new Vector2(
                    target.Center.X,
                    target.Center.Z)),
            0,
            0.05f);

        Assert.InRange(
            snap.Rotation,
            44.9,
            45.1);

        Assert.InRange(
            snap.Distance,
            1.9,
            2.1);
    }

    [Fact]
    public void IgnoresHeightSplinesAndOutOfRangeRoads()
    {
        var heightSpline =
            CreateSpline(
                id: 12,
                length: 100,
                radius: 0,
                rotation: 0,
                heightSpline: true);

        var scene =
            new NativeSceneSnapshot(
                [],
                [],
                [heightSpline],
                []);

        Assert.Null(
            NativeSceneryRoadSnapper
                .FindNearest(
                    scene,
                    new Vector3(
                        1,
                        0,
                        10),
                    8));

        var road =
            CreateSpline(
                id: 13,
                length: 100,
                radius: 0,
                rotation: 0,
                heightSpline: false);

        scene =
            new NativeSceneSnapshot(
                [],
                [],
                [road],
                []);

        Assert.Null(
            NativeSceneryRoadSnapper
                .FindNearest(
                    scene,
                    new Vector3(
                        20,
                        0,
                        10),
                    8));
    }

    private static NativeSplineEntity
        CreateSpline(
            int id,
            double length,
            double radius,
            double rotation,
            bool heightSpline)
    {
        var tile =
            new OmsiTileReference(
                0,
                0,
                "tile.map");

        return new NativeSplineEntity(
            new PickingId(
                PickingKind.Spline,
                id),
            tile,
            new OmsiPlacedSpline(
                "spline",
                @"Splines\Roads\road.sli",
                id,
                -1,
                -1,
                0,
                0,
                0,
                rotation,
                length,
                radius,
                0,
                0,
                heightSpline,
                []),
            0,
            0,
            0);
    }
}
