using MapStudio.Core.Omsi.Indexing;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioOriginalOmsiAssetCatalogTests
{
    [Theory]
    [InlineData(
        @"Splines\Marcel\str_2spur_11m_SeeburgerStr1.sli",
        OmsiAssetKind.Spline,
        MapStudioOriginalOmsiAssetRole.RoadSpline)]
    [InlineData(
        @"Splines\Marcel\Hstr_6spur_Ruhlebener1.sli",
        OmsiAssetKind.Spline,
        MapStudioOriginalOmsiAssetRole.RoadSpline)]
    [InlineData(
        @"Splines\Ruede\sdwk_1.5m_DDR_Spandauer_Str.sli",
        OmsiAssetKind.Spline,
        MapStudioOriginalOmsiAssetRole.SidewalkSpline)]
    [InlineData(
        @"Sceneryobjects\Kreuz_MC\Einm_Altonaer.sco",
        OmsiAssetKind.SceneryObject,
        MapStudioOriginalOmsiAssetRole.JunctionObject)]
    [InlineData(
        @"Sceneryobjects\Kreuz_MC\Zebra_falks.sco",
        OmsiAssetKind.SceneryObject,
        MapStudioOriginalOmsiAssetRole.CrosswalkObject)]
    [InlineData(
        @"Sceneryobjects\Verkehrszeichen_MC\Ampel_Kfz_1.sco",
        OmsiAssetKind.SceneryObject,
        MapStudioOriginalOmsiAssetRole.TrafficSignalObject)]
    [InlineData(
        @"Sceneryobjects\Verkehrszeichen_MC\VZ_vb_tempo30_m.sco",
        OmsiAssetKind.SceneryObject,
        MapStudioOriginalOmsiAssetRole.TrafficSignObject)]
    [InlineData(
        @"Sceneryobjects\Streetobjects_RUE\Busstop_Pole_L.sco",
        OmsiAssetKind.SceneryObject,
        MapStudioOriginalOmsiAssetRole.BusStopObject)]
    [InlineData(
        @"Sceneryobjects\Ruede\hst_70er_wartehaus.sco",
        OmsiAssetKind.SceneryObject,
        MapStudioOriginalOmsiAssetRole.BusShelterObject)]
    [InlineData(
        @"Sceneryobjects\Streetobjects_MC\strlt_gwg_70s_1.sco",
        OmsiAssetKind.SceneryObject,
        MapStudioOriginalOmsiAssetRole.StreetLightObject)]
    [InlineData(
        @"Sceneryobjects\Streetobjects_MC\muelleinmer_alt_orange.sco",
        OmsiAssetKind.SceneryObject,
        MapStudioOriginalOmsiAssetRole.WasteBasketObject)]
    [InlineData(
        @"Sceneryobjects\Trees_MC\tree_avenue_01.sco",
        OmsiAssetKind.SceneryObject,
        MapStudioOriginalOmsiAssetRole.VegetationObject)]
    [InlineData(
        @"Sceneryobjects\Buildings_MC\bw_50s_01.sco",
        OmsiAssetKind.SceneryObject,
        MapStudioOriginalOmsiAssetRole.BuildingObject)]
    public void ClassifiesKnownOriginalOmsiFamilies(
        string path,
        OmsiAssetKind kind,
        MapStudioOriginalOmsiAssetRole expected)
    {
        Assert.Equal(
            expected,
            MapStudioOriginalOmsiAssetCatalog
                .Classify(
                    path,
                    kind));
    }

    [Fact]
    public void DoesNotTreatAddonRoadAsOriginalOmsiRoad()
    {
        var entry =
            new OmsiAssetIndexEntry(
                @"Splines\AddonPack\str_2spur_11m.sli",
                OmsiAssetKind.Spline,
                1,
                1);

        Assert.False(
            MapStudioOriginalOmsiAssetCatalog
                .IsOriginalRoadSpline(
                    entry));
    }
}
