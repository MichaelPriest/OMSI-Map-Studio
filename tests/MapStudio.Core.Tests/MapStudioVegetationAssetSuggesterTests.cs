using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Vegetation;
using MapStudio.Core.Omsi.Indexing;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioVegetationAssetSuggesterTests
{
    [Fact]
    public void SuggestPrefersSpeciesMatchForTrees()
    {
        var assets =
            new[]
            {
                new OmsiAssetIndexEntry(
                    @"Sceneryobjects\Vegetation\generic_tree.sco",
                    OmsiAssetKind.SceneryObject,
                    1,
                    1),
                new OmsiAssetIndexEntry(
                    @"Sceneryobjects\Vegetation\Tipuana\tipuana_tipu_tree.sco",
                    OmsiAssetKind.SceneryObject,
                    1,
                    1)
            };

        var points =
            new[]
            {
                new MapStudioProjectedVegetationPoint(
                    "tree-1",
                    new MapStudioRoadPoint(
                        0,
                        0),
                    MapStudioOsmVegetationKind.Tree,
                    "Tipuana tipu",
                    "Tipuana",
                    "broadleaved",
                    null)
            };

        var result =
            new MapStudioVegetationAssetSuggester()
                .Suggest(
                    assets,
                    points,
                    MapStudioOsmVegetationKind.Tree);

        Assert.NotNull(
            result);

        Assert.Equal(
            @"Sceneryobjects\Vegetation\Tipuana\tipuana_tipu_tree.sco",
            result.RelativePath);
    }

    [Fact]
    public void SuggestUsesGenericKindWhenMetadataDoesNotMatch()
    {
        var assets =
            new[]
            {
                new OmsiAssetIndexEntry(
                    @"Sceneryobjects\Vegetation\tree_generic.sco",
                    OmsiAssetKind.SceneryObject,
                    1,
                    1),
                new OmsiAssetIndexEntry(
                    @"Sceneryobjects\Vegetation\hedge_bush.sco",
                    OmsiAssetKind.SceneryObject,
                    1,
                    1)
            };

        var points =
            new[]
            {
                new MapStudioProjectedVegetationPoint(
                    "shrub-1",
                    new MapStudioRoadPoint(
                        0,
                        0),
                    MapStudioOsmVegetationKind.Shrub,
                    null,
                    null,
                    null,
                    null)
            };

        var result =
            new MapStudioVegetationAssetSuggester()
                .Suggest(
                    assets,
                    points,
                    MapStudioOsmVegetationKind.Shrub);

        Assert.NotNull(
            result);

        Assert.Equal(
            @"Sceneryobjects\Vegetation\hedge_bush.sco",
            result.RelativePath);
    }

    [Fact]
    public void SuggestReturnsNullWithoutVegetationAssets()
    {
        var assets =
            new[]
            {
                new OmsiAssetIndexEntry(
                    @"Sceneryobjects\Buildings\house.sco",
                    OmsiAssetKind.SceneryObject,
                    1,
                    1)
            };

        var result =
            new MapStudioVegetationAssetSuggester()
                .Suggest(
                    assets,
                    Array.Empty<MapStudioProjectedVegetationPoint>(),
                    MapStudioOsmVegetationKind.Tree);

        Assert.Null(
            result);
    }
}
