using MapStudio.Core.Generation.Vegetation;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioOsmVegetationImporterTests
{
    [Fact]
    public void ImporterReadsTreeAndShrubNodesWithMetadata()
    {
        const string xml =
            """
            <osm version="0.6">
              <node id="1" lat="-23.5500" lon="-46.6300">
                <tag k="natural" v="tree" />
                <tag k="species" v="Tipuana tipu" />
                <tag k="genus" v="Tipuana" />
                <tag k="leaf_type" v="broadleaved" />
                <tag k="name" v="Árvore da praça" />
              </node>
              <node id="2" lat="-23.5501" lon="-46.6301">
                <tag k="natural" v="shrub" />
              </node>
            </osm>
            """;

        var result =
            new MapStudioOsmVegetationImporter()
                .Parse(
                    xml);

        Assert.Equal(
            2,
            result.Points.Count);

        var tree =
            Assert.Single(
                result.Points,
                point =>
                    point.Kind ==
                    MapStudioOsmVegetationKind.Tree);

        Assert.Equal(
            "osm-vegetation-1",
            tree.Id);

        Assert.Equal(
            "Tipuana tipu",
            tree.Species);

        Assert.Equal(
            "Tipuana",
            tree.Genus);

        Assert.Equal(
            "broadleaved",
            tree.LeafType);

        Assert.Equal(
            "Árvore da praça",
            tree.Name);

        var shrub =
            Assert.Single(
                result.Points,
                point =>
                    point.Kind ==
                    MapStudioOsmVegetationKind.Shrub);

        Assert.Equal(
            "osm-vegetation-2",
            shrub.Id);

        Assert.Equal(
            0,
            result.IgnoredNodeCount);
    }

    [Fact]
    public void ImporterIgnoresUnrelatedAndInvalidNodes()
    {
        const string xml =
            """
            <osm version="0.6">
              <node id="1" lat="-23.55" lon="-46.63">
                <tag k="amenity" v="bench" />
              </node>
              <node id="2" lat="999" lon="-46.63">
                <tag k="natural" v="tree" />
              </node>
            </osm>
            """;

        var result =
            new MapStudioOsmVegetationImporter()
                .Parse(
                    xml);

        Assert.Empty(
            result.Points);

        Assert.Equal(
            2,
            result.IgnoredNodeCount);
    }

    [Fact]
    public void ImporterRejectsNonOsmRoot()
    {
        Assert.Throws<
            InvalidDataException>(
                () =>
                    new MapStudioOsmVegetationImporter()
                        .Parse(
                            "<root />"));
    }
}
