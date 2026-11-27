using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Maps;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiTerrainDiagnosticsTests
{
    [Fact]
    public void ReadContent_DetectsTerrainMarker()
    {
        var content =
            OmsiTileReader.ReadContent(
                OmsiConfigParser.Parse(
                    "[version]\n14\n[terrain]\n"));

        Assert.True(
            content.Summary
                .TerrainMarkerPresent);

        Assert.False(
            content.Summary
                .TerrainFileExists);

        Assert.Equal(
            0,
            content.Summary
                .TerrainFileSize);
    }

    [Fact]
    public async Task ReadContentAsync_DetectsTerrainSidecar()
    {
        var directory =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-TerrainTests",
                Guid.NewGuid()
                    .ToString("N"));

        Directory.CreateDirectory(
            directory);

        try
        {
            var tilePath =
                Path.Combine(
                    directory,
                    "tile_0_0.map");

            await File.WriteAllTextAsync(
                tilePath,
                "[version]\n14\n[terrain]\n");

            await File.WriteAllBytesAsync(
                tilePath + ".terrain",
                [1, 2, 3, 4, 5, 6]);

            var reader =
                new OmsiTileReader();

            var content =
                await reader
                    .ReadContentAsync(
                        tilePath);

            Assert.True(
                content.Summary
                    .TerrainMarkerPresent);

            Assert.True(
                content.Summary
                    .TerrainFileExists);

            Assert.Equal(
                6,
                content.Summary
                    .TerrainFileSize);
        }
        finally
        {
            Directory.Delete(
                directory,
                recursive: true);
        }
    }

    [Fact]
    public async Task ReadContentAsync_ReportsSidecarEvenWithoutMarker()
    {
        var directory =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-TerrainTests",
                Guid.NewGuid()
                    .ToString("N"));

        Directory.CreateDirectory(
            directory);

        try
        {
            var tilePath =
                Path.Combine(
                    directory,
                    "tile_1_0.map");

            await File.WriteAllTextAsync(
                tilePath,
                "[version]\n14\n");

            await File.WriteAllBytesAsync(
                tilePath + ".terrain",
                [10, 20]);

            var reader =
                new OmsiTileReader();

            var content =
                await reader
                    .ReadContentAsync(
                        tilePath);

            Assert.False(
                content.Summary
                    .TerrainMarkerPresent);

            Assert.True(
                content.Summary
                    .TerrainFileExists);

            Assert.Equal(
                2,
                content.Summary
                    .TerrainFileSize);
        }
        finally
        {
            Directory.Delete(
                directory,
                recursive: true);
        }
    }
}
