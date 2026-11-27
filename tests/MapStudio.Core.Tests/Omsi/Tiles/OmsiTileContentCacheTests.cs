using MapStudio.Core.Omsi.Maps;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiTileContentCacheTests
{
    [Fact]
    public async Task ReadContentAsync_ReusesUnchangedTile()
    {
        var directory =
            CreateTemporaryDirectory();

        try
        {
            var tilePath =
                Path.Combine(
                    directory,
                    "tile_0_0.map");

            await File.WriteAllTextAsync(
                tilePath,
                string.Empty);

            var cache =
                new OmsiTileContentCache(
                    capacity: 4);

            var first =
                await cache
                    .ReadContentAsync(
                        tilePath);

            var second =
                await cache
                    .ReadContentAsync(
                        tilePath);

            Assert.Same(
                first,
                second);

            Assert.Equal(
                1,
                cache.Count);
        }
        finally
        {
            DeleteTemporaryDirectory(
                directory);
        }
    }

    [Fact]
    public async Task ReadContentAsync_InvalidatesWhenMapFileChanges()
    {
        var directory =
            CreateTemporaryDirectory();

        try
        {
            var tilePath =
                Path.Combine(
                    directory,
                    "tile_0_0.map");

            await File.WriteAllTextAsync(
                tilePath,
                string.Empty);

            var cache =
                new OmsiTileContentCache(
                    capacity: 4);

            var first =
                await cache
                    .ReadContentAsync(
                        tilePath);

            await File.WriteAllTextAsync(
                tilePath,
                "[terrain]\r\n");

            var second =
                await cache
                    .ReadContentAsync(
                        tilePath);

            Assert.NotSame(
                first,
                second);
        }
        finally
        {
            DeleteTemporaryDirectory(
                directory);
        }
    }

    [Fact]
    public async Task ReadContentAsync_InvalidatesWhenTerrainAppears()
    {
        var directory =
            CreateTemporaryDirectory();

        try
        {
            var tilePath =
                Path.Combine(
                    directory,
                    "tile_0_0.map");

            await File.WriteAllTextAsync(
                tilePath,
                string.Empty);

            var cache =
                new OmsiTileContentCache(
                    capacity: 4);

            var first =
                await cache
                    .ReadContentAsync(
                        tilePath);

            await File.WriteAllBytesAsync(
                tilePath +
                    ".terrain",
                new byte[4]);

            var second =
                await cache
                    .ReadContentAsync(
                        tilePath);

            Assert.NotSame(
                first,
                second);
        }
        finally
        {
            DeleteTemporaryDirectory(
                directory);
        }
    }

    [Fact]
    public async Task ReadContentAsync_EvictsLeastRecentlyUsedTile()
    {
        var directory =
            CreateTemporaryDirectory();

        try
        {
            var firstPath =
                Path.Combine(
                    directory,
                    "tile_0_0.map");

            var secondPath =
                Path.Combine(
                    directory,
                    "tile_1_0.map");

            var thirdPath =
                Path.Combine(
                    directory,
                    "tile_2_0.map");

            await File.WriteAllTextAsync(
                firstPath,
                string.Empty);

            await File.WriteAllTextAsync(
                secondPath,
                string.Empty);

            await File.WriteAllTextAsync(
                thirdPath,
                string.Empty);

            var cache =
                new OmsiTileContentCache(
                    capacity: 2);

            var first =
                await cache
                    .ReadContentAsync(
                        firstPath);

            var second =
                await cache
                    .ReadContentAsync(
                        secondPath);

            Assert.Same(
                first,
                await cache
                    .ReadContentAsync(
                        firstPath));

            await cache
                .ReadContentAsync(
                    thirdPath);

            var secondReloaded =
                await cache
                    .ReadContentAsync(
                        secondPath);

            Assert.NotSame(
                second,
                secondReloaded);

            Assert.Equal(
                2,
                cache.Count);
        }
        finally
        {
            DeleteTemporaryDirectory(
                directory);
        }
    }

    [Fact]
    public void Constructor_RejectsNonPositiveCapacity()
    {
        Assert.Throws<
            ArgumentOutOfRangeException>(
            () =>
                new OmsiTileContentCache(
                    0));
    }

    private static string
        CreateTemporaryDirectory()
    {
        var path =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio.Core.Tests",
                Guid.NewGuid()
                    .ToString(
                        "N"));

        Directory.CreateDirectory(
            path);

        return path;
    }

    private static void
        DeleteTemporaryDirectory(
            string path)
    {
        if (Directory.Exists(
                path))
        {
            Directory.Delete(
                path,
                true);
        }
    }
}
