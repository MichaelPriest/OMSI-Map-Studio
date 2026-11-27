using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Omsi.Structures;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioOsmJunctionControlReaderTests
{
    [Fact]
    public void ReaderProjectsTrafficSignalsAndCrossings()
    {
        var controls =
            new MapStudioOsmJunctionControlReader()
                .Parse(
                    """
                    <osm version="0.6">
                      <node id="10" lat="-23.55000" lon="-46.63000">
                        <tag k="highway" v="traffic_signals"/>
                      </node>
                      <node id="20" lat="-23.55000" lon="-46.62990">
                        <tag k="highway" v="crossing"/>
                        <tag k="crossing" v="marked"/>
                      </node>
                      <node id="30" lat="-23.55010" lon="-46.63000">
                        <tag k="highway" v="crossing"/>
                        <tag k="crossing:signals" v="yes"/>
                      </node>
                    </osm>
                    """,
                    new MapStudioGeographicAnchor(
                        -23.55000,
                        -46.63000,
                        150,
                        150));

        Assert.Equal(
            4,
            controls.Count);

        Assert.Contains(
            controls,
            item =>
                item.NodeId ==
                    10 &&
                item.Kind ==
                    MapStudioOsmJunctionControlKind
                        .TrafficSignal);

        Assert.Contains(
            controls,
            item =>
                item.NodeId ==
                    20 &&
                item.Kind ==
                    MapStudioOsmJunctionControlKind
                        .Crosswalk);

        Assert.Contains(
            controls,
            item =>
                item.NodeId ==
                    30 &&
                item.Kind ==
                    MapStudioOsmJunctionControlKind
                        .Crosswalk);

        Assert.Contains(
            controls,
            item =>
                item.NodeId ==
                    30 &&
                item.Kind ==
                    MapStudioOsmJunctionControlKind
                        .TrafficSignal);

        var center =
            Assert.Single(
                controls,
                item =>
                    item.NodeId ==
                        10);

        Assert.InRange(
            center.WorldPoint.X,
            149.999,
            150.001);

        Assert.InRange(
            center.WorldPoint.Z,
            149.999,
            150.001);
    }
}
