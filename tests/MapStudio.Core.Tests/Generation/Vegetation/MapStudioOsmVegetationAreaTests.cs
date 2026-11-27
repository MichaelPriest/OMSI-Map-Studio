using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Vegetation;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioOsmVegetationAreaTests
{
    [Fact]
    public void ImporterReadsForestAndScrubClosedWays()
    {
        const string xml =
            """
            <osm version="0.6">
              <node id="1" lat="0.0000" lon="0.0000" />
              <node id="2" lat="0.0000" lon="0.0010" />
              <node id="3" lat="0.0010" lon="0.0010" />
              <node id="4" lat="0.0010" lon="0.0000" />

              <way id="10">
                <nd ref="1" />
                <nd ref="2" />
                <nd ref="3" />
                <nd ref="4" />
                <nd ref="1" />
                <tag k="landuse" v="forest" />
                <tag k="species" v="Pinus" />
              </way>

              <way id="20">
                <nd ref="1" />
                <nd ref="2" />
                <nd ref="3" />
                <nd ref="4" />
                <nd ref="1" />
                <tag k="natural" v="scrub" />
                <tag k="name" v="Mato baixo" />
              </way>
            </osm>
            """;

        var result =
            new MapStudioOsmVegetationAreaImporter()
                .Parse(
                    xml);

        Assert.Equal(
            2,
            result.Areas.Count);

        var forest =
            Assert.Single(
                result.Areas,
                area =>
                    area.Kind ==
                    MapStudioOsmVegetationAreaKind
                        .Forest);

        Assert.Equal(
            "osm-vegetation-area-10",
            forest.Id);

        Assert.Equal(
            "Pinus",
            forest.Species);

        var scrub =
            Assert.Single(
                result.Areas,
                area =>
                    area.Kind ==
                    MapStudioOsmVegetationAreaKind
                        .Scrub);

        Assert.Equal(
            "Mato baixo",
            scrub.Name);

        Assert.Equal(
            0,
            result.MissingNodeReferenceCount);
    }

    [Fact]
    public void ImporterRejectsOpenVegetationWay()
    {
        const string xml =
            """
            <osm version="0.6">
              <node id="1" lat="0.0000" lon="0.0000" />
              <node id="2" lat="0.0000" lon="0.0010" />
              <node id="3" lat="0.0010" lon="0.0010" />

              <way id="10">
                <nd ref="1" />
                <nd ref="2" />
                <nd ref="3" />
                <tag k="natural" v="wood" />
              </way>
            </osm>
            """;

        var result =
            new MapStudioOsmVegetationAreaImporter()
                .Parse(
                    xml);

        Assert.Empty(
            result.Areas);

        Assert.Equal(
            1,
            result.IgnoredWayCount);
    }

    [Fact]
    public void ImporterAssemblesVegetationMultipolygonRelation()
    {
        const string xml =
            """
            <osm version="0.6">
              <node id="1" lat="0.0000" lon="0.0000" />
              <node id="2" lat="0.0000" lon="0.0010" />
              <node id="3" lat="0.0010" lon="0.0010" />
              <node id="4" lat="0.0010" lon="0.0000" />

              <way id="10">
                <nd ref="1" />
                <nd ref="2" />
                <nd ref="3" />
              </way>

              <way id="20">
                <nd ref="3" />
                <nd ref="4" />
                <nd ref="1" />
              </way>

              <relation id="30">
                <member type="way" ref="10" role="outer" />
                <member type="way" ref="20" role="outer" />
                <tag k="type" v="multipolygon" />
                <tag k="landuse" v="forest" />
                <tag k="species" v="Pinus" />
                <tag k="name" v="Bosque relation" />
              </relation>
            </osm>
            """;

        var result =
            new MapStudioOsmVegetationAreaImporter()
                .Parse(
                    xml);

        var area =
            Assert.Single(
                result.Areas);

        Assert.Equal(
            "osm-vegetation-area-relation-30",
            area.Id);

        Assert.Equal(
            MapStudioOsmVegetationAreaKind.Forest,
            area.Kind);

        Assert.Equal(
            "Pinus",
            area.Species);

        Assert.Equal(
            "Bosque relation",
            area.Name);

        Assert.Equal(
            4,
            area.Points.Count);

        Assert.Equal(
            0,
            result.IgnoredRelationCount);

        Assert.Equal(
            0,
            result.MissingNodeReferenceCount);
    }

    [Fact]
    public void ImporterRejectsVegetationMultipolygonWithInnerRing()
    {
        const string xml =
            """
            <osm version="0.6">
              <node id="1" lat="0.0000" lon="0.0000" />
              <node id="2" lat="0.0000" lon="0.0020" />
              <node id="3" lat="0.0020" lon="0.0020" />
              <node id="4" lat="0.0020" lon="0.0000" />
              <node id="5" lat="0.0005" lon="0.0005" />
              <node id="6" lat="0.0005" lon="0.0010" />
              <node id="7" lat="0.0010" lon="0.0010" />
              <node id="8" lat="0.0010" lon="0.0005" />

              <way id="10">
                <nd ref="1" />
                <nd ref="2" />
                <nd ref="3" />
                <nd ref="4" />
                <nd ref="1" />
              </way>

              <way id="20">
                <nd ref="5" />
                <nd ref="6" />
                <nd ref="7" />
                <nd ref="8" />
                <nd ref="5" />
              </way>

              <relation id="30">
                <member type="way" ref="10" role="outer" />
                <member type="way" ref="20" role="inner" />
                <tag k="type" v="multipolygon" />
                <tag k="natural" v="wood" />
              </relation>
            </osm>
            """;

        var result =
            new MapStudioOsmVegetationAreaImporter()
                .Parse(
                    xml);

        Assert.Empty(
            result.Areas);

        Assert.Equal(
            1,
            result.IgnoredRelationCount);
    }

    [Fact]
    public void ScattererIsDeterministicAndStaysInsideProjectedBounds()
    {
        var anchor =
            new MapStudioGeographicAnchor(
                0,
                0,
                100,
                200);

        var area =
            new MapStudioGeoVegetationArea(
                "forest",
                [
                    new(
                        0.0000,
                        0.0000),
                    new(
                        0.0000,
                        0.0010),
                    new(
                        0.0010,
                        0.0010),
                    new(
                        0.0010,
                        0.0000)
                ],
                MapStudioOsmVegetationAreaKind
                    .Forest,
                "Pinus",
                null,
                "needleleaved",
                "Bosque");

        var scatterer =
            new MapStudioVegetationAreaScatterer();

        var first =
            scatterer
                .ProjectAndScatter(
                    [area],
                    anchor,
                    forestSpacingMeters:
                        20.0);

        var second =
            scatterer
                .ProjectAndScatter(
                    [area],
                    anchor,
                    forestSpacingMeters:
                        20.0);

        Assert.NotEmpty(
            first);

        Assert.Equal(
            first,
            second);

        Assert.All(
            first,
            point =>
            {
                Assert.Equal(
                    MapStudioOsmVegetationKind
                        .Tree,
                    point.Kind);

                Assert.InRange(
                    point.Position.X,
                    100,
                    212);

                Assert.InRange(
                    point.Position.Z,
                    88,
                    200);

                Assert.Equal(
                    "Pinus",
                    point.Species);
            });
    }

    [Fact]
    public void ScattererHonorsMaximumPointLimit()
    {
        var anchor =
            new MapStudioGeographicAnchor(
                0,
                0,
                0,
                0);

        var area =
            new MapStudioGeoVegetationArea(
                "scrub",
                [
                    new(
                        0.0000,
                        0.0000),
                    new(
                        0.0000,
                        0.0100),
                    new(
                        0.0100,
                        0.0100),
                    new(
                        0.0100,
                        0.0000)
                ],
                MapStudioOsmVegetationAreaKind
                    .Scrub,
                null,
                null,
                null,
                null);

        var points =
            new MapStudioVegetationAreaScatterer()
                .ProjectAndScatter(
                    [area],
                    anchor,
                    scrubSpacingMeters:
                        2.0,
                    maxPoints:
                        5);

        Assert.Equal(
            5,
            points.Count);

        Assert.All(
            points,
            point =>
                Assert.Equal(
                    MapStudioOsmVegetationKind
                        .Shrub,
                    point.Kind));
    }
}
