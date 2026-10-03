using MapStudio.Renderer.Scene;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeDerivedFileCacheTests
{
    [Fact]
    public void ReusesValueWhileFileFingerprintIsUnchanged()
    {
        var directory =
            CreateTemporaryDirectory();

        try
        {
            var path =
                Path.Combine(
                    directory,
                    "asset.sco");

            File.WriteAllText(
                path,
                "original");

            var cache =
                new NativeDerivedFileCache<
                    string>(
                    4);

            cache.Set(
                path,
                "cached");

            Assert.True(
                cache.TryGet(
                    path,
                    out var value));

            Assert.Equal(
                "cached",
                value);
        }
        finally
        {
            DeleteTemporaryDirectory(
                directory);
        }
    }

    [Fact]
    public void InvalidatesValueWhenFileFingerprintChanges()
    {
        var directory =
            CreateTemporaryDirectory();

        try
        {
            var path =
                Path.Combine(
                    directory,
                    "asset.o3d");

            File.WriteAllText(
                path,
                "short");

            var cache =
                new NativeDerivedFileCache<
                    string>(
                    4);

            cache.Set(
                path,
                "cached");

            File.WriteAllText(
                path,
                "this-content-is-longer");

            Assert.False(
                cache.TryGet(
                    path,
                    out _));

            Assert.Equal(
                0,
                cache.Count);
        }
        finally
        {
            DeleteTemporaryDirectory(
                directory);
        }
    }

    [Fact]
    public void MissingFileEntryInvalidatesWhenFileAppears()
    {
        var directory =
            CreateTemporaryDirectory();

        try
        {
            var path =
                Path.Combine(
                    directory,
                    "late.sli");

            var cache =
                new NativeDerivedFileCache<
                    string>(
                    4);

            cache.Set(
                path,
                "missing");

            Assert.True(
                cache.TryGet(
                    path,
                    out var cached));

            Assert.Equal(
                "missing",
                cached);

            File.WriteAllText(
                path,
                "created");

            Assert.False(
                cache.TryGet(
                    path,
                    out _));
        }
        finally
        {
            DeleteTemporaryDirectory(
                directory);
        }
    }

    [Fact]
    public void EvictsLeastRecentlyUsedEntryAtCapacity()
    {
        var directory =
            CreateTemporaryDirectory();

        try
        {
            var first =
                Path.Combine(
                    directory,
                    "first.sco");

            var second =
                Path.Combine(
                    directory,
                    "second.sco");

            var third =
                Path.Combine(
                    directory,
                    "third.sco");

            File.WriteAllText(
                first,
                "1");

            File.WriteAllText(
                second,
                "2");

            File.WriteAllText(
                third,
                "3");

            var cache =
                new NativeDerivedFileCache<
                    string>(
                    2);

            cache.Set(
                first,
                "first");

            cache.Set(
                second,
                "second");

            Assert.True(
                cache.TryGet(
                    first,
                    out _));

            cache.Set(
                third,
                "third");

            Assert.True(
                cache.TryGet(
                    first,
                    out _));

            Assert.False(
                cache.TryGet(
                    second,
                    out _));

            Assert.True(
                cache.TryGet(
                    third,
                    out _));

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
    public void RejectsNonPositiveCapacity()
    {
        Assert.Throws<
            ArgumentOutOfRangeException>(
            () =>
                new NativeDerivedFileCache<
                    string>(
                    0));
    }

    private static string
        CreateTemporaryDirectory()
    {
        var path =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio.Renderer.Tests",
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
