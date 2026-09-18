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
    public async Task CatalogDiscovery_SkipsUnreadableMapAndContinues()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"mapstudio-catalog-errors-{Guid.NewGuid():N}");

        var goodMap = Path.Combine(
            root,
            "maps",
            "Good");

        var badMap = Path.Combine(
            root,
            "maps",
            "Bad");

        Directory.CreateDirectory(goodMap);
        Directory.CreateDirectory(badMap);

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(
                    goodMap,
                    "global.cfg"),
                "[name]\r\nGood Map\r\n",
                new UTF8Encoding(false));

            var badGlobal = Path.Combine(
                badMap,
                "global.cfg");

            await File.WriteAllTextAsync(
                badGlobal,
                "[name]\r\nLocked Map\r\n",
                new UTF8Encoding(false));

            using var lockStream =
                new FileStream(
                    badGlobal,
                    FileMode.Open,
                    FileAccess.ReadWrite,
                    FileShare.None);

            var result =
                await new OmsiMapCatalog()
                    .DiscoverWithProgressAsync(
                        root);

            var map =
                Assert.Single(result.Maps);

            Assert.Equal(
                "Good Map",
                map.DisplayName);

            Assert.Equal(
                1,
                result.SkippedMaps);
        }
        finally
        {
            Directory.Delete(
                root,
                recursive: true);
        }
    }

    [Fact]
    public async Task OpenMapAsync_ReadsOnlySelectedMap()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"mapstudio-open-map-{Guid.NewGuid():N}");

        var selectedMap = Path.Combine(
            root,
            "maps",
            "Selected");

        var otherMap = Path.Combine(
            root,
            "maps",
            "Other");

        Directory.CreateDirectory(selectedMap);
        Directory.CreateDirectory(otherMap);

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(
                    selectedMap,
                    "global.cfg"),
                "[name]\r\nSelected Map\r\n" +
                "[map]\r\n0\r\n0\r\ntile_0_0.map\r\n",
                new UTF8Encoding(false));

            await File.WriteAllTextAsync(
                Path.Combine(
                    otherMap,
                    "global.cfg"),
                "[name]\r\nOther Map\r\n",
                new UTF8Encoding(false));

            var descriptor =
                await OmsiMapCatalog.OpenMapAsync(
                    selectedMap);

            Assert.Equal(
                "Selected Map",
                descriptor.DisplayName);

            Assert.Equal(
                "Selected",
                descriptor.DirectoryName);

            Assert.Single(
                descriptor.Tiles);
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
