using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Terrain;
using MapStudio.Core.Omsi.Structures;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioRoadStructureSceneryPlannerTests
{
    [Fact]
    public void TunnelPortalsFollowRoadElevationAndCoverTerrainCut()
    {
        var segment =
            new MapStudioRoadGraphSegment(
                Id: 1,
                FromNodeId: 1,
                ToNodeId: 2,
                TraceId: "tunnel-run",
                ProfileId:
                    @"Splines\MapStudio_RoadKit\ms_road_2lane_7m_tunnel.sli",
                Start:
                    new MapStudioRoadPoint(
                        0,
                        0),
                End:
                    new MapStudioRoadPoint(
                        30,
                        0),
                LengthMeters: 30,
                LaneCount: 2,
                OneWay: false,
                WidthMeters: 7,
                Layer: -1,
                Tunnel: true,
                SourceTopologyAuthoritative:
                    true);

        var elevation =
            new MapStudioGeoreferencedElevationSurface(
                new MapStudioElevationGrid(
                    2,
                    2,
                    [
                        100,
                        100,
                        100,
                        100
                    ],
                    100,
                    100,
                    "flat-test"),
                south: -0.01,
                west: -0.01,
                north: 0.01,
                east: 0.01);

        var plan =
            new MapStudioRoadStructureSceneryPlanner()
                .Build(
                    [
                        new MapStudioRoadStructureSpline(
                            segment,
                            StartHeightMeters: -4.8,
                            EndHeightMeters: -4.8)
                    ],
                    new MapStudioGeographicAnchor(
                        0,
                        0,
                        0,
                        0),
                    elevation);

        Assert.Equal(
            2,
            plan.TunnelPortals.Count);

        Assert.All(
            plan.TunnelPortals,
            portal =>
            {
                Assert.InRange(
                    portal.HeightMeters,
                    -4.801,
                    -4.799);

                Assert.Equal(
                    7.0,
                    portal.Spec.LeftWingHeightMeters,
                    6);

                Assert.Equal(
                    7.0,
                    portal.Spec.RightWingHeightMeters,
                    6);
            });
    }
}
