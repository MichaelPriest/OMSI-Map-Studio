using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Scene;
using MapStudio.Core.Omsi.Maps;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioInfrastructureFeatureClipperTests
{
    [Fact]
    public void AreaIsClippedToCreatedTileBounds()
    {
        var feature =
            new MapStudioProjectedInfrastructureFeature(
                "parking-large",
                MapStudioOsmInfrastructureKind.Parking,
                [
                    new MapStudioRoadPoint(-120, -80),
                    new MapStudioRoadPoint(520, -80),
                    new MapStudioRoadPoint(520, 460),
                    new MapStudioRoadPoint(-120, 460)
                ],
                new MapStudioRoadPoint(200, 190),
                "Large parking",
                "asphalt",
                null,
                true);

        var clipped =
            MapStudioInfrastructureFeatureClipper
                .ClipToBounds(
                    feature,
                    new OmsiTileWorldBounds(
                        0,
                        0,
                        300,
                        300));

        var result =
            Assert.Single(
                clipped);

        Assert.All(
            result.Points,
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

        Assert.InRange(
            result.Center.X,
            0.009,
            299.991);

        Assert.InRange(
            result.Center.Z,
            0.009,
            299.991);
    }

    [Fact]
    public void LineCrossingMapIsClippedInsteadOfGeneratingOutlierMesh()
    {
        var feature =
            new MapStudioProjectedInfrastructureFeature(
                "sidewalk-crossing",
                MapStudioOsmInfrastructureKind.Sidewalk,
                [
                    new MapStudioRoadPoint(-100, 150),
                    new MapStudioRoadPoint(400, 150)
                ],
                new MapStudioRoadPoint(150, 150),
                null,
                "concrete",
                2,
                false);

        var clipped =
            MapStudioInfrastructureFeatureClipper
                .ClipToBounds(
                    feature,
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
            result.Points[1].X,
            299.989,
            299.991);
    }
}
