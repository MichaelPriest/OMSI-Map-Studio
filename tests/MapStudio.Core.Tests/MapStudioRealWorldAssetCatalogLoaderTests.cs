using MapStudio.Core.Omsi.Structures;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioRealWorldAssetCatalogLoaderTests
{
    [Fact]
    public async Task LoadAsyncRefreshesPersistentIndexAndReturnsSceneryObjects()
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

            Assert.Equal(
                3,
                second.Refresh.UnchangedFiles);

            Assert.Equal(
                2,
                second.SceneryObjects.Count);
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
