using System.Text;
using MapStudio.Core.Omsi.Maps;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiLazyLoadingTests
{
    [Fact]
    public async Task CatalogDiscovery_DoesNotReadTileFiles()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"mapstudio-lazy-{Guid.NewGuid():N}");

        var mapDirectory = Path.Combine(
            root,
            "maps",
            "TestMap");

        Directory.CreateDirectory(mapDirectory);

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(
                    mapDirectory,
                    "global.cfg"),
                "[name]\r\nFast Map\r\n" +
                "[map]\r\n0\r\n0\r\ntile_0_0.map\r\n",
                new UTF8Encoding(false));

            await File.WriteAllTextAsync(
                Path.Combine(
                    mapDirectory,
                    "tile_0_0.map"),
                "[object]\r\n0\r\nSceneryobjects\\Building.sco\r\n" +
                "1\r\n0\r\n0\r\n0\r\n0\r\n0\r\n0\r\n",
                new UTF8Encoding(false));

            var maps =
                await new OmsiMapCatalog()
                    .DiscoverAsync(root);

            var map = Assert.Single(maps);
            var tile = Assert.Single(map.Tiles);

            Assert.Equal(
                "Fast Map",
                map.DisplayName);

            Assert.Null(tile.Summary);
        }
        finally
        {
            Directory.Delete(
                root,
                recursive: true);
        }
    }

    [Fact]
    public async Task TileContent_ReadsSummaryAndObjectsTogether()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"mapstudio-content-{Guid.NewGuid():N}.map");

        try
        {
            await File.WriteAllTextAsync(
                path,
                "[spline]\r\n0\r\nSplines\\Street.sli\r\n" +
                "[object]\r\n0\r\nSceneryobjects\\Building.sco\r\n" +
                "42\r\n10\r\n20\r\n1\r\n90\r\n0\r\n0\r\n",
                new UTF8Encoding(false));

            var content =
                await new OmsiTileReader()
                    .ReadContentAsync(path);

            Assert.True(
                content.Summary.Exists);

            Assert.Equal(
                1,
                content.Summary.ObjectCount);

            Assert.Equal(
                1,
                content.Summary.SplineCount);

            var placedObject =
                Assert.Single(
                    content.Objects);

            Assert.Equal(
                42,
                placedObject.ObjectId);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
