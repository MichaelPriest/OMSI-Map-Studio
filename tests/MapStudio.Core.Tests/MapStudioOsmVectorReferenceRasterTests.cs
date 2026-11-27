using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Scene;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioOsmVectorReferenceRasterTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LakeRelationJoinsReversedWaysAndPreservesIsland(bool missingIslandNode)
    {
        var xml = """
            <osm>
              <node id="1" lat="0.00036" lon="-0.00036"/><node id="2" lat="0.00036" lon="0.00036"/>
              <node id="3" lat="-0.00036" lon="0.00036"/><node id="4" lat="-0.00036" lon="-0.00036"/>
              <node id="5" lat="0.00009" lon="-0.00009"/><node id="6" lat="0.00009" lon="0.00009"/>
              <node id="7" lat="-0.00009" lon="0.00009"/><node id="8" lat="-0.00009" lon="-0.00009"/>
              <way id="10"><nd ref="1"/><nd ref="2"/><nd ref="3"/></way>
              <way id="11"><nd ref="1"/><nd ref="4"/><nd ref="3"/></way>
              <way id="12"><nd ref="5"/><nd ref="6"/><nd ref="7"/><nd ref="8"/><nd ref="5"/></way>
              <relation id="20"><tag k="natural" v="water"/><tag k="type" v="multipolygon"/>
                <member type="way" role="inner" ref="12"/><member type="way" role="outer" ref="11"/>
                <member type="way" role="outer" ref="10"/></relation>
            </osm>
            """;
        if (missingIslandNode) xml = xml.Replace("nd ref=\"8\"", "nd ref=\"999\"");
        var rgb = Render(xml);
        Assert.Equal(missingIslandNode ? Ground : Water, Pixel(rgb, 20, 50));
        Assert.Equal(Ground, Pixel(rgb, 50, 50));
        Assert.Equal(Ground, Pixel(rgb, 5, 5));
    }

    [Fact]
    public void UsesMeasuredRiverWidthAndKeepsRoadAboveWater()
    {
        const string xml = """
            <osm>
              <node id="1" lat="0" lon="-0.0005"/><node id="2" lat="0" lon="0.0005"/>
              <node id="3" lat="-0.0005" lon="0"/><node id="4" lat="0.0005" lon="0"/>
              <node id="5" lat="0.0002" lon="-0.0005"/><node id="6" lat="0.0002" lon="0.0005"/>
              <way id="10"><nd ref="1"/><nd ref="2"/><tag k="waterway" v="river"/><tag k="width" v="12 m"/></way>
              <way id="11"><nd ref="3"/><nd ref="4"/><tag k="highway" v="residential"/><tag k="bridge" v="yes"/></way>
              <way id="12"><nd ref="5"/><nd ref="6"/><tag k="waterway" v="stream"/><tag k="tunnel" v="culvert"/></way>
            </osm>
            """;
        var rgb = Render(xml);
        Assert.Equal(Water, Pixel(rgb, 20, 55));
        Assert.Equal(Ground, Pixel(rgb, 20, 56));
        Assert.Equal(Ground, Pixel(rgb, 20, 28));
        Assert.Equal(new byte[] { 255, 255, 255 }, Pixel(rgb, 50, 50));
    }

    private static readonly byte[] Ground = [235, 233, 224];
    private static readonly byte[] Water = [150, 199, 219];
    private static byte[] Render(string xml) => MapStudioOsmVectorReferenceRaster.RenderRgb(
        xml, new MapStudioGeographicAnchor(0, 0, 50, 50), 0, 0, 100, 100, 1);
    private static byte[] Pixel(byte[] rgb, int x, int y) => rgb.AsSpan((y * 100 + x) * 3, 3).ToArray();

    [Fact]
    public void DrawsRoadAtGeoreferencedPositionAndEncodesPng()
    {
        const string xml = """
            <osm version="0.6">
              <node id="1" lat="0" lon="-0.0003"/>
              <node id="2" lat="0" lon="0.0003"/>
              <way id="3"><nd ref="1"/><nd ref="2"/><tag k="highway" v="residential"/><tag k="width" v="6"/></way>
            </osm>
            """;
        var anchor = new MapStudioGeographicAnchor(0, 0, 50, 50);
        var rgb = MapStudioOsmVectorReferenceRaster.RenderRgb(xml, anchor, 0, 0, 100, 100, 1);
        Assert.Equal(255, rgb[(50 * 100 + 50) * 3]);
        Assert.Equal(235, rgb[0]);
        var png = MapStudioOsmVectorReferenceRaster.RenderPng(xml, anchor, 0, 0, 100, 100, 1);
        Assert.Equal(new byte[] {137,80,78,71,13,10,26,10}, png.Take(8).ToArray());
    }
}
