using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Scene;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioOsmInfrastructureProjectorTests
{
    [Fact]
    public void ProjectorConvertsClosedParkingToLocalWorldGeometry()
    {
        var feature =
            new MapStudioGeoInfrastructureFeature(
                "parking-1",
                MapStudioOsmInfrastructureKind.Parking,
                [
                    new(-23.5500, -46.6300),
                    new(-23.5500, -46.6299),
                    new(-23.5501, -46.6299),
                    new(-23.5501, -46.6300),
                    new(-23.5500, -46.6300)
                ],
                "Parking",
                "asphalt",
                12.0,
                true);

        var projected =
            Assert.Single(
                new MapStudioOsmInfrastructureProjector()
                    .Project(
                        [feature],
                        new MapStudioGeographicAnchor(
                            -23.5500,
                            -46.6300,
                            1000,
                            2000)));

        Assert.Equal(
            4,
            projected.Points.Count);

        Assert.True(
            projected.IsArea);

        Assert.InRange(
            projected.Center.X,
            1000,
            1020);

        Assert.InRange(
            projected.Center.Z,
            2000,
            2020);

        Assert.Equal(
            12.0,
            projected.WidthMeters);
    }

    [Fact]
    public void ProjectorRejectsDegenerateLinearGeometry()
    {
        var feature =
            new MapStudioGeoInfrastructureFeature(
                "wall-1",
                MapStudioOsmInfrastructureKind.Wall,
                [
                    new(-23.55, -46.63),
                    new(-23.55, -46.63)
                ],
                null,
                null,
                null,
                false);

        var projected =
            new MapStudioOsmInfrastructureProjector()
                .Project(
                    [feature],
                    new MapStudioGeographicAnchor(
                        -23.55,
                        -46.63,
                        0,
                        0));

        Assert.Empty(projected);
    }
}
