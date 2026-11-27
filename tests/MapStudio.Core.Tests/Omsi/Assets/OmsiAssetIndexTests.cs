using MapStudio.Core.Omsi.Indexing;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiAssetIndexTests
{
    [Fact]
    public async Task RefreshAsync_IndexesAndReusesUnchangedAssets()
    {
        var root =
            CreateTemporaryOmsiRoot();

        var databasePath =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio.Core.Tests",
                Guid.NewGuid()
                    .ToString("N"),
                "assets.sqlite");

        try
        {
            WriteAsset(
                root,
                "Sceneryobjects/Test/object.sco",
                "[friendlyname]\nObject");
            WriteAsset(
                root,
                "Sceneryobjects/Test/model/test.o3d",
                "o3d");
            WriteAsset(
                root,
                "Splines/Test/road.sli",
                "[spline]");
            WriteAsset(
                root,
                "Texture/test.bmp",
                "bmp");
            WriteAsset(
                root,
                "Sceneryobjects/Test/readme.txt",
                "ignored");

            var index =
                new OmsiAssetIndex(
                    databasePath);

            var first =
                await index.RefreshAsync(
                    root);

            Assert.Equal(5, first.ExaminedFiles);
            Assert.Equal(4, first.TotalEntries);
            Assert.Equal(4, first.AddedFiles);
            Assert.Equal(0, first.UpdatedFiles);
            Assert.Equal(0, first.UnchangedFiles);
            Assert.Equal(0, first.RemovedFiles);

            var second =
                await index.RefreshAsync(
                    root);

            Assert.Equal(4, second.TotalEntries);
            Assert.Equal(0, second.AddedFiles);
            Assert.Equal(0, second.UpdatedFiles);
            Assert.Equal(4, second.UnchangedFiles);
            Assert.Equal(0, second.RemovedFiles);

            var statistics =
                await index.GetStatisticsAsync();

            Assert.Equal(4, statistics.TotalEntries);
            Assert.Equal(1, statistics.SceneryObjects);
            Assert.Equal(1, statistics.Splines);
            Assert.Equal(1, statistics.Models);
            Assert.Equal(1, statistics.Textures);
            Assert.NotNull(statistics.RefreshedAtUtc);
        }
        finally
        {
            DeleteDirectorySafe(root);
            DeleteDirectorySafe(
                Path.GetDirectoryName(
                    databasePath)!);
        }
    }

    [Fact]
    public async Task RefreshAsync_UpdatesChangedAndRemovesMissingAssets()
    {
        var root =
            CreateTemporaryOmsiRoot();

        var databasePath =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio.Core.Tests",
                Guid.NewGuid()
                    .ToString("N"),
                "assets.sqlite");

        try
        {
            var objectPath =
                WriteAsset(
                    root,
                    "Sceneryobjects/Test/object.sco",
                    "one");

            var splinePath =
                WriteAsset(
                    root,
                    "Splines/Test/road.sli",
                    "road");

            var index =
                new OmsiAssetIndex(
                    databasePath);

            await index.RefreshAsync(
                root);

            await File.AppendAllTextAsync(
                objectPath,
                "-changed");

            File.SetLastWriteTimeUtc(
                objectPath,
                DateTime.UtcNow
                    .AddSeconds(2));

            File.Delete(
                splinePath);

            var refreshed =
                await index.RefreshAsync(
                    root);

            Assert.Equal(1, refreshed.TotalEntries);
            Assert.Equal(0, refreshed.AddedFiles);
            Assert.Equal(1, refreshed.UpdatedFiles);
            Assert.Equal(0, refreshed.UnchangedFiles);
            Assert.Equal(1, refreshed.RemovedFiles);

            var entries =
                await index.GetEntriesAsync();

            var entry =
                Assert.Single(entries);

            Assert.Equal(
                OmsiAssetKind.SceneryObject,
                entry.Kind);

            Assert.EndsWith(
                "Sceneryobjects/Test/object.sco",
                entry.RelativePath,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectorySafe(root);
            DeleteDirectorySafe(
                Path.GetDirectoryName(
                    databasePath)!);
        }
    }

    private static string
        CreateTemporaryOmsiRoot()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio.Core.Tests",
                Guid.NewGuid()
                    .ToString("N"),
                "OMSI 2");

        Directory.CreateDirectory(
            Path.Combine(
                root,
                "maps"));
        Directory.CreateDirectory(
            Path.Combine(
                root,
                "Sceneryobjects"));
        Directory.CreateDirectory(
            Path.Combine(
                root,
                "Splines"));
        Directory.CreateDirectory(
            Path.Combine(
                root,
                "Texture"));

        return root;
    }

    private static string WriteAsset(
        string root,
        string relativePath,
        string content)
    {
        var path =
            Path.Combine(
                root,
                relativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar));

        Directory.CreateDirectory(
            Path.GetDirectoryName(
                path)!);

        File.WriteAllText(
            path,
            content);

        return path;
    }

    private static void DeleteDirectorySafe(
        string directory)
    {
        try
        {
            if (
                Directory.Exists(
                    directory))
            {
                Directory.Delete(
                    directory,
                    recursive: true);
            }
        }
        catch
        {
            // Best-effort test cleanup.
        }
    }
}
