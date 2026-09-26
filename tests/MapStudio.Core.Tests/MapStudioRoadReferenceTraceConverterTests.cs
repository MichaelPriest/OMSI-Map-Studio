using MapStudio.Core.AI;
using MapStudio.Core.Generation.Roads;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioRoadReferenceTraceConverterTests
{
    [Fact]
    public void NormalizedImageCoordinatesProjectOntoReferenceMeters()
    {
        var analysis =
            new MapStudioRoadReferenceAnalysis(
                [
                    new MapStudioRoadReferencePolyline(
                        "road",
                        [
                            new(0, 0.5),
                            new(1, 0.5)
                        ],
                        LaneCount:
                            2,
                        OneWay:
                            false,
                        WidthMeters:
                            7)
                ],
                null,
                0.95);

        var result =
            new MapStudioRoadReferenceTraceConverter()
                .Convert(
                    analysis,
                    new MapStudioRoadReferenceProjection(
                        1000,
                        500,
                        0.5,
                        2000,
                        3000),
                    _ =>
                        "road.sli");

        var trace =
            Assert.Single(
                result.Traces);

        Assert.Equal(
            1750,
            trace.Points[0].X,
            6);

        Assert.Equal(
            3000,
            trace.Points[0].Z,
            6);

        Assert.Equal(
            2250,
            trace.Points[1].X,
            6);

        Assert.Equal(
            3000,
            trace.Points[1].Z,
            6);

        Assert.Equal(
            0,
            result.SkippedRoads);

        Assert.Equal(
            0,
            result.SkippedPoints);
    }

    [Fact]
    public void PixelCoordinatesUseAnalysisImageDimensions()
    {
        var analysis =
            new MapStudioRoadReferenceAnalysis(
                [
                    new MapStudioRoadReferencePolyline(
                        "avenue",
                        [
                            new(100, 100),
                            new(300, 100)
                        ])
                ],
                null,
                0.8,
                MapStudioRoadReferenceCoordinateSpace
                    .ImagePixels,
                ImageWidth:
                    400,
                ImageHeight:
                    200);

        var result =
            new MapStudioRoadReferenceTraceConverter()
                .Convert(
                    analysis,
                    new MapStudioRoadReferenceProjection(
                        800,
                        400,
                        1,
                        0,
                        0),
                    _ =>
                        "avenue.sli");

        var trace =
            Assert.Single(
                result.Traces);

        Assert.Equal(
            -200,
            trace.Points[0].X,
            6);

        Assert.Equal(
            0,
            trace.Points[0].Z,
            6);

        Assert.Equal(
            200,
            trace.Points[1].X,
            6);
    }

    [Fact]
    public void InvalidDetectedPointsAreSkippedWithoutInventingGeometry()
    {
        var analysis =
            new MapStudioRoadReferenceAnalysis(
                [
                    new MapStudioRoadReferencePolyline(
                        "road",
                        [
                            new(-1, 0.5),
                            new(0.2, 0.5),
                            new(0.8, 0.5)
                        ])
                ],
                null,
                0.7);

        var result =
            new MapStudioRoadReferenceTraceConverter()
                .Convert(
                    analysis,
                    new MapStudioRoadReferenceProjection(
                        100,
                        100,
                        1,
                        0,
                        0),
                    _ =>
                        "road.sli");

        var trace =
            Assert.Single(
                result.Traces);

        Assert.Equal(
            2,
            trace.Points.Count);

        Assert.Equal(
            1,
            result.SkippedPoints);
    }
}
