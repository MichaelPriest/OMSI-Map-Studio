using System.Text;
using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Terrain;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Structures;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioRealWorldJunctionElevationTests
{
    [Fact]
    public async Task PhysicalJunctionUsesSameDemDatumAsConnectedRoads()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-junction-elevation-" +
                Guid.NewGuid().ToString("N"));

        try
        {
            var mapDirectory =
                Path.Combine(
                    root,
                    "maps",
                    "JunctionElevation");

            Directory.CreateDirectory(
                mapDirectory);

            await File.WriteAllTextAsync(
                Path.Combine(
                    mapDirectory,
                    "global.cfg"),
                "[name]\r\nJunction Elevation\r\n" +
                "[map]\r\n0\r\n0\r\ntile_0_0.map\r\n",
                Encoding.UTF8);

            var tilePath =
                Path.Combine(
                    mapDirectory,
                    "tile_0_0.map");

            await File.WriteAllTextAsync(
                tilePath,
                "[version]\r\n14\r\n",
                Encoding.UTF8);

            var elevation =
                new MapStudioGeoreferencedElevationSurface(
                    new MapStudioElevationGrid(
                        2,
                        2,
                        [
                            110,
                            110,
                            100,
                            100
                        ],
                        100,
                        110,
                        "junction-dem"),
                    south: -0.001,
                    west: -0.001,
                    north: 0.001,
                    east: 0.001);

            var result =
                await new MapStudioRoadBatchReconstructionRunner()
                    .RunAsync(
                        root,
                        mapDirectory,
                        JunctionXml,
                        new MapStudioGeographicAnchor(
                            0,
                            0,
                            50,
                            50),
                        CancellationToken.None,
                        elevation);

            var placement =
                Assert.Single(
                    result.JunctionPlacements);

            var content =
                await new OmsiTileReader()
                    .ReadContentAsync(
                        tilePath);

            var junction =
                Assert.Single(
                    content.Objects,
                    item =>
                        item.ObjectId ==
                        placement.ObjectId);

            Assert.InRange(
                junction.X,
                placement.LocalX - 0.001,
                placement.LocalX + 0.001);

            Assert.InRange(
                junction.Y,
                placement.LocalZ - 0.001,
                placement.LocalZ + 0.001);

            Assert.InRange(
                junction.Z,
                -0.001,
                0.001);

            var touchingSplines =
                content.Splines
                    .Where(
                        spline =>
                            Math.Abs(
                                spline.X -
                                placement.LocalX) <
                                0.01 ||
                            Math.Abs(
                                spline.Y -
                                placement.LocalZ) <
                                0.01)
                    .ToArray();

            Assert.NotEmpty(
                touchingSplines);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }

    private const string JunctionXml =
        """
        <osm version="0.6">
          <node id="1" lat="0.00000" lon="-0.00010"/>
          <node id="2" lat="0.00000" lon="-0.00005"/>
          <node id="3" lat="0.00000" lon="0.00000"/>
          <node id="4" lat="0.00000" lon="0.00005"/>
          <node id="5" lat="0.00000" lon="0.00010"/>
          <node id="6" lat="-0.00010" lon="0.00000"/>
          <node id="7" lat="-0.00005" lon="0.00000"/>
          <node id="8" lat="0.00005" lon="0.00000"/>
          <node id="9" lat="0.00010" lon="0.00000"/>

          <way id="100">
            <nd ref="1"/><nd ref="2"/><nd ref="3"/><nd ref="4"/><nd ref="5"/>
            <tag k="highway" v="residential"/>
            <tag k="lanes" v="2"/>
            <tag k="width" v="7"/>
          </way>

          <way id="200">
            <nd ref="6"/><nd ref="7"/><nd ref="3"/><nd ref="8"/><nd ref="9"/>
            <tag k="highway" v="secondary"/>
            <tag k="lanes" v="2"/>
            <tag k="width" v="7"/>
          </way>
        </osm>
        """;
}
