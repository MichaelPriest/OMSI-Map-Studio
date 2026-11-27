using MapStudio.Core.Generation.Roads;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioGeoJsonRoadImporterTests
{
    [Fact]
    public void ImporterReadsLineStringAndRoadProperties()
    {
        const string json =
            """
            {
              "type": "FeatureCollection",
              "features": [
                {
                  "type": "Feature",
                  "id": "main-road",
                  "properties": {
                    "highway": "primary",
                    "lanes": "4",
                    "oneway": "no",
                    "width": 14.5,
                    "name": "Avenida Teste"
                  },
                  "geometry": {
                    "type": "LineString",
                    "coordinates": [
                      [-46.63, -23.55],
                      [-46.629, -23.55],
                      [-46.628, -23.549]
                    ]
                  }
                }
              ]
            }
            """;

        var result =
            new MapStudioGeoJsonRoadImporter()
                .Parse(
                    json);

        var road =
            Assert.Single(
                result.Traces);

        Assert.Equal(
            "main-road",
            road.Id);

        Assert.Equal(
            "primary",
            road.Highway);

        Assert.Equal(
            4,
            road.LaneCount);

        Assert.False(
            road.OneWay);

        Assert.Equal(
            14.5,
            road.WidthMeters);

        Assert.Equal(
            "Avenida Teste",
            road.Name);

        Assert.Equal(
            3,
            road.Points.Count);

        Assert.Equal(
            -23.55,
            road.Points[0]
                .Latitude,
            6);

        Assert.Equal(
            -46.63,
            road.Points[0]
                .Longitude,
            6);
    }

    [Fact]
    public void ImporterSplitsMultiLineStringIntoIndependentTraces()
    {
        const string json =
            """
            {
              "type": "Feature",
              "properties": {
                "highway": "residential"
              },
              "geometry": {
                "type": "MultiLineString",
                "coordinates": [
                  [[-46.63,-23.55],[-46.629,-23.55]],
                  [[-46.629,-23.55],[-46.628,-23.55]]
                ]
              }
            }
            """;

        var result =
            new MapStudioGeoJsonRoadImporter()
                .Parse(
                    json);

        Assert.Equal(
            2,
            result.Traces.Count);

        Assert.All(
            result.Traces,
            road =>
                Assert.Equal(
                    "residential",
                    road.Highway));
    }

    [Fact]
    public void ImporterIgnoresUnsupportedFeatures()
    {
        const string json =
            """
            {
              "type": "FeatureCollection",
              "features": [
                {
                  "type": "Feature",
                  "properties": {},
                  "geometry": {
                    "type": "Point",
                    "coordinates": [-46.63,-23.55]
                  }
                },
                {
                  "type": "Feature",
                  "properties": {},
                  "geometry": {
                    "type": "LineString",
                    "coordinates": [
                      [-46.63,-23.55],
                      [-46.629,-23.55]
                    ]
                  }
                }
              ]
            }
            """;

        var result =
            new MapStudioGeoJsonRoadImporter()
                .Parse(
                    json);

        Assert.Single(
            result.Traces);

        Assert.Equal(
            1,
            result.IgnoredFeatureCount);
    }
}
