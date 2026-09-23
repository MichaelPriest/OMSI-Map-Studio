using MapStudio.Core.ProtonBus;
using Xunit;

namespace MapStudio.ProtonBus.Tests;

public sealed class ProtonBusOmsiDirectoryPreflightAnalyzerTests
{
    [Fact]
    public async Task AnalyzerReportsReadyMapWithoutWritingPackageFiles()
    {
        var root =
            CreateRoot();

        var mapDirectory =
            CreateSingleTileMap(
                root,
                @"Splines\Test\road.sli");

        try
        {
            CreateSplineFixture(
                root,
                @"Splines\Test\road.sli");

            var progress =
                new List<
                    ProtonBusOmsiDirectoryExportProgress>();

            var result =
                await new ProtonBusOmsiDirectoryPreflightAnalyzer()
                    .AnalyzeAsync(
                        root,
                        mapDirectory,
                        new(
                            "Mapa",
                            "Mapa",
                            "Rota"),
                        progress:
                            new ProgressCollector(
                                progress));

            Assert.True(
                result.CanExport);

            Assert.Equal(
                0,
                result.ErrorCount);

            Assert.Equal(
                1,
                result.Summary.TileCount);

            Assert.Equal(
                1,
                result.Summary.SplineCount);

            Assert.Equal(
                1,
                result.Summary.SplineMeshCount);

            Assert.Equal(
                1,
                result.Summary.TextureCount);

            Assert.Equal(
                1,
                result.Summary.VehiclePathCount);

            Assert.Equal(
                0,
                result.Summary.BusStopCount);

            Assert.Contains(
                progress,
                item =>
                    item.Stage ==
                    "preflight-complete");

            Assert.Empty(
                Directory.EnumerateFiles(
                    root,
                    "*.3ds",
                    SearchOption.AllDirectories));

            Assert.Empty(
                Directory.EnumerateFiles(
                    root,
                    "*.map.txt",
                    SearchOption.AllDirectories));
        }
        finally
        {
            DeleteRoot(
                root);
        }
    }

    [Fact]
    public async Task AnalyzerBlocksMissingSplineBeforeExport()
    {
        var root =
            CreateRoot();

        var mapDirectory =
            CreateSingleTileMap(
                root,
                @"Splines\Missing\road.sli");

        try
        {
            var result =
                await new ProtonBusOmsiDirectoryPreflightAnalyzer()
                    .AnalyzeAsync(
                        root,
                        mapDirectory,
                        new(
                            "Mapa",
                            "Mapa",
                            "Rota"));

            Assert.False(
                result.CanExport);

            Assert.True(
                result.ErrorCount >
                0);

            Assert.Contains(
                result.Issues,
                issue =>
                    issue.Severity ==
                        ProtonBusOmsiPreflightSeverity.Error &&
                    issue.Code ==
                        "splineMissing");

            Assert.Empty(
                Directory.EnumerateFiles(
                    root,
                    "*.3ds",
                    SearchOption.AllDirectories));
        }
        finally
        {
            DeleteRoot(
                root);
        }
    }

    [Fact]
    public async Task AnalyzerIncludesMapDefinitionValidation()
    {
        var root =
            CreateRoot();

        var mapDirectory =
            CreateSingleTileMap(
                root,
                @"Splines\Test\road.sli");

        try
        {
            CreateSplineFixture(
                root,
                @"Splines\Test\road.sli");

            var result =
                await new ProtonBusOmsiDirectoryPreflightAnalyzer()
                    .AnalyzeAsync(
                        root,
                        mapDirectory,
                        new(
                            "../Mapa",
                            "Mapa",
                            "Rota"));

            Assert.False(
                result.CanExport);

            Assert.Contains(
                result.Issues,
                issue =>
                    issue.Severity ==
                        ProtonBusOmsiPreflightSeverity.Error &&
                    issue.Code ==
                        "PBMAP_MAP_NAME_NOT_RELATIVE");
        }
        finally
        {
            DeleteRoot(
                root);
        }
    }

    [Fact]
    public async Task AnalyzerDetectsCrossTileTextureCollision()
    {
        var root =
            CreateRoot();

        var mapDirectory =
            Path.Combine(
                root,
                "maps",
                "Collision");

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
                        "Collision",
                        "[map]",
                        "0",
                        "0",
                        "tile_0_0.map",
                        "[map]",
                        "1",
                        "0",
                        "tile_1_0.map"
                    ]));

            await File.WriteAllTextAsync(
                Path.Combine(
                    mapDirectory,
                    "tile_0_0.map"),
                CreateTileText(
                    @"Splines\PackA\road.sli",
                    1));

            await File.WriteAllTextAsync(
                Path.Combine(
                    mapDirectory,
                    "tile_1_0.map"),
                CreateTileText(
                    @"Splines\PackB\road.sli",
                    2));

            CreateSplineFixture(
                root,
                @"Splines\PackA\road.sli");

            CreateSplineFixture(
                root,
                @"Splines\PackB\road.sli");

            var result =
                await new ProtonBusOmsiDirectoryPreflightAnalyzer()
                    .AnalyzeAsync(
                        root,
                        mapDirectory,
                        new(
                            "Mapa",
                            "Mapa",
                            "Rota"));

            Assert.False(
                result.CanExport);

            Assert.Contains(
                result.Issues,
                issue =>
                    issue.Code ==
                    "textureTargetCollisionAcrossTiles");
        }
        finally
        {
            DeleteRoot(
                root);
        }
    }

    private static string CreateSingleTileMap(
        string root,
        string splinePath)
    {
        var mapDirectory =
            Path.Combine(
                root,
                "maps",
                "TestMap");

        Directory.CreateDirectory(
            mapDirectory);

        File.WriteAllText(
            Path.Combine(
                mapDirectory,
                "global.cfg"),
            string.Join(
                Environment.NewLine,
                [
                    "[name]",
                    "Test Map",
                    "[map]",
                    "0",
                    "0",
                    "tile_0_0.map"
                ]));

        File.WriteAllText(
            Path.Combine(
                mapDirectory,
                "tile_0_0.map"),
            CreateTileText(
                splinePath,
                1));

        return mapDirectory;
    }

    private static string CreateTileText(
        string splinePath,
        int splineId) =>
        string.Join(
            Environment.NewLine,
            [
                "[version]",
                "14",
                "[spline]",
                "0",
                splinePath,
                splineId.ToString(
                    System.Globalization
                        .CultureInfo.InvariantCulture),
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
            ]);

    private static void CreateSplineFixture(
        string root,
        string relativeSplinePath)
    {
        var normalized =
            relativeSplinePath
                .Replace(
                    '\\',
                    Path.DirectorySeparatorChar)
                .Replace(
                    '/',
                    Path.DirectorySeparatorChar);

        var splinePath =
            Path.Combine(
                root,
                normalized);

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
                Path.GetDirectoryName(
                    splinePath)!,
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
                "MapStudioProtonBusPreflightTests",
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
