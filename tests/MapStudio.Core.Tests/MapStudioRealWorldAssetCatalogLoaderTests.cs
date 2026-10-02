using Microsoft.Data.Sqlite;
using MapStudio.Core.Omsi.Structures;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioRealWorldAssetCatalogLoaderTests
{
    [Fact]
    public async Task LoadAsyncReusesPersistentIndexAndSupportsForcedRefresh()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-realworld-assets-" +
                Guid.NewGuid().ToString("N"));

        try
        {
            var sceneryDirectory =
                Path.Combine(
                    root,
                    "Sceneryobjects",
                    "Vegetation");

            var splineDirectory =
                Path.Combine(
                    root,
                    "Splines",
                    "Roads");

            Directory.CreateDirectory(sceneryDirectory);
            Directory.CreateDirectory(splineDirectory);

            await File.WriteAllTextAsync(
                Path.Combine(
                    sceneryDirectory,
                    "tree.sco"),
                "[friendlyname]\r\nTree\r\n");

            await File.WriteAllTextAsync(
                Path.Combine(
                    sceneryDirectory,
                    "lamp.sco"),
                "[friendlyname]\r\nLamp\r\n");

            await File.WriteAllTextAsync(
                Path.Combine(
                    splineDirectory,
                    "road.sli"),
                "[spline]\r\n");

            var loader =
                new MapStudioRealWorldAssetCatalogLoader();

            var first =
                await loader.LoadAsync(root);

            Assert.Equal(
                2,
                first.SceneryObjects.Count);

            Assert.Single(
                first.Splines);

            Assert.Single(
                first.ConstructionAssets
                    .RoadSplines);

            Assert.Equal(
                3,
                first.Refresh.TotalEntries);

            Assert.True(
                File.Exists(
                    first.DatabasePath));

            Assert.EndsWith(
                Path.Combine(
                    ".mapstudio",
                    "cache",
                    "assets.sqlite"),
                first.DatabasePath,
                StringComparison.OrdinalIgnoreCase);

            var second =
                await loader.LoadAsync(root);

            Assert.True(
                second.ReusedExistingIndex);

            Assert.Equal(
                0,
                second.Refresh.ExaminedFiles);

            Assert.Equal(
                3,
                second.Refresh.TotalEntries);

            Assert.Equal(
                3,
                second.Refresh.UnchangedFiles);

            Assert.Equal(
                2,
                second.SceneryObjects.Count);

            Assert.Single(
                second.Splines);

            Assert.Single(
                second.ConstructionAssets
                    .RoadSplines);

            await File.WriteAllTextAsync(
                Path.Combine(
                    sceneryDirectory,
                    "new-object.sco"),
                "[friendlyname]\r\nNew object\r\n");

            var stillCached =
                await loader.LoadAsync(root);

            Assert.True(
                stillCached.ReusedExistingIndex);

            Assert.Equal(
                2,
                stillCached.SceneryObjects.Count);

            var forced =
                await loader.LoadAsync(
                    root,
                    forceRefresh:
                        true);

            Assert.False(
                forced.ReusedExistingIndex);

            Assert.True(
                forced.Refresh.ExaminedFiles >
                    0);

            Assert.Equal(
                3,
                forced.SceneryObjects.Count);

            Assert.Equal(
                4,
                forced.Refresh.TotalEntries);
        }
        finally
        {
            SqliteConnection.ClearAllPools();

            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }
}
