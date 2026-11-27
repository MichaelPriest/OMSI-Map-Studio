using System.Text;
using MapStudio.Core.Omsi.Maps;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiTileWaterTests
{
    [Fact]
    public async Task TileReaderLoadsWaterMarkerAndSidecar()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-WaterTile-" +
                Guid.NewGuid()
                    .ToString("N"));

        Directory.CreateDirectory(
            root);

        try
        {
            var tilePath =
                Path.Combine(
                    root,
                    "tile_0_0.map");

            await File.WriteAllTextAsync(
                tilePath,
                "[version]\r\n14\r\n\r\n" +
                "[terrain]\r\n\r\n" +
                "[water]\r\n\r\n",
                Encoding.Unicode);

            var expected =
                new OmsiWaterGrid(
                    [
                        -1.0f,
                        -0.5f,
                        0.25f,
                        1.5f
                    ]);

            await File.WriteAllBytesAsync(
                tilePath +
                ".water",
                OmsiWaterWriter
                    .Write(
                        expected));

            var reader =
                new OmsiTileReader();

            var content =
                await reader
                    .ReadContentAsync(
                        tilePath);

            Assert.True(
                content.Summary
                    .WaterMarkerPresent);

            Assert.True(
                content.Summary
                    .WaterFileExists);

            Assert.Equal(
                20,
                content.Summary
                    .WaterFileSize);

            Assert.NotNull(
                content.Water);

            Assert.Equal(
                expected.Heights,
                content.Water!
                    .Heights);

            var light =
                await reader
                    .ReadSummaryLightAsync(
                        tilePath);

            Assert.True(
                light.WaterMarkerPresent);

            Assert.True(
                light.WaterFileExists);

            Assert.Equal(
                20,
                light.WaterFileSize);
        }
        finally
        {
            if (
                Directory.Exists(
                    root))
            {
                Directory.Delete(
                    root,
                    recursive:
                        true);
            }
        }
    }

    [Fact]
    public async Task TileReaderKeepsMapReadableWhenWaterSidecarIsInvalid()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-WaterInvalid-" +
                Guid.NewGuid()
                    .ToString("N"));

        Directory.CreateDirectory(
            root);

        try
        {
            var tilePath =
                Path.Combine(
                    root,
                    "tile_0_0.map");

            await File.WriteAllTextAsync(
                tilePath,
                "[version]\r\n14\r\n\r\n" +
                "[water]\r\n\r\n",
                Encoding.Unicode);

            await File.WriteAllBytesAsync(
                tilePath +
                ".water",
                new byte[16]);

            var content =
                await new OmsiTileReader()
                    .ReadContentAsync(
                        tilePath);

            Assert.True(
                content.Summary
                    .WaterMarkerPresent);

            Assert.True(
                content.Summary
                    .WaterFileExists);

            Assert.Equal(
                16,
                content.Summary
                    .WaterFileSize);

            Assert.Null(
                content.Water);
        }
        finally
        {
            if (
                Directory.Exists(
                    root))
            {
                Directory.Delete(
                    root,
                    recursive:
                        true);
            }
        }
    }
}
