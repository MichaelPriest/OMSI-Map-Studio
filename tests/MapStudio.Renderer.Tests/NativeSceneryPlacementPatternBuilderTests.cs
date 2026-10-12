using System.Numerics;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Renderer.Viewport;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeSceneryPlacementPatternBuilderTests
{
    private static NativeSceneryPlacementRequest
        Request(
            double worldX,
            double worldZ,
            double rotation = 0) =>
        new(
            new OmsiTileReference(
                (int)Math.Floor(
                    worldX /
                    300.0),
                (int)Math.Floor(
                    worldZ /
                    300.0),
                "tile.map"),
            @"Sceneryobjects\Test\object.sco",
            worldX % 300,
            worldZ % 300,
            0,
            rotation,
            0,
            0,
            new Vector3(
                (float)worldX,
                0,
                (float)worldZ),
            false);

    [Fact]
    public void NonFinitePatternParametersNeverProduceInvalidPlacements()
    {
        var center = Request(10, 20);

        var area = NativeSceneryPlacementPatternBuilder.BuildArea(
            center, double.NaN, 8, false);
        var matrix = NativeSceneryPlacementPatternBuilder.BuildMatrix(
            center, 2, 3, double.PositiveInfinity, double.NaN, false);
        var circle = NativeSceneryPlacementPatternBuilder.BuildCircle(
            center, double.NegativeInfinity, 8, true, false);
        var lot = NativeSceneryPlacementPatternBuilder.BuildLot(
            Request(0, 0), Request(20, 0), double.PositiveInfinity,
            double.NaN, false);

        Assert.All(area, HasFiniteCoordinates);
        Assert.All(matrix, HasFiniteCoordinates);
        Assert.All(circle, HasFiniteCoordinates);
        Assert.All(lot, HasFiniteCoordinates);
    }

    [Fact]
    public void NonFiniteWorldPointIsRejected()
    {
        var invalid = Request(0, 0) with
        {
            WorldPoint = new Vector3(float.NaN, 0, 0)
        };

        Assert.Empty(
            NativeSceneryPlacementPatternBuilder.BuildArea(
                invalid, 10, 5, false));

        Assert.Empty(
            NativeSceneryPlacementPatternBuilder.BuildLine(
                invalid, Request(10, 0), 2, false));
    }

    private static void HasFiniteCoordinates(
        NativeSceneryPatternPlacement placement)
    {
        Assert.True(double.IsFinite(placement.WorldX));
        Assert.True(double.IsFinite(placement.WorldZ));
        Assert.True(double.IsFinite(placement.Z));
        Assert.True(double.IsFinite(placement.Rotation));
        Assert.True(double.IsFinite(placement.Pitch));
        Assert.True(double.IsFinite(placement.Bank));
    }

    [Fact]
    public void LineUsesSpacingAndAlignsRotation()
    {
        var result =
            NativeSceneryPlacementPatternBuilder
                .BuildLine(
                    Request(0, 0),
                    Request(0, 20),
                    5,
                    false);

        Assert.Equal(
            5,
            result.Count);

        Assert.All(
            result,
            item =>
                Assert.InRange(
                    item.Rotation,
                    -0.001,
                    0.001));

        Assert.Equal(
            20,
            result[^1].WorldZ);
    }

    [Fact]
    public void LongLineRespectsCapAndStillReachesEndpoint()
    {
        var result =
            NativeSceneryPlacementPatternBuilder
                .BuildLine(
                    Request(0, 0),
                    Request(10000, 0),
                    1,
                    false);

        Assert.Equal(
            256,
            result.Count);

        Assert.Equal(
            0,
            result[0].WorldX,
            6);

        Assert.Equal(
            10000,
            result[^1].WorldX,
            6);
    }

    [Fact]
    public void NonFiniteSpacingFallsBackToSafeMinimum()
    {
        var result =
            NativeSceneryPlacementPatternBuilder
                .BuildLine(
                    Request(0, 0),
                    Request(0, 2),
                    double.NaN,
                    false);

        Assert.Equal(
            5,
            result.Count);

        Assert.Equal(
            2,
            result[^1].WorldZ,
            6);
    }

    [Fact]
    public void MatrixIsCenteredAndCapped()
    {
        var result =
            NativeSceneryPlacementPatternBuilder
                .BuildMatrix(
                    Request(100, 100),
                    16,
                    16,
                    2,
                    4,
                    false);

        Assert.Equal(
            256,
            result.Count);

        Assert.Contains(
            result,
            item =>
                item.WorldX <
                    100 &&
                item.WorldZ <
                    100);

        Assert.Contains(
            result,
            item =>
                item.WorldX >
                    100 &&
                item.WorldZ >
                    100);
    }

    [Fact]
    public void CircleUsesTangentRotation()
    {
        var result =
            NativeSceneryPlacementPatternBuilder
                .BuildCircle(
                    Request(50, 50),
                    10,
                    4,
                    true,
                    false);

        Assert.Equal(
            4,
            result.Count);

        Assert.InRange(
            result[0].WorldX,
            59.999,
            60.001);

        Assert.InRange(
            result[0].Rotation,
            89.999,
            90.001);
    }

    [Fact]
    public void AreaUsesRequestedCount()
    {
        var result =
            NativeSceneryPlacementPatternBuilder
                .BuildArea(
                    Request(20, 20),
                    15,
                    30,
                    true);

        Assert.Equal(
            30,
            result.Count);

        Assert.All(
            result,
            item =>
            {
                var dx =
                    item.WorldX -
                    20;

                var dz =
                    item.WorldZ -
                    20;

                Assert.True(
                    Math.Sqrt(
                        dx * dx +
                        dz * dz) <=
                    15.001);
            });
    }

    [Fact]
    public void LotAppliesSetback()
    {
        var result =
            NativeSceneryPlacementPatternBuilder
                .BuildLot(
                    Request(0, 0),
                    Request(20, 0),
                    10,
                    5,
                    false);

        Assert.Equal(
            3,
            result.Count);

        Assert.All(
            result,
            item =>
                Assert.InRange(
                    item.WorldZ,
                    4.999,
                    5.001));
    }
}
