using System.IO.Compression;
using MapStudio.Core.ProtonBus;
using Xunit;

namespace MapStudio.ProtonBus.Tests;

public sealed class ProtonBusPackageArchiveWriterTests
{
    [Fact]
    public void WriterArchivesOnlyGeneratedPackageFiles()
    {
        var root =
            CreateRoot();

        try
        {
            var map =
                CreateFile(
                    root,
                    Path.Combine(
                        "maps",
                        "Mapa.map.txt"),
                    "[map]");

            var model =
                CreateFile(
                    root,
                    Path.Combine(
                        "maps",
                        "Mapa",
                        "tiles",
                        "Rota",
                        "tile_0_0.3ds"),
                    "3ds");

            var texture =
                CreateFile(
                    root,
                    Path.Combine(
                        "maps",
                        "Mapa",
                        "textures",
                        "road.png"),
                    "png");

            var destination =
                Path.Combine(
                    root,
                    "maps",
                    "Mapa",
                    "dest",
                    "Linha-01");

            Directory.CreateDirectory(
                destination);

            CreateFile(
                destination,
                "intercity.txt",
                string.Empty);

            CreateFile(
                root,
                "unrelated.txt",
                "do not archive");

            var package =
                new ProtonBusMapPackageResult(
                    root,
                    map,
                    [
                        model
                    ],
                    [
                        texture
                    ])
                {
                    DestinationDirectories =
                    [
                        destination
                    ]
                };

            var archivePath =
                ProtonBusPackageArchiveWriter
                    .Write(
                        package,
                        Path.Combine(
                            root,
                            "Mapa-ProtonBus.zip"));

            Assert.True(
                File.Exists(
                    archivePath));

            using var archive =
                ZipFile.OpenRead(
                    archivePath);

            var entries =
                archive.Entries
                    .Select(
                        entry =>
                            entry.FullName)
                    .OrderBy(
                        value =>
                            value,
                        StringComparer.Ordinal)
                    .ToArray();

            Assert.Contains(
                "maps/Mapa.map.txt",
                entries);

            Assert.Contains(
                "maps/Mapa/tiles/Rota/tile_0_0.3ds",
                entries);

            Assert.Contains(
                "maps/Mapa/textures/road.png",
                entries);

            Assert.Contains(
                "maps/Mapa/dest/Linha-01/intercity.txt",
                entries);

            Assert.DoesNotContain(
                "unrelated.txt",
                entries);

            Assert.DoesNotContain(
                "Mapa-ProtonBus.zip",
                entries);
        }
        finally
        {
            DeleteRoot(
                root);
        }
    }

    [Fact]
    public void WriterRejectsGeneratedPathOutsideOutputRoot()
    {
        var root =
            CreateRoot();

        var outside =
            CreateRoot();

        try
        {
            var map =
                CreateFile(
                    outside,
                    "Mapa.map.txt",
                    "[map]");

            var package =
                new ProtonBusMapPackageResult(
                    root,
                    map,
                    [],
                    []);

            Assert.Throws<
                InvalidDataException>(
                    () =>
                        ProtonBusPackageArchiveWriter
                            .Write(
                                package,
                                Path.Combine(
                                    root,
                                    "Mapa.zip")));
        }
        finally
        {
            DeleteRoot(
                root);

            DeleteRoot(
                outside);
        }
    }

    private static string CreateFile(
        string root,
        string relativePath,
        string content)
    {
        var path =
            Path.Combine(
                root,
                relativePath);

        var directory =
            Path.GetDirectoryName(
                path);

        if (
            !string.IsNullOrWhiteSpace(
                directory))
        {
            Directory.CreateDirectory(
                directory);
        }

        File.WriteAllText(
            path,
            content);

        return path;
    }

    private static string CreateRoot()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudioProtonBusArchiveTests",
                Guid.NewGuid()
                    .ToString("N"));

        Directory.CreateDirectory(
            root);

        return root;
    }

    private static void DeleteRoot(
        string root)
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
