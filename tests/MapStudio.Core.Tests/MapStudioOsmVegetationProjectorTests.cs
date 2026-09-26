using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Vegetation;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioOsmVegetationProjectorTests
{
    [Fact]
    public void ProjectorPreservesMetadataAndUsesMapAnchor()
    {
        var points =
            new[]
            {
                new MapStudioGeoVegetationPoint(
                    "tree-1",
                    -23.5500,
                    -46.6290,
                    MapStudioOsmVegetationKind.Tree,
                    "Tipuana tipu",
                    "Tipuana",
                    "broadleaved",
                    "Árvore teste")
            };

        var anchor =
            new MapStudioGeographicAnchor(
                -23.5500,
                -46.6300,
                1500,
                900);

        var projected =
            Assert.Single(
                new MapStudioOsmVegetationProjector()
                    .Project(
                        points,
                        anchor));

        Assert.True(
            projected.Position.X >
            anchor.WorldX);

        Assert.Equal(
            anchor.WorldZ,
            projected.Position.Z,
            6);

        Assert.Equal(
            "tree-1",
            projected.Id);

        Assert.Equal(
            MapStudioOsmVegetationKind.Tree,
            projected.Kind);

        Assert.Equal(
            "Tipuana tipu",
            projected.Species);

        Assert.Equal(
            "Tipuana",
            projected.Genus);

        Assert.Equal(
            "broadleaved",
            projected.LeafType);

        Assert.Equal(
            "Árvore teste",
            projected.Name);
    }

    [Fact]
    public void ProjectorKeepsNorthSouthOrientationUsedByMapStudio()
    {
        var points =
            new[]
            {
                new MapStudioGeoVegetationPoint(
                    "tree-1",
                    -23.5490,
                    -46.6300,
                    MapStudioOsmVegetationKind.Tree,
                    null,
                    null,
                    null,
                    null)
            };

        var anchor =
            new MapStudioGeographicAnchor(
                -23.5500,
                -46.6300,
                0,
                0);

        var projected =
            Assert.Single(
                new MapStudioOsmVegetationProjector()
                    .Project(
                        points,
                        anchor));

        Assert.True(
            projected.Position.Z <
            anchor.WorldZ);
    }
}
