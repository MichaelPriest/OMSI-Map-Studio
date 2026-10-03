using MapStudio.Core.AI;
using MapStudio.Core.Generation.Roads;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioRoadReferenceProjectorTests
{
    [Fact]
    public void NormalizedImageProjectsAroundReferenceCenter()
    {
        var analysis =
            new MapStudioRoadReferenceAnalysis(
                [
                    new MapStudioRoadReferencePolyline(
                        "primary",
                        [
                            new(0.5, 0.5),
                            new(0.75, 0.5)
                        ],
                        LaneCount:
                            2)
                ],
                null,
                0.9,
                MapStudioRoadReferenceCoordinateSpace
                    .NormalizedImage);

        var projected =
            MapStudioRoadReferenceProjector
                .Project(
                    analysis,
                    new MapStudioRoadReferenceImageProjection(
                        400,
                        200,
                        2,
                        1000,
                        2000));

        var road =
            Assert.Single(
                projected);

        Assert.Equal(
            1000,
            road.Points[0].X,
            6);

        Assert.Equal(
            2000,
            road.Points[0].Z,
            6);

        Assert.Equal(
            1200,
            road.Points[1].X,
            6);

        Assert.Equal(
            2000,
            road.Points[1].Z,
            6);
    }

    [Fact]
    public void PixelCoordinatesRespectSourceImageDimensions()
    {
        var analysis =
            new MapStudioRoadReferenceAnalysis(
                [
                    new MapStudioRoadReferencePolyline(
                        "road",
                        [
                            new(100, 50),
                            new(200, 50)
                        ])
                ],
                null,
                0.8,
                MapStudioRoadReferenceCoordinateSpace
                    .ImagePixels,
                ImageWidth:
                    200,
                ImageHeight:
                    100);

        var projected =
            MapStudioRoadReferenceProjector
                .Project(
                    analysis,
                    new MapStudioRoadReferenceImageProjection(
                        400,
                        200,
                        1,
                        0,
                        0));

        var road =
            Assert.Single(
                projected);

        Assert.Equal(
            0,
            road.Points[0].X,
            6);

        Assert.Equal(
            200,
            road.Points[1].X,
            6);
    }

    [Fact]
    public void WorldMetersPassThroughWithoutImageProjection()
    {
        var analysis =
            new MapStudioRoadReferenceAnalysis(
                [
                    new MapStudioRoadReferencePolyline(
                        "service",
                        [
                            new(12, 34),
                            new(56, 78)
                        ])
                ],
                null,
                1,
                MapStudioRoadReferenceCoordinateSpace
                    .WorldMeters);

        var projected =
            MapStudioRoadReferenceProjector
                .Project(
                    analysis,
                    new MapStudioRoadReferenceImageProjection(
                        640,
                        640,
                        1,
                        999,
                        999));

        var road =
            Assert.Single(
                projected);

        Assert.Equal(
            12,
            road.Points[0].X,
            6);

        Assert.Equal(
            34,
            road.Points[0].Z,
            6);

        Assert.Equal(
            56,
            road.Points[1].X,
            6);

        Assert.Equal(
            78,
            road.Points[1].Z,
            6);
    }
}
