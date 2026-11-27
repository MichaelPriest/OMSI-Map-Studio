using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Omsi.Splines;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioStandardRoadProfileSelectorTests
{
    [Theory]
    [InlineData("service", null, false, 4.2, "local-5.5")]
    [InlineData("track", null, false, null, "local-5.5")]
    [InlineData("residential", 2, false, null, "local-5.5-sidewalk")]
    [InlineData("living_street", 2, false, 5.0, "local-5.5-sidewalk")]
    [InlineData("tertiary", 2, false, 7.0, "road-7-sidewalk")]
    [InlineData("secondary", 2, false, 7.0, "road-7-sidewalk")]
    [InlineData("primary", 4, false, 14.0, "avenue-4")]
    [InlineData("motorway", 4, false, 14.0, "avenue-divided-4")]
    [InlineData("residential", 1, true, 3.5, "oneway-1")]
    [InlineData("primary", 2, true, 7.0, "oneway-2")]
    [InlineData("footway", null, false, 2.0, "pedestrian-3")]
    public void SelectorUsesExpectedStandardAsset(
        string kind,
        int? laneCount,
        bool? oneWay,
        double? widthMeters,
        string expectedKey)
    {
        var profile =
            MapStudioStandardRoadProfileSelector
                .Select(
                    kind,
                    laneCount,
                    oneWay,
                    widthMeters);

        Assert.Equal(
            expectedKey,
            profile.Key);
    }

    [Fact]
    public void CatalogKeepsPhysicalWidthsInOmsiMeters()
    {
        Assert.Equal(
            5.5,
            MapStudioStandardRoadCatalog
                .LocalNarrow
                .CarriagewayWidthMeters,
            3);

        Assert.Equal(
            8.5,
            MapStudioStandardRoadCatalog
                .LocalWithSidewalk
                .TotalWidthMeters,
            3);

        Assert.Equal(
            7.0,
            MapStudioStandardRoadCatalog
                .RoadTwoLane
                .CarriagewayWidthMeters,
            3);

        Assert.Equal(
            11.0,
            MapStudioStandardRoadCatalog
                .RoadTwoLaneWithSidewalk
                .TotalWidthMeters,
            3);

        Assert.Equal(
            20.0,
            MapStudioStandardRoadCatalog
                .DividedAvenueFourLane
                .TotalWidthMeters,
            3);
    }

    [Fact]
    public void CatalogPathsPointOnlyToMapStudioRoadKit()
    {
        Assert.All(
            MapStudioStandardRoadCatalog
                .Profiles,
            profile =>
                Assert.StartsWith(
                    @"Splines\MapStudio_RoadKit\",
                    profile.RelativePath));
    }

    [Theory]
    [InlineData(@"Splines\MapStudio_RoadKit\ms_road_2lane_7m_bridge.sli")]
    [InlineData(@"Splines\MapStudio_RoadKit\ms_road_2lane_7m_tunnel.sli")]
    [InlineData(@"Splines\MapStudio_Bridges\ms_bridge_city.sli")]
    [InlineData(@"Splines/MapStudio_Tunnels/ms_tunnel_city.sli")]
    public void CatalogProtectsStructuralSplinesFromTerrainConform(
        string assetPath)
    {
        Assert.True(
            MapStudioStandardRoadCatalog
                .IsTerrainConformProtectedPath(
                    assetPath));
    }

    [Theory]
    [InlineData(@"Splines\MapStudio_RoadKit\ms_road_2lane_7m.sli")]
    [InlineData(@"Splines\Marcel\road.sli")]
    [InlineData("")]
    public void CatalogAllowsOrdinarySplinesToConformTerrain(
        string assetPath)
    {
        Assert.False(
            MapStudioStandardRoadCatalog
                .IsTerrainConformProtectedPath(
                    assetPath));
    }


}
