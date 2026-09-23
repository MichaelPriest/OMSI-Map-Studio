using MapStudio.Core.ProtonBus;
using Xunit;

namespace MapStudio.ProtonBus.Tests;

public sealed class ProtonBusOmsiDirectoryPackageExporterTests
{
    [Fact]
    public async Task ExporterLoadsGlobalTilesAndTimetableAutomatically()
    {
        var root =
            CreateRoot();

        var mapDirectory =
            Path.Combine(
                root,
                "maps",
                "TestMap");

        var output =
            Path.Combine(
                root,
                "export");

        try
        {
            Directory.CreateDirectory(
                mapDirectory);

            await File.WriteAllTextAsync(
                Path.Combine(
                    mapDirectory,
                    "global.cfg"),
                string.Join(
                    Environment.NewLine,
                    [
                        "[name]",
                        "Test OMSI Map",
                        "[map]",
                        "0",
                        "0",
                        "tile_0_0.map"
                    ]));

            await File.WriteAllTextAsync(
                Path.Combine(
                    mapDirectory,
                    "tile_0_0.map"),
                string.Join(
                    Environment.NewLine,
                    [
                        "[version]",
                        "14",
                        "[spline]",
                        "0",
                        @"Splines\Test\road.sli",
                        "1",
                        "-1",
                        "-1",
                        "0",
                        "0",
                        "0",
                        "0",
                        "10",
                        "0",
                        "0",
                        "0"
                    ]));

            CreateSplineFixture(
                root);

            var progress =
                new List<
                    ProtonBusOmsiDirectoryExportProgress>();

            var result =
                await new ProtonBusOmsiDirectoryPackageExporter()
                    .ExportAsync(
                        root,
                        mapDirectory,
                        output,
                        new(
                            "Mapa",
                            "Mapa",
                            "Rota"),
                        progress:
                            new ProgressCollector(
                                progress));

            Assert.True(
                result.IsExported);

            Assert.NotNull(
                result.Descriptor);

            Assert.Equal(
                "Test OMSI Map",
                result.Descriptor!
                    .DisplayName);

            Assert.NotNull(
                result.Timetable);

            Assert.NotNull(
                result.MapExport);

            Assert.NotNull(
                result.MapExport!
                    .Package);

            Assert.True(
                File.Exists(
                    result.MapExport
                        .Package!
                        .MapDefinitionPath));

            Assert.Single(
                result.MapExport
                    .Package
                    .ModelPaths);

            Assert.Single(
                result.MapExport
                    .Package
                    .TexturePaths);

            Assert.Single(
                result.MapExport
                    .Package
                    .VehiclePathPaths);

            Assert.Contains(
                progress,
                item =>
                    item.Stage ==
                    "tiles" &&
                    item.CompletedTiles ==
                    1 &&
                    item.TotalTiles ==
                    1);

            Assert.Contains(
                progress,
                item =>
                    item.Stage ==
                    "complete");

            Assert.DoesNotContain(
                result.Issues,
                issue =>
                    issue.Code is
                        "tilePathInvalid" or
                        "tileFileMissing" or
                        "tileReadFailed");
        }
        finally
        {
            DeleteRoot(
                root);
        }
    }

    [Fact]
    public async Task ExporterRejectsTilePathEscapingMapDirectory()
    {
        var root =
            CreateRoot();

        var mapDirectory =
            Path.Combine(
                root,
                "maps",
                "Unsafe");

        try
        {
            Directory.CreateDirectory(
                mapDirectory);

            await File.WriteAllTextAsync(
                Path.Combine(
                    mapDirectory,
                    "global.cfg"),
                string.Join(
                    Environment.NewLine,
                    [
                        "[name]",
                        "Unsafe",
                        "[map]",
                        "0",
                        "0",
                        "../outside.map"
                    ]));

            await File.WriteAllTextAsync(
                Path.Combine(
                    root,
                    "maps",
                    "outside.map"),
                "[version]\n14\n");

            var result =
                await new ProtonBusOmsiDirectoryPackageExporter()
                    .ExportAsync(
                        root,
                        mapDirectory,
                        Path.Combine(
                            root,
                            "export"),
                        new(
                            "Mapa",
                            "Mapa",
                            "Rota"));

            Assert.False(
                result.IsExported);

            Assert.Null(
                result.MapExport);

            Assert.Contains(
                result.Issues,
                issue =>
                    issue.Code ==
                    "tilePathInvalid");
        }
        finally
        {
            DeleteRoot(
                root);
        }
    }

    private static void CreateSplineFixture(
        string root)
    {
        var splinePath =
            Path.Combine(
                root,
                "Splines",
                "Test",
                "road.sli");

        Directory.CreateDirectory(
            Path.GetDirectoryName(
                splinePath)!);

        File.WriteAllText(
            splinePath,
            string.Join(
                Environment.NewLine,
                [
                    "[texture]",
                    "road.png",
                    "[profile]",
                    "0",
                    "[profilepnt]",
                    "-3",
                    "0",
                    "0",
                    "1",
                    "[profilepnt]",
                    "3",
                    "0",
                    "1",
                    "1",
                    "[path]",
                    "0",
                    "0",
                    "0.1",
                    "3",
                    "0"
                ]));

        var texturePath =
            Path.Combine(
                root,
                "Splines",
                "Test",
                "Texture",
                "road.png");

        Directory.CreateDirectory(
            Path.GetDirectoryName(
                texturePath)!);

        File.WriteAllBytes(
            texturePath,
            [
                137,
                80,
                78,
                71,
                13,
                10,
                26,
                10
            ]);
    }

    private static string CreateRoot()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudioProtonBusDirectoryTests",
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

    private sealed class ProgressCollector(
        ICollection<ProtonBusOmsiDirectoryExportProgress> output)
        : IProgress<ProtonBusOmsiDirectoryExportProgress>
    {
        public void Report(
            ProtonBusOmsiDirectoryExportProgress value) =>
            output.Add(
                value);
    }
}
