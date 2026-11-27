using MapStudio.Core.Generation.Roads;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioGeographicProjectionTests
{
    [Fact]
    public void ProjectionUsesEastPositiveAndNorthNegativeZ()
    {
        var anchor =
            new MapStudioGeographicAnchor(
                -23.55,
                -46.63,
                1000,
                2000);

        var east =
            MapStudioGeographicProjection
                .Project(
                    anchor,
                    new MapStudioGeoRoadPoint(
                        -23.55,
                        -46.629));

        var north =
            MapStudioGeographicProjection
                .Project(
                    anchor,
                    new MapStudioGeoRoadPoint(
                        -23.549,
                        -46.63));

        Assert.True(
            east.X >
            anchor.WorldX);

        Assert.InRange(
            east.Z,
            1999.99,
            2000.01);

        Assert.True(
            north.Z <
            anchor.WorldZ);

        Assert.InRange(
            north.X,
            999.99,
            1000.01);
    }

    [Fact]
    public void ProjectionRoundTripsWithinCentimeterScale()
    {
        var anchor =
            new MapStudioGeographicAnchor(
                -23.55,
                -46.63,
                150,
                150);

        var source =
            new MapStudioGeoRoadPoint(
                -23.54873,
                -46.62641);

        var projected =
            MapStudioGeographicProjection
                .Project(
                    anchor,
                    source);

        var roundTrip =
            MapStudioGeographicProjection
                .Unproject(
                    anchor,
                    projected);

        Assert.InRange(
            Math.Abs(
                roundTrip.Latitude -
                source.Latitude),
            0,
            1e-10);

        Assert.InRange(
            Math.Abs(
                roundTrip.Longitude -
                source.Longitude),
            0,
            1e-10);
    }
}
