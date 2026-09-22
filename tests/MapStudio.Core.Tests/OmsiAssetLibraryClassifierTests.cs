using MapStudio.Core.Omsi.Indexing;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiAssetLibraryClassifierTests
{
    [Theory]
    [InlineData(
        @"Sceneryobjects\City\crossing_4way.sco",
        OmsiAssetLibraryGroup.Junctions)]
    [InlineData(
        @"Sceneryobjects\Nature\Tree_Maple.sco",
        OmsiAssetLibraryGroup.Vegetation)]
    [InlineData(
        @"Sceneryobjects\Street\Lamp_Post.sco",
        OmsiAssetLibraryGroup.StreetFurniture)]
    [InlineData(
        @"Sceneryobjects\MapStudio_Props\Starter_BusStop\starter_busstop.sco",
        OmsiAssetLibraryGroup.Transit)]
    [InlineData(
        @"Sceneryobjects\MapStudio_Props\Starter_UtilityBox\starter_utilitybox.sco",
        OmsiAssetLibraryGroup.Utilities)]
    [InlineData(
        @"Sceneryobjects\MapStudio_Traffic\Starter_TrafficLight\starter_trafficlight.sco",
        OmsiAssetLibraryGroup.StreetFurniture)]
    public void ClassifiesSceneryObjects(
        string path,
        OmsiAssetLibraryGroup expected)
    {
        var entry =
            new OmsiAssetIndexEntry(
                path,
                OmsiAssetKind.SceneryObject,
                1,
                1);

        Assert.Equal(
            expected,
            OmsiAssetLibraryClassifier
                .Classify(entry));
    }

    [Theory]
    [InlineData(
        @"Splines\Roads\city_street.sli",
        OmsiAssetLibraryGroup.Roads)]
    [InlineData(
        @"Splines\Rail\tram_track.sli",
        OmsiAssetLibraryGroup.Rail)]
    [InlineData(
        @"Splines\Paths\sidewalk.sli",
        OmsiAssetLibraryGroup.Paths)]
    public void ClassifiesSplines(
        string path,
        OmsiAssetLibraryGroup expected)
    {
        var entry =
            new OmsiAssetIndexEntry(
                path,
                OmsiAssetKind.Spline,
                1,
                1);

        Assert.Equal(
            expected,
            OmsiAssetLibraryClassifier
                .Classify(entry));
    }

    [Theory]
    [InlineData(
        @"Splines\Roads\hauptstrasse.sli",
        "rua")]
    [InlineData(
        @"Sceneryobjects\Trees\Baum_01.sco",
        "árvore")]
    [InlineData(
        @"Sceneryobjects\Busstop\Haltestelle.sco",
        "ponto")]
    public void SmartSearchUsesReactSynonyms(
        string text,
        string query)
    {
        Assert.True(
            OmsiAssetLibraryClassifier
                .MatchesSmartSearch(
                    text,
                    query));
    }
    [Theory]
    [InlineData(
        @"Sceneryobjects\Houses\residential_house.sco",
        "Residencial")]
    [InlineData(
        @"Sceneryobjects\Nature\grass_patch.sco",
        "Grama")]
    [InlineData(
        @"Sceneryobjects\Street\traffic_light.sco",
        "Sinalização")]
    [InlineData(
        @"Sceneryobjects\MapStudio_Traffic\Starter_TrafficLight\starter_trafficlight.sco",
        "Sinalização")]
    [InlineData(
        @"Sceneryobjects\MapStudio_Props\Starter_UtilityBox\starter_utilitybox.sco",
        "Infraestrutura")]
    [InlineData(
        @"Splines\Roads\avenue_4lane.sli",
        "Avenidas")]
    [InlineData(
        @"Splines\Rail\tram_track.sli",
        "Bonde / tram")]
    public void SubcategoriesMatchReactLibrary(
        string path,
        string expected)
    {
        var kind =
            Path.GetExtension(path)
                .Equals(
                    ".sli",
                    StringComparison.OrdinalIgnoreCase)
                ? OmsiAssetKind.Spline
                : OmsiAssetKind.SceneryObject;

        var entry =
            new OmsiAssetIndexEntry(
                path,
                kind,
                1,
                1);

        Assert.Equal(
            expected,
            OmsiAssetLibraryClassifier
                .GetSubcategory(
                    entry));
    }

}
