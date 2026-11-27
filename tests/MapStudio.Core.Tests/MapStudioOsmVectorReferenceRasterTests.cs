using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Scene;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioOsmVectorReferenceRasterTests
{
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
