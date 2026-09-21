using MapStudio.Core.Generation.Buildings;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioOsmBuildingImporterTests
{
    [Fact]
    public void ImporterPreservesClosedBuildingFootprintAndMetadata()
    {
        const string xml =
            """
            <osm version="0.6">
              <node id="1" lat="-23.5500" lon="-46.6300" />
              <node id="2" lat="-23.5500" lon="-46.6299" />
              <node id="3" lat="-23.5499" lon="-46.6299" />
              <node id="4" lat="-23.5499" lon="-46.6300" />
              <way id="200">
                <nd ref="1" />
                <nd ref="2" />
                <nd ref="3" />
                <nd ref="4" />
                <nd ref="1" />
                <tag k="building" v="apartments" />
                <tag k="building:levels" v="7" />
                <tag k="height" v="22 m" />
                <tag k="roof:shape" v="hipped" />
                <tag k="roof:height" v="2.4" />
                <tag k="name" v="Edifício Teste" />
                <tag k="addr:street" v="Rua Exemplo" />
                <tag k="addr:housenumber" v="123" />
              </way>
            </osm>
            """;

        var result =
            new MapStudioOsmBuildingImporter()
                .Parse(
                    xml);

        var building =
            Assert.Single(
                result.Buildings);

        Assert.Equal(
            "osm-building-200",
            building.Id);

        Assert.Equal(
            "apartments",
            building.BuildingType);

        Assert.Equal(
            4,
            building.Points.Count);

        Assert.Equal(
            7,
            building.Levels);

        Assert.Equal(
            22,
            building.HeightMeters);

        Assert.Equal(
            "hipped",
            building.RoofShape);

        Assert.Equal(
            2.4,
            building.RoofHeightMeters);

        Assert.Equal(
            "Edifício Teste",
            building.Name);

        Assert.Equal(
            "Rua Exemplo",
            building.Street);

        Assert.Equal(
            "123",
            building.HouseNumber);

        Assert.Equal(
            0,
            result.MissingNodeReferenceCount);

        Assert.Equal(
            0,
            result.IgnoredRelationCount);
    }

    [Fact]
    public void ImporterIgnoresNonBuildingsAndInvalidFootprints()
    {
        const string xml =
            """
            <osm version="0.6">
              <node id="1" lat="-23.55" lon="-46.63" />
              <node id="2" lat="-23.55" lon="-46.62" />
              <way id="1">
                <nd ref="1" />
                <nd ref="2" />
                <tag k="highway" v="residential" />
              </way>
              <way id="2">
                <nd ref="1" />
                <nd ref="999" />
                <tag k="building" v="yes" />
              </way>
            </osm>
            """;

        var result =
            new MapStudioOsmBuildingImporter()
                .Parse(
                    xml);

        Assert.Empty(
            result.Buildings);

        Assert.Equal(
            2,
            result.IgnoredWayCount);

        Assert.Equal(
            1,
            result.MissingNodeReferenceCount);
    }

    [Fact]
    public void ImporterCountsUnsupportedBuildingRelations()
    {
        const string xml =
            """
            <osm version="0.6">
              <relation id="10">
                <tag k="type" v="multipolygon" />
                <tag k="building" v="yes" />
              </relation>
            </osm>
            """;

        var result =
            new MapStudioOsmBuildingImporter()
                .Parse(
                    xml);

        Assert.Empty(
            result.Buildings);

        Assert.Equal(
            1,
            result.IgnoredRelationCount);
    }

    [Fact]
    public void ImporterRejectsNonOsmRoot()
    {
        Assert.Throws<
            InvalidDataException>(
                () =>
                    new MapStudioOsmBuildingImporter()
                        .Parse(
                            "<root />"));
    }
    [Fact]
    public void ImporterAssemblesBuildingMultipolygonOuterWays()
    {
        const string xml =
            """
            <osm version="0.6">
              <node id="1" lat="-23.5500" lon="-46.6300" />
              <node id="2" lat="-23.5500" lon="-46.6299" />
              <node id="3" lat="-23.5499" lon="-46.6299" />
              <node id="4" lat="-23.5499" lon="-46.6300" />
              <way id="100">
                <nd ref="1" />
                <nd ref="2" />
                <nd ref="3" />
              </way>
              <way id="101">
                <nd ref="3" />
                <nd ref="4" />
                <nd ref="1" />
              </way>
              <relation id="500">
                <member type="way" ref="100" role="outer" />
                <member type="way" ref="101" role="outer" />
                <tag k="type" v="multipolygon" />
                <tag k="building" v="apartments" />
                <tag k="building:levels" v="4" />
                <tag k="roof:shape" v="hipped" />
                <tag k="roof:height" v="1.5" />
                <tag k="name" v="Edifício Relação" />
              </relation>
            </osm>
            """;

        var result =
            new MapStudioOsmBuildingImporter()
                .Parse(
                    xml);

        var building =
            Assert.Single(
                result.Buildings);

        Assert.Equal(
            "osm-building-relation-500",
            building.Id);

        Assert.Equal(
            "apartments",
            building.BuildingType);

        Assert.Equal(
            4,
            building.Points.Count);

        Assert.Equal(
            4,
            building.Levels);

        Assert.Equal(
            "hipped",
            building.RoofShape);

        Assert.Equal(
            1.5,
            building.RoofHeightMeters);

        Assert.Equal(
            "Edifício Relação",
            building.Name);

        Assert.Equal(
            0,
            result.IgnoredWayCount);

        Assert.Equal(
            0,
            result.IgnoredRelationCount);

        Assert.Equal(
            0,
            result.MissingNodeReferenceCount);
    }

    [Fact]
    public void ImporterDoesNotFlattenMultipolygonWithInnerRing()
    {
        const string xml =
            """
            <osm version="0.6">
              <node id="1" lat="0" lon="0" />
              <node id="2" lat="0" lon="1" />
              <node id="3" lat="1" lon="1" />
              <node id="4" lat="1" lon="0" />
              <node id="5" lat="0.2" lon="0.2" />
              <node id="6" lat="0.2" lon="0.8" />
              <node id="7" lat="0.8" lon="0.8" />
              <node id="8" lat="0.8" lon="0.2" />
              <way id="10">
                <nd ref="1" />
                <nd ref="2" />
                <nd ref="3" />
                <nd ref="4" />
                <nd ref="1" />
              </way>
              <way id="11">
                <nd ref="5" />
                <nd ref="6" />
                <nd ref="7" />
                <nd ref="8" />
                <nd ref="5" />
              </way>
              <relation id="20">
                <member type="way" ref="10" role="outer" />
                <member type="way" ref="11" role="inner" />
                <tag k="type" v="multipolygon" />
                <tag k="building" v="yes" />
              </relation>
            </osm>
            """;

        var result =
            new MapStudioOsmBuildingImporter()
                .Parse(
                    xml);

        Assert.Empty(
            result.Buildings);

        Assert.Equal(
            1,
            result.IgnoredRelationCount);
    }

}
