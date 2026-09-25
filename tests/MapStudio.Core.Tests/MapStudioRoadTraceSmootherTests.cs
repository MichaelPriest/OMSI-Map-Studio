using MapStudio.Core.Generation.Roads;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioRoadTraceSmootherTests
{
    [Fact]
    public void SmootherPreservesControlPointsAndMetadata()
    {
        var source =
            new MapStudioRoadTrace(
                "road-a",
                [
                    new(0, 0),
                    new(20, 0),
                    new(30, 10)
                ],
                "road.sli",
                LaneCount:
                    4,
                OneWay:
                    false,
                WidthMeters:
                    14);

        var trace =
            Assert.Single(
                new MapStudioRoadTraceSmoother()
                    .Smooth(
                        [source],
                        smoothness:
                            0.65,
                        maximumSampleSpacingMeters:
                            6));

        Assert.Equal(
            source.Id,
            trace.Id);

        Assert.Equal(
            source.ProfileId,
            trace.ProfileId);

        Assert.Equal(
            source.LaneCount,
            trace.LaneCount);

        Assert.Equal(
            source.OneWay,
            trace.OneWay);

        Assert.Equal(
            source.WidthMeters,
            trace.WidthMeters);

        Assert.True(
            trace.Points.Count >
            source.Points.Count);

        Assert.Equal(
            source.Points[0],
            trace.Points[0]);

        Assert.Equal(
            source.Points[^1],
            trace.Points[^1]);

        Assert.Contains(
            source.Points[1],
            trace.Points);
    }

    [Fact]
    public void SmootherAddsCurvedSamplesAroundCorner()
    {
        var trace =
            Assert.Single(
                new MapStudioRoadTraceSmoother()
                    .Smooth(
                        [
                            new MapStudioRoadTrace(
                                "corner",
                                [
                                    new(0, 0),
                                    new(20, 0),
                                    new(20, 20)
                                ],
                                "road.sli")
                        ],
                        smoothness:
                            0.75,
                        maximumSampleSpacingMeters:
                            5));

        Assert.Contains(
            trace.Points,
            point =>
                point.X >
                    0 &&
                point.X <
                    20 &&
                Math.Abs(
                    point.Z) >
                    0.001);

        Assert.Contains(
            trace.Points,
            point =>
                point.Z >
                    0 &&
                point.Z <
                    20 &&
                Math.Abs(
                    point.X -
                    20) >
                    0.001);
    }

    [Fact]
    public void TwoPointTraceIsNotInventedIntoCurve()
    {
        var source =
            new MapStudioRoadTrace(
                "straight",
                [
                    new(1, 2),
                    new(10, 20)
                ],
                "road.sli");

        var trace =
            Assert.Single(
                new MapStudioRoadTraceSmoother()
                    .Smooth(
                        [source]));

        Assert.Equal(
            source.Points,
            trace.Points);
    }

    [Fact]
    public void ZeroSmoothnessKeepsOriginalGeometry()
    {
        var source =
            new MapStudioRoadTrace(
                "road",
                [
                    new(0, 0),
                    new(10, 0),
                    new(10, 10)
                ],
                "road.sli");

        var trace =
            Assert.Single(
                new MapStudioRoadTraceSmoother()
                    .Smooth(
                        [source],
                        smoothness:
                            0));

        Assert.Equal(
            source.Points,
            trace.Points);
    }


    [Fact]
    public void ShortSegmentsAreNotSubdividedOnlyToSatisfyMinimumSampleCount()
    {
        var source =
            new MapStudioRoadTrace(
                "short",
                [
                    new(0, 0),
                    new(4, 0),
                    new(8, 0)
                ],
                "road.sli");

        var trace =
            Assert.Single(
                new MapStudioRoadTraceSmoother()
                    .Smooth(
                        [source],
                        smoothness:
                            0.65,
                        maximumSampleSpacingMeters:
                            8));

        Assert.Equal(
            source.Points,
            trace.Points);
    }

    [Fact]
    public void InvalidSmoothingOptionsAreRejected()
    {
        var smoother =
            new MapStudioRoadTraceSmoother();

        var trace =
            new MapStudioRoadTrace(
                "road",
                [
                    new(0, 0),
                    new(10, 0),
                    new(10, 10)
                ],
                "road.sli");

        Assert.Throws<
            ArgumentOutOfRangeException>(
                () =>
                    smoother.Smooth(
                        [trace],
                        smoothness:
                            1.1));

        Assert.Throws<
            ArgumentOutOfRangeException>(
                () =>
                    smoother.Smooth(
                        [trace],
                        maximumSampleSpacingMeters:
                            0));

        Assert.Throws<
            ArgumentOutOfRangeException>(
                () =>
                    smoother.Smooth(
                        [trace],
                        maximumSubdivisionsPerSegment:
                            1));
    }
}
