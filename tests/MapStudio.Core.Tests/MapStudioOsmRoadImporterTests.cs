using MapStudio.Core.Generation.Roads;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioOsmRoadImporterTests
{
    [Fact]
    public void ImporterReadsHighwayTagsAndNodeGeometry()
    {
        const string xml =
            """
            <osm version="0.6">
              <node id="1" lat="-23.5500" lon="-46.6300" />
              <node id="2" lat="-23.5500" lon="-46.6290" />
              <node id="3" lat="-23.5495" lon="-46.6285" />
              <way id="100">
                <nd ref="1" />
                <nd ref="2" />
                <nd ref="3" />
                <tag k="highway" v="primary" />
                <tag k="lanes" v="4" />
                <tag k="oneway" v="no" />
                <tag k="width" v="14.5 m" />
                <tag k="name" v="Avenida Teste" />
              </way>
            </osm>
            """;

        var result =
            new MapStudioOsmRoadImporter()
                .Parse(
                    xml);

        var road =
            Assert.Single(
                result.Traces);

        Assert.Equal(
            "osm-way-100",
            road.Id);

        Assert.Equal(
            "primary",
            road.Highway);

        Assert.Equal(
            4,
            road.LaneCount);

        Assert.False(
            road.OneWay);

        Assert.Equal(
            14.5,
            road.WidthMeters);

        Assert.Equal(
            "Avenida Teste",
            road.Name);

        Assert.Equal(
            3,
            road.Points.Count);

        Assert.Equal(
            0,
            result.IgnoredWayCount);

        Assert.Equal(
            0,
            result.MissingNodeReferenceCount);
    }

    [Fact]
    public void ImporterReversesMinusOneOneway()
    {
        const string xml =
            """
            <osm version="0.6">
              <node id="1" lat="-23.55" lon="-46.63" />
              <node id="2" lat="-23.55" lon="-46.62" />
              <way id="7">
                <nd ref="1" />
                <nd ref="2" />
                <tag k="highway" v="service" />
                <tag k="oneway" v="-1" />
              </way>
            </osm>
            """;

        var road =
            Assert.Single(
                new MapStudioOsmRoadImporter()
                    .Parse(
                        xml)
                    .Traces);

        Assert.True(
            road.OneWay);

        Assert.Equal(
            -46.62,
            road.Points[0]
                .Longitude,
            6);

        Assert.Equal(
            -46.63,
            road.Points[1]
                .Longitude,
            6);
    }

    [Fact]
    public void ImporterTreatsRoundaboutAsImplicitOneWay()
    {
        const string xml =
            """
            <osm version="0.6">
              <node id="1" lat="-23.55" lon="-46.63" />
              <node id="2" lat="-23.55" lon="-46.62" />
              <node id="3" lat="-23.54" lon="-46.62" />
              <way id="8">
                <nd ref="1" />
                <nd ref="2" />
                <nd ref="3" />
                <nd ref="1" />
                <tag k="highway" v="tertiary" />
                <tag k="junction" v="roundabout" />
              </way>
            </osm>
            """;

        var road =
            Assert.Single(
                new MapStudioOsmRoadImporter()
                    .Parse(
                        xml)
                    .Traces);

        Assert.True(
            road.OneWay);
    }

    [Fact]
    public void ImporterIgnoresNonRoadWaysAndCountsMissingNodes()
    {
        const string xml =
            """
            <osm version="0.6">
              <node id="1" lat="-23.55" lon="-46.63" />
              <way id="10">
                <nd ref="1" />
                <nd ref="999" />
                <tag k="highway" v="residential" />
              </way>
              <way id="11">
                <nd ref="1" />
                <nd ref="1" />
                <tag k="building" v="yes" />
              </way>
            </osm>
            """;

        var result =
            new MapStudioOsmRoadImporter()
                .Parse(
                    xml);

        Assert.Empty(
            result.Traces);

        Assert.Equal(
            2,
            result.IgnoredWayCount);

        Assert.Equal(
            1,
            result.MissingNodeReferenceCount);
    }

    [Fact]
    public void ImporterRejectsNonOsmRoot()
    {
        Assert.Throws<
            InvalidDataException>(
                () =>
                    new MapStudioOsmRoadImporter()
                        .Parse(
                            "<root />"));
    }
}
