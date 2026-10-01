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
