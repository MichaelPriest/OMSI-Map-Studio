using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.ProtonBus;
using Xunit;

namespace MapStudio.ProtonBus.Tests;

public sealed class ProtonBusOmsiTilePackageExporterTests
{
    [Fact]
    public async Task ExporterWritesMapTxt3dsAndPngForReadyTile()
    {
        var root =
            CreateRoot();

        var output =
            Path.Combine(
                root,
                "export");

        try
        {
            CreateSplineFixture(
                root,
                "road.png",
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

            var result =
                await new ProtonBusOmsiTilePackageExporter()
                    .ExportAsync(
                        root,
                        output,
                        new(
                            "Mapa",
                            "Mapa",
                            "Rota"),
                        new(
                            0,
                            0,
                            "tile_0_0.map"),
                        CreateContent(
                            @"Splines\Test\road.sli"));

            Assert.True(
                result.IsExported);

            Assert.NotNull(
                result.Package);

            Assert.NotNull(
                result.Tile);

            Assert.Empty(
                result.Issues);

            Assert.EndsWith(
                Path.Combine(
                    "maps",
                    "Mapa.map.txt"),
                result.Package!
                    .MapDefinitionPath,
                StringComparison
                    .OrdinalIgnoreCase);

            Assert.True(
                File.Exists(
                    result.Package
                        .MapDefinitionPath));

            Assert.True(
                File.Exists(
                    Assert.Single(
                        result.Package
                            .ModelPaths)));

            Assert.True(
                File.Exists(
                    Assert.Single(
                        result.Package
                            .TexturePaths)));

            Assert.Equal(
                1,
                result.Tile!
                    .SplineMeshCount);
        }
        finally
        {
            DeleteRoot(
                root);
        }
    }

    [Fact]
    public async Task ExporterStopsBeforeWritingWhenTextureNeedsConversion()
    {
        var root =
            CreateRoot();

        var output =
            Path.Combine(
                root,
                "export");

        try
        {
            CreateSplineFixture(
                root,
                "road.dds",
                [
                    68,
                    68,
                    83,
                    32
                ]);

            var result =
                await new ProtonBusOmsiTilePackageExporter()
                    .ExportAsync(
                        root,
                        output,
                        new(
                            "Mapa",
                            "Mapa",
                            "Rota"),
                        new(
                            0,
                            0,
                            "tile_0_0.map"),
                        CreateContent(
                            @"Splines\Test\road.sli"));

            Assert.False(
                result.IsExported);

            Assert.Null(
                result.Package);

            Assert.Null(
                result.Tile);

            Assert.Contains(
                result.Issues,
                issue =>
                    issue.Code ==
                    "textureConversionRequired");

            Assert.False(
                File.Exists(
                    Path.Combine(
                        output,
                        "maps",
                        "Mapa.map.txt")));
        }
        finally
        {
            DeleteRoot(
                root);
        }
    }

    private static OmsiTileContent
        CreateContent(
            string splinePath) =>
        new(
            new(
                true,
                ObjectCount: 0,
                SplineCount: 1,
                SplineAttachmentCount: 0),
            [],
            [
                new(
                    HeaderValue:
                        "spline",
                    SplinePath:
                        splinePath,
                    SplineId: 1,
                    PreviousSplineId:
                        -1,
                    NextSplineId:
                        -1,
                    X: 0,
                    Z: 0,
                    Y: 0,
                    Rotation: 0,
                    Length: 10,
                    Radius: 0,
                    GradientStart: 0,
                    GradientEnd: 0,
                    IsHeightSpline:
                        false,
                    ExtraValues: [])
            ]);

    private static void CreateSplineFixture(
        string root,
        string textureName,
        byte[] textureBytes)
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
                    textureName,
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
                    "1"
                ]));

        var texturePath =
            Path.Combine(
                root,
                "Splines",
                "Test",
                "Texture",
                textureName);

        Directory.CreateDirectory(
            Path.GetDirectoryName(
                texturePath)!);

        File.WriteAllBytes(
            texturePath,
            textureBytes);
    }

    private static string CreateRoot()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudioProtonBusExporterTests",
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
                recursive: true);
        }
    }
}
