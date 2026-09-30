using System.Text;
using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Omsi.Maps;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioGeneratedSceneryBatchWriterTests
{
    [Fact]
    public async Task WriteStoresGroundPlaneAndHeightInOmsiAxisOrder()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-generated-scenery-" +
                Guid.NewGuid().ToString("N"));

        try
        {
            var mapDirectory =
                Path.Combine(
                    root,
                    "maps",
                    "GeneratedScenery");

            Directory.CreateDirectory(
                mapDirectory);

            await File.WriteAllTextAsync(
                Path.Combine(
                    mapDirectory,
                    "global.cfg"),
                "[name]\r\nGenerated Scenery\r\n" +
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

            var assetDirectory =
                Path.Combine(
                    root,
                    "Sceneryobjects",
                    "Test");

            Directory.CreateDirectory(
                assetDirectory);

            var assetPath =
                Path.Combine(
                    assetDirectory,
                    "test.sco");

            await File.WriteAllTextAsync(
                assetPath,
                "[friendlyname]\r\nTest\r\n",
                Encoding.ASCII);

            await new MapStudioGeneratedSceneryBatchWriter()
                .WriteAsync(
                    root,
                    mapDirectory,
                    [
                        new MapStudioGeneratedSceneryPlacementRequest(
                            "test",
                            assetPath,
                            new MapStudioRoadPoint(
                                45,
                                120),
                            HeightMeters:
                                7.5)
                    ]);

            var content =
                await new OmsiTileReader()
                    .ReadContentAsync(
                        tilePath);

            var placed =
                Assert.Single(
                    content.Objects);

            Assert.Equal(
                45,
                placed.X,
                6);

            Assert.Equal(
                120,
                placed.Y,
                6);

            Assert.Equal(
                7.5,
                placed.Z,
                6);
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
}
