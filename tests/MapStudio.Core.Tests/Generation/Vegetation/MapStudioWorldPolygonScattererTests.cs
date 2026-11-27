using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Vegetation;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioWorldPolygonScattererTests
{
    [Fact]
    public void ScatterFillsSquareOnRegularSpacing()
    {
        var polygon =
            new[]
            {
                new MapStudioRoadPoint(
                    0,
                    0),
                new MapStudioRoadPoint(
                    20,
                    0),
                new MapStudioRoadPoint(
                    20,
                    20),
                new MapStudioRoadPoint(
                    0,
                    20)
            };

        var points =
            new MapStudioWorldPolygonScatterer()
                .Scatter(
                    polygon,
                    5);

        Assert.Equal(
            16,
            points.Count);

        Assert.All(
            points,
            point =>
            {
                Assert.InRange(
                    point.X,
                    0,
                    20);

                Assert.InRange(
                    point.Z,
                    0,
                    20);
            });
    }

    [Fact]
    public void ScatterRespectsConcaveCutout()
    {
        var polygon =
            new[]
            {
                new MapStudioRoadPoint(
                    0,
                    0),
                new MapStudioRoadPoint(
                    20,
                    0),
                new MapStudioRoadPoint(
                    20,
                    8),
                new MapStudioRoadPoint(
                    8,
                    8),
                new MapStudioRoadPoint(
                    8,
                    20),
                new MapStudioRoadPoint(
                    0,
                    20)
            };

        var points =
            new MapStudioWorldPolygonScatterer()
                .Scatter(
                    polygon,
                    4);

        Assert.NotEmpty(
            points);

        Assert.DoesNotContain(
            points,
            point =>
                point.X >
                    8 &&
                point.Z >
                    8);
    }

    [Fact]
    public void ScatterHonorsMaximumPointCount()
    {
        var polygon =
            new[]
            {
                new MapStudioRoadPoint(
                    0,
                    0),
                new MapStudioRoadPoint(
                    100,
                    0),
                new MapStudioRoadPoint(
                    100,
                    100),
                new MapStudioRoadPoint(
                    0,
                    100)
            };

        var points =
            new MapStudioWorldPolygonScatterer()
                .Scatter(
                    polygon,
                    1,
                    maxPoints:
                        25);

        Assert.Equal(
            25,
            points.Count);
    }
}
