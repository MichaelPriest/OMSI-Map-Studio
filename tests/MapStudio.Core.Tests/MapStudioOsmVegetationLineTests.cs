using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Vegetation;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioOsmVegetationLineTests
{
    [Fact]
    public void ImporterReadsHedgeAndTreeRowWays()
    {
        const string xml =
            """
            <osm version="0.6">
              <node id="1" lat="-23.5500" lon="-46.6300" />
              <node id="2" lat="-23.5500" lon="-46.6299" />
              <node id="3" lat="-23.5499" lon="-46.6299" />

              <way id="10">
                <nd ref="1" />
                <nd ref="2" />
                <tag k="barrier" v="hedge" />
                <tag k="species" v="Ligustrum" />
              </way>

              <way id="20">
                <nd ref="2" />
                <nd ref="3" />
                <tag k="natural" v="tree_row" />
                <tag k="genus" v="Tipuana" />
                <tag k="name" v="Alameda" />
              </way>
            </osm>
            """;

        var result =
            new MapStudioOsmVegetationLineImporter()
                .Parse(
                    xml);

        Assert.Equal(
            2,
            result.Lines.Count);

        var hedge =
            Assert.Single(
                result.Lines,
                line =>
                    line.Kind ==
                    MapStudioOsmVegetationLineKind
                        .Hedge);

        Assert.Equal(
            "osm-vegetation-line-10",
            hedge.Id);

        Assert.Equal(
            "Ligustrum",
            hedge.Species);

        var treeRow =
            Assert.Single(
                result.Lines,
                line =>
                    line.Kind ==
                    MapStudioOsmVegetationLineKind
                        .TreeRow);

        Assert.Equal(
            "Tipuana",
            treeRow.Genus);

        Assert.Equal(
            "Alameda",
            treeRow.Name);

        Assert.Equal(
            0,
            result.MissingNodeReferenceCount);
    }

    [Fact]
    public void ImporterRejectsWayWithMissingNodeReference()
    {
        const string xml =
            """
            <osm version="0.6">
              <node id="1" lat="-23.5500" lon="-46.6300" />
              <way id="10">
                <nd ref="1" />
                <nd ref="999" />
                <tag k="barrier" v="hedge" />
              </way>
            </osm>
            """;

        var result =
            new MapStudioOsmVegetationLineImporter()
                .Parse(
                    xml);

        Assert.Empty(
            result.Lines);

        Assert.Equal(
            1,
            result.IgnoredWayCount);

        Assert.Equal(
            1,
            result.MissingNodeReferenceCount);
    }

    [Fact]
    public void SamplerCreatesEvenlySpacedTreeRowPoints()
    {
        var anchor =
            new MapStudioGeographicAnchor(
                -23.5500,
                -46.6300,
                100,
                200);

        var line =
            new MapStudioGeoVegetationLine(
                "row",
                [
                    new(
                        -23.5500,
                        -46.6300),
                    new(
                        -23.5500,
                        -46.6299)
                ],
                MapStudioOsmVegetationLineKind
                    .TreeRow,
                "Tipuana tipu",
                "Tipuana",
                "broadleaved",
                "Alameda");

        var points =
            new MapStudioVegetationLineSampler()
                .ProjectAndSample(
                    [line],
                    anchor,
                    spacingMeters:
                        4.0);

        Assert.Equal(
            4,
            points.Count);

        Assert.All(
            points,
            point =>
                Assert.Equal(
                    MapStudioOsmVegetationKind
                        .Tree,
                    point.Kind));

        Assert.Equal(
            anchor.WorldX,
            points[0].Position.X,
            6);

        Assert.InRange(
            points[0]
                .Position
                .DistanceTo(
                    points[1]
                        .Position),
            3.99,
            4.01);

        Assert.Equal(
            "Tipuana tipu",
            points[0].Species);

        Assert.Equal(
            "row-sample-0",
            points[0].Id);
    }

    [Fact]
    public void SamplerHonorsMaximumPointLimit()
    {
        var anchor =
            new MapStudioGeographicAnchor(
                0,
                0,
                0,
                0);

        var line =
            new MapStudioGeoVegetationLine(
                "hedge",
                [
                    new(
                        0,
                        0),
                    new(
                        0,
                        0.01)
                ],
                MapStudioOsmVegetationLineKind
                    .Hedge,
                null,
                null,
                null,
                null);

        var points =
            new MapStudioVegetationLineSampler()
                .ProjectAndSample(
                    [line],
                    anchor,
                    spacingMeters:
                        1.0,
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
