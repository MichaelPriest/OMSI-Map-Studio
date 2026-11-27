using MapStudio.Core.Generation.Roads;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioGpsRoadImporterTests
{
    [Fact]
    public void GpxPreservesSeparateSegmentsAndRoutes()
    {
        const string xml = """
            <gpx xmlns="http://www.topografix.com/GPX/1/1">
              <trk><name>Rua A</name><trkseg><trkpt lat="-23.55" lon="-46.63"/><trkpt lat="-23.55" lon="-46.62"/></trkseg>
                <trkseg><trkpt lat="-23.54" lon="-46.60"/><trkpt lat="-23.54" lon="-46.59"/></trkseg></trk>
              <rte><name>Rua B</name><rtept lat="0" lon="0"/><rtept lat="0.1" lon="0.1"/></rte>
            </gpx>
            """;
        var result = new MapStudioGpsRoadImporter().Parse(xml, ".gpx");
        Assert.Equal(3, result.Traces.Count);
        Assert.Equal("Rua A", result.Traces[0].Name);
        Assert.Equal(-46.60, result.Traces[1].Points[0].Longitude, 6);
        Assert.Equal("Rua B", result.Traces[2].Name);
    }

    [Fact]
    public void KmlReadsLinesAndIgnoresPointGeometry()
    {
        const string xml = """
            <kml xmlns="http://www.opengis.net/kml/2.2"><Document><Placemark><name>Via</name>
            <MultiGeometry><Point><coordinates>1,2,0</coordinates></Point>
            <LineString><coordinates>-46.63,-23.55,10 -46.63,-23.55,10 -46.62,-23.54,12</coordinates></LineString>
            </MultiGeometry></Placemark></Document></kml>
            """;
        var trace = Assert.Single(new MapStudioGpsRoadImporter().Parse(xml, ".kml").Traces);
        Assert.Equal(2, trace.Points.Count);
        Assert.Equal("Via", trace.Name);
        Assert.Equal(-23.55, trace.Points[0].Latitude, 6);
    }

    [Fact]
    public void RejectsDtdAndInvalidCoordinates()
    {
        Assert.Throws<System.Xml.XmlException>(() =>
            new MapStudioGpsRoadImporter().Parse("<!DOCTYPE gpx [<!ENTITY x 'unsafe'>]><gpx>&x;</gpx>", ".gpx"));
        Assert.Throws<InvalidDataException>(() =>
            new MapStudioGpsRoadImporter().Parse("<gpx><rte><rtept lat='91' lon='0'/></rte></gpx>", ".gpx"));
    }

    [Fact]
    public void GeoJsonStillWorks()
    {
        var result = new MapStudioGpsRoadImporter().Parse(
            """{"type":"LineString","coordinates":[[0,0],[1,1]]}""", ".geojson");
        Assert.Single(result.Traces);
    }
}
