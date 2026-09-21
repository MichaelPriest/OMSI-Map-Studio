using MapStudio.Core.AI;
using MapStudio.Core.Generation.Buildings;
using MapStudio.Core.Generation.Roads;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioOsmBuildingProjectorTests
{
    [Fact]
    public void ProjectorPreservesPolygonAndConvertsHeightMetadata()
    {
        var source =
            new MapStudioOsmBuildingFootprint(
                "building-1",
                [
                    new(-23.5500, -46.6300),
                    new(-23.5500, -46.6299),
                    new(-23.5499, -46.6299),
                    new(-23.5499, -46.6300)
                ],
                "apartments",
                "Edifício",
                5,
                17,
                "hipped",
                2,
                "Rua Teste",
                "10");

        var result =
            new MapStudioOsmBuildingProjector()
                .Project(
                    [source],
                    new MapStudioGeographicAnchor(
                        -23.55,
                        -46.63,
                        100,
                        200));

        var building =
            Assert.Single(
                result);

        Assert.Equal(
            4,
            building.Points.Count);

        Assert.Equal(
            5,
            building.FloorCount);

        Assert.Equal(
            15,
            building.WallHeightMeters,
            4);

        Assert.Equal(
            MapStudioBuildingRoofType.Hip,
            building.RoofType);

        Assert.Equal(
            2,
            building.RoofHeightMeters);

        Assert.InRange(
            building.Center.X,
            104,
            106);

        Assert.InRange(
            building.Center.Z,
            193,
            195);
    }

    [Fact]
    public void ProjectorDefaultsMissingHeightAndUnknownRoofSafely()
    {
        var source =
            new MapStudioOsmBuildingFootprint(
                "building-1",
                [
                    new(-23.55, -46.63),
                    new(-23.55, -46.62995),
                    new(-23.54995, -46.62995)
                ],
                "yes",
                null,
                null,
                null,
                "unknown-shape",
                null,
                null,
                null);

        var building =
            Assert.Single(
                new MapStudioOsmBuildingProjector()
                    .Project(
                        [source],
                        new MapStudioGeographicAnchor(
                            -23.55,
                            -46.63,
                            0,
                            0)));

        Assert.Equal(
            1,
            building.FloorCount);

        Assert.Equal(
            3,
            building.WallHeightMeters);

        Assert.Equal(
            MapStudioBuildingRoofType.Flat,
            building.RoofType);

        Assert.Equal(
            0,
            building.RoofHeightMeters);
    }
}
