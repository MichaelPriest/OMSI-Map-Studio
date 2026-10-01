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
        @"Sceneryobjects\Transporte\ponto_parada_centro.sco",
        OmsiAssetLibraryGroup.Transit)]
    [InlineData(
        @"Sceneryobjects\Transporte\garagem_leste.sco",
        OmsiAssetLibraryGroup.Transit)]
    [InlineData(
        @"Sceneryobjects\MapStudio_Props\Starter_UtilityBox\starter_utilitybox.sco",
        OmsiAssetLibraryGroup.Utilities)]
    [InlineData(
        @"Sceneryobjects\Infraestrutura\caixa_energia_01.sco",
        OmsiAssetLibraryGroup.Utilities)]
    [InlineData(
        @"Sceneryobjects\Saneamento\rede_esgoto_01.sco",
        OmsiAssetLibraryGroup.Utilities)]
    [InlineData(
        @"Sceneryobjects\MapStudio_Traffic\Starter_TrafficLight\starter_trafficlight.sco",
        OmsiAssetLibraryGroup.StreetFurniture)]
    [InlineData(
        @"Sceneryobjects\Kreuz_MC\Einm_Altonaer.sco",
        OmsiAssetLibraryGroup.Junctions)]
    [InlineData(
        @"Sceneryobjects\Kreuz_MC\Zebra_falks.sco",
        OmsiAssetLibraryGroup.StreetFurniture)]
    [InlineData(
        @"Sceneryobjects\Verkehrszeichen_MC\VZ_vb_tempo30_m.sco",
        OmsiAssetLibraryGroup.StreetFurniture)]
    [InlineData(
        @"Sceneryobjects\Ruede\hst_70er_wartehaus.sco",
        OmsiAssetLibraryGroup.Transit)]
    [InlineData(
        @"Sceneryobjects\Ruede\peitschenleuchte01.sco",
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
        @"Splines\Marcel\str_2spur_8m.sli",
        OmsiAssetLibraryGroup.Roads)]
    [InlineData(
        @"Splines\Marcel\Hstr_6spur_Ruhlebener1.sli",
        OmsiAssetLibraryGroup.Roads)]
    [InlineData(
        @"Splines\Ruede\sdwk_1.5m_DDR_Spandauer_Str.sli",
        OmsiAssetLibraryGroup.Paths)]
    [InlineData(
        @"Splines\Paths\sidewalk.sli",
        OmsiAssetLibraryGroup.Paths)]
    [InlineData(
        @"Splines\Tuneis\tunel_urbano.sli",
        OmsiAssetLibraryGroup.Bridges)]
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
        @"Sceneryobjects\Transporte\abrigo_parada_centro.sco",
        "Pontos / abrigos")]
    [InlineData(
        @"Sceneryobjects\Infraestrutura\agua_esgoto.sco",
        "Água / saneamento")]
    [InlineData(
        @"Splines\Tuneis\tunel_urbano.sli",
        "Túneis")]
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
