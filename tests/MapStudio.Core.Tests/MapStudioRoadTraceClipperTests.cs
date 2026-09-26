using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Omsi.Maps;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioRoadTraceClipperTests
{
    [Fact]
    public void CrossingWayIsClippedInsideTileBounds()
    {
        var trace =
            new MapStudioRoadTrace(
                "crossing",
                [
                    new(-120, 150),
                    new(420, 150)
                ],
                "road");

        var clipped =
            MapStudioRoadTraceClipper
                .ClipToBounds(
                    trace,
                    new OmsiTileWorldBounds(
                        0,
                        0,
                        300,
                        300));

        var result =
            Assert.Single(
                clipped);

        Assert.Equal(
            2,
            result.Points.Count);

        Assert.InRange(
            result.Points[0].X,
            0.009,
            0.011);

        Assert.InRange(
            result.Points[^1].X,
            299.989,
            299.991);

        Assert.Equal(
            150,
            result.Points[0].Z,
            3);

        Assert.Equal(
            150,
            result.Points[^1].Z,
            3);
    }

    [Fact]
    public void TraceLeavingAndReenteringBoundsBecomesSeparateFragments()
    {
        var trace =
            new MapStudioRoadTrace(
                "loop",
                [
                    new(20, 20),
                    new(150, 20),
                    new(350, 20),
                    new(350, 280),
                    new(150, 280),
                    new(20, 280)
                ],
                "road",
                2,
                false,
                7);

        var clipped =
            MapStudioRoadTraceClipper
                .ClipToBounds(
                    trace,
                    new OmsiTileWorldBounds(
                        0,
                        0,
                        300,
                        300));

        Assert.Equal(
            2,
            clipped.Count);

        Assert.All(
            clipped,
            item =>
            {
                Assert.Equal(
                    2,
                    item.LaneCount);

                Assert.False(
                    item.OneWay);

                Assert.Equal(
                    7,
                    item.WidthMeters);

                Assert.All(
                    item.Points,
                    point =>
                    {
                        Assert.InRange(
                            point.X,
                            0.009,
                            299.991);

                        Assert.InRange(
                            point.Z,
                            0.009,
                            299.991);
                    });
            });
    }

    [Fact]
    public void TraceFullyOutsideBoundsIsDiscarded()
    {
        var clipped =
            MapStudioRoadTraceClipper
                .ClipToBounds(
                    new MapStudioRoadTrace(
                        "outside",
                        [
                            new(-50, -50),
                            new(-10, -10)
                        ],
                        "road"),
                    new OmsiTileWorldBounds(
                        0,
                        0,
                        300,
                        300));

        Assert.Empty(
            clipped);
    }
}
