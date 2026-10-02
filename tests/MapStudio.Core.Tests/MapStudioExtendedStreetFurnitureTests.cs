using MapStudio.Core.Generation.Scene;
using MapStudio.Core.Omsi.Indexing;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioExtendedStreetFurnitureTests
{
    [Fact]
    public void ImporterRecognizesExtendedFurnitureKinds()
    {
        const string xml =
            """
            <osm version="0.6">
              <node id="1" lat="-23.55" lon="-46.63"><tag k="amenity" v="bench"/></node>
              <node id="2" lat="-23.55" lon="-46.6299"><tag k="amenity" v="waste_basket"/></node>
              <node id="3" lat="-23.55" lon="-46.6298"><tag k="barrier" v="bollard"/></node>
              <node id="4" lat="-23.55" lon="-46.6297"><tag k="emergency" v="fire_hydrant"/></node>
              <node id="5" lat="-23.55" lon="-46.6296"><tag k="highway" v="traffic_signals"/></node>
              <node id="6" lat="-23.55" lon="-46.6295"><tag k="highway" v="crossing"/></node>
            </osm>
            """;

        var result =
            new MapStudioOsmStreetFurnitureImporter()
                .Parse(xml);

        Assert.Equal(6, result.Points.Count);
        Assert.Contains(result.Points, p => p.Kind == MapStudioOsmStreetFurnitureKind.Bench);
        Assert.Contains(result.Points, p => p.Kind == MapStudioOsmStreetFurnitureKind.WasteBasket);
        Assert.Contains(result.Points, p => p.Kind == MapStudioOsmStreetFurnitureKind.Bollard);
        Assert.Contains(result.Points, p => p.Kind == MapStudioOsmStreetFurnitureKind.FireHydrant);
        Assert.Contains(result.Points, p => p.Kind == MapStudioOsmStreetFurnitureKind.TrafficSignal);
        Assert.Contains(result.Points, p => p.Kind == MapStudioOsmStreetFurnitureKind.Crosswalk);
    }

    [Theory]
    [InlineData(MapStudioOsmStreetFurnitureKind.Bench, @"Sceneryobjects\StreetFurniture\wood_bench.sco")]
    [InlineData(MapStudioOsmStreetFurnitureKind.WasteBasket, @"Sceneryobjects\StreetFurniture\trash_bin.sco")]
    [InlineData(MapStudioOsmStreetFurnitureKind.Bollard, @"Sceneryobjects\StreetFurniture\bollard_black.sco")]
    [InlineData(MapStudioOsmStreetFurnitureKind.FireHydrant, @"Sceneryobjects\Utilities\fire_hydrant.sco")]
    [InlineData(MapStudioOsmStreetFurnitureKind.TrafficSignal, @"Sceneryobjects\Traffic\Ampel_3fach.sco")]
    [InlineData(MapStudioOsmStreetFurnitureKind.Crosswalk, @"Sceneryobjects\Markings\Zebrastreifen.sco")]
    public void SuggesterSelectsExtendedFurnitureAssets(
        MapStudioOsmStreetFurnitureKind kind,
        string expected)
    {
        var assets =
            new[]
            {
                Asset(@"Sceneryobjects\Buildings\house.sco"),
                Asset(@"Sceneryobjects\StreetFurniture\wood_bench.sco"),
                Asset(@"Sceneryobjects\StreetFurniture\trash_bin.sco"),
                Asset(@"Sceneryobjects\StreetFurniture\bollard_black.sco"),
                Asset(@"Sceneryobjects\Utilities\fire_hydrant.sco"),
                Asset(@"Sceneryobjects\Traffic\Ampel_3fach.sco"),
                Asset(@"Sceneryobjects\Markings\Zebrastreifen.sco")
            };

        var selected =
            new MapStudioStreetFurnitureAssetSuggester()
                .Suggest(
                    assets,
                    new MapStudioGeoStreetFurniturePoint(
                        "feature",
                        -23.55,
                        -46.63,
                        kind,
                        "Mapped",
                        "City",
                        "01",
                        null));

        Assert.NotNull(selected);
        Assert.Equal(expected, selected.RelativePath);
    }

    [Theory]
    [InlineData(
        MapStudioOsmStreetFurnitureKind.StreetLight,
        @"Sceneryobjects\Streetobjects_MC\strlt_gwg_70s_1.sco")]
    [InlineData(
        MapStudioOsmStreetFurnitureKind.TrafficSign,
        @"Sceneryobjects\Verkehrszeichen_MC\VZ_vb_tempo30_m.sco")]
    [InlineData(
        MapStudioOsmStreetFurnitureKind.BusShelter,
        @"Sceneryobjects\Streetobjects_RUE\Wartehaus_DDR.sco")]
    [InlineData(
        MapStudioOsmStreetFurnitureKind.WasteBasket,
        @"Sceneryobjects\Streetobjects_MC\muelleinmer_alt_orange.sco")]
    [InlineData(
        MapStudioOsmStreetFurnitureKind.Bollard,
        @"Sceneryobjects\Streetobjects_MC\bollard_conc_1_norm.sco")]
    [InlineData(
        MapStudioOsmStreetFurnitureKind.TrafficSignal,
        @"Sceneryobjects\Verkehrszeichen_MC\Ampel_Kfz_1.sco")]
    [InlineData(
        MapStudioOsmStreetFurnitureKind.Crosswalk,
        @"Sceneryobjects\Kreuz_MC\Zebra_falks.sco")]
    public void SuggesterRecognizesOriginalOmsiFurnitureFamilies(
        MapStudioOsmStreetFurnitureKind kind,
        string expected)
    {
        var assets =
            new[]
            {
                Asset(@"Sceneryobjects\StreetFurniture\traffic_light_mod.sco"),
                Asset(@"Sceneryobjects\StreetFurniture\generic_sign.sco"),
                Asset(@"Sceneryobjects\StreetFurniture\street_light_mod.sco"),
                Asset(@"Sceneryobjects\Transit\busstop_shelter_mod.sco"),
                Asset(@"Sceneryobjects\StreetFurniture\trash_bin_mod.sco"),
                Asset(@"Sceneryobjects\StreetFurniture\bollard_mod.sco"),
                Asset(@"Sceneryobjects\Markings\crosswalk_mod.sco"),
                Asset(@"Sceneryobjects\Streetobjects_MC\strlt_gwg_70s_1.sco"),
                Asset(@"Sceneryobjects\Verkehrszeichen_MC\VZ_vb_tempo30_m.sco"),
                Asset(@"Sceneryobjects\Streetobjects_RUE\Wartehaus_DDR.sco"),
                Asset(@"Sceneryobjects\Streetobjects_MC\muelleinmer_alt_orange.sco"),
                Asset(@"Sceneryobjects\Streetobjects_MC\bollard_conc_1_norm.sco"),
                Asset(@"Sceneryobjects\Verkehrszeichen_MC\Ampel_Kfz_1.sco"),
                Asset(@"Sceneryobjects\Kreuz_MC\Zebra_falks.sco")
            };

        var selected =
            new MapStudioStreetFurnitureAssetSuggester()
                .Suggest(
                    assets,
                    new MapStudioGeoStreetFurniturePoint(
                        "original-omsi",
                        -23.55,
                        -46.63,
                        kind,
                        "Mapped",
                        "City",
                        null,
                        kind ==
                            MapStudioOsmStreetFurnitureKind.BusShelter
                            ? "yes"
                            : null));

        Assert.NotNull(
            selected);

        Assert.Equal(
            expected,
            selected.RelativePath);
    }

    [Fact]
    public void SuggesterMatchesExplicitPrioritySignsAndRejectsUnrelatedStockSign()
    {
        var assets =
            new[]
            {
                Asset(
                    @"Sceneryobjects\Verkehrszeichen_MC\VZ_205.sco"),
                Asset(
                    @"Sceneryobjects\Verkehrszeichen_MC\VZ_206.sco"),
                Asset(
                    @"Sceneryobjects\Verkehrszeichen_MC\VZ_274_30.sco")
            };

        OmsiAssetIndexEntry? Select(
            string reference) =>
            new MapStudioStreetFurnitureAssetSuggester()
                .Suggest(
                    assets,
                    new MapStudioGeoStreetFurniturePoint(
                        "sign-" +
                            reference,
                        -23.55,
                        -46.63,
                        MapStudioOsmStreetFurnitureKind
                            .TrafficSign,
                        null,
                        null,
                        reference,
                        null,
                        90));

        Assert.Equal(
            @"Sceneryobjects\Verkehrszeichen_MC\VZ_206.sco",
            Select(
                "stop")
                ?.RelativePath);

        Assert.Equal(
            @"Sceneryobjects\Verkehrszeichen_MC\VZ_205.sco",
            Select(
                "give_way")
                ?.RelativePath);

        Assert.Equal(
            @"Sceneryobjects\Verkehrszeichen_MC\VZ_206.sco",
            Select(
                "DE:206")
                ?.RelativePath);

        Assert.Equal(
            @"Sceneryobjects\Verkehrszeichen_MC\VZ_274_30.sco",
            Select(
                "DE:274-30")
                ?.RelativePath);

        Assert.Null(
            Select(
                "BR:R-1"));
    }

    [Fact]
    public void AdapterMapsExtendedFurnitureToDedicatedSceneKinds()
    {
        var points =
            new[]
            {
                Point("bench", MapStudioOsmStreetFurnitureKind.Bench),
                Point("bin", MapStudioOsmStreetFurnitureKind.WasteBasket),
                Point("bollard", MapStudioOsmStreetFurnitureKind.Bollard),
                Point("hydrant", MapStudioOsmStreetFurnitureKind.FireHydrant),
                Point("signal", MapStudioOsmStreetFurnitureKind.TrafficSignal),
                Point("crosswalk", MapStudioOsmStreetFurnitureKind.Crosswalk)
            };

        var items =
            new MapStudioOsmStreetFurnitureReconstructionAdapter()
                .BuildCandidates(points);

        Assert.Contains(items, x => x.Kind == MapStudioSceneFeatureKind.Bench);
        Assert.Contains(items, x => x.Kind == MapStudioSceneFeatureKind.WasteBasket);
        Assert.Contains(items, x => x.Kind == MapStudioSceneFeatureKind.Bollard);
        Assert.Contains(items, x => x.Kind == MapStudioSceneFeatureKind.FireHydrant);
        Assert.Contains(items, x => x.Kind == MapStudioSceneFeatureKind.TrafficSignal);
        Assert.Contains(items, x => x.Kind == MapStudioSceneFeatureKind.Crosswalk);
    }

    private static OmsiAssetIndexEntry Asset(string path) =>
        new(path, OmsiAssetKind.SceneryObject, 1, 1);

    private static MapStudioGeoStreetFurniturePoint Point(
        string id,
        MapStudioOsmStreetFurnitureKind kind) =>
        new(id, -23.55, -46.63, kind, "Mapped", "City", "01", null);
}
