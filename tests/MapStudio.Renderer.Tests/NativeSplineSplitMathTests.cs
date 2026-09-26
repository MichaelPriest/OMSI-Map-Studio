using System.Numerics;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Renderer.Picking;
using MapStudio.Renderer.Scene;
using MapStudio.Renderer.Viewport;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeSplineSplitMathTests
{
    [Fact]
    public void SplitStraightSplineAtPointerProducesTwoSegments()
    {
        var tileSummary =
            new OmsiTileSummary(
                Exists:
                    true,
                ObjectCount:
                    0,
                SplineCount:
                    1,
                SplineAttachmentCount:
                    0);

        var tile =
            new OmsiTileReference(
                0,
                0,
                "tile_0_0.map",
                tileSummary);

        var spline =
            new OmsiPlacedSpline(
                "0",
                @"Splines\Road.sli",
                10,
                -1,
                11,
                10,
                20,
                2,
                0,
                100,
                0,
                1,
                3,
                false,
                []);

        var entity =
            new NativeSplineEntity(
                new PickingId(
                    PickingKind.Spline,
                    10),
                tile,
                spline,
                10,
                2,
                20);

        var scene =
            new NativeSceneSnapshot(
                [
                    new NativeSceneTile(
                        tile,
                        new OmsiTileContent(
                            tileSummary,
                            [],
                            [spline]))
                ],
                [],
                [entity],
                []);

        var selection =
            new NativeSelectionInfo(
                PickingKind.Spline,
                10,
                0,
                0,
                spline.SplinePath,
                spline.X,
                spline.Y,
                spline.Z,
                spline.Rotation,
                null,
                null,
                spline.Length,
                spline.Radius,
                spline.GradientStart,
                spline.GradientEnd,
                spline.PreviousSplineId,
                spline.NextSplineId,
                spline.IsHeightSpline);

        var ok =
            NativeSplineSplitMath
                .TryCreateRequest(
                    scene,
                    entity,
                    selection,
                    new Vector3(
                        10,
                        2,
                        70),
                    out var request,
                    out _);

        Assert.True(
            ok);

        Assert.NotNull(
            request);

        Assert.InRange(
            request!.SplitDistance,
            49.9,
            50.1);

        Assert.InRange(
            request.SecondLength,
            49.9,
            50.1);

        Assert.InRange(
            request.FirstGradientEnd,
            1.99,
            2.01);

        Assert.InRange(
            request.SecondGradientStart,
            1.99,
            2.01);
    }
}
