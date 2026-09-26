using System.Text;
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
    public async Task ExporterWritesAutomaticVehiclePathAnd3dMarkers()
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
                ],
                includeVehiclePath:
                    true);

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
                            @"Splines\Test\road.sli"),
                        new()
                        {
                            FunctionalOptions =
                                new(
                                    WaypointSpacing:
                                        5)
                        });

            Assert.True(
                result.IsExported);

            Assert.NotNull(
                result.Functional);

            var vehicle =
                Assert.Single(
                    result.Functional!
                        .VehiclePaths);

            Assert.Equal(
                "pv_t0_0_s1_p0_f",
                vehicle.Prefix);

            Assert.Equal(
                3,
                vehicle.MaxPathsToCheck);

            var pathFile =
                Assert.Single(
                    result.Package!
                        .VehiclePathPaths);

            Assert.True(
                File.Exists(
                    pathFile));

            Assert.Contains(
                "prefix=pv_t0_0_s1_p0_f",
                File.ReadAllText(
                    pathFile),
                StringComparison.Ordinal);

            var modelPath =
                Assert.Single(
                    result.Package
                        .ModelPaths);

            var modelBytes =
                File.ReadAllBytes(
                    modelPath);

            var markerName =
                Encoding.ASCII
                    .GetBytes(
                        "pv_t0_0_s1_p0_f.000\0");

            Assert.True(
                modelBytes
                    .AsSpan()
                    .IndexOf(
                        markerName) >=
                0);

            Assert.Equal(
                3,
                result.Functional
                    .MarkerMeshCount);
        }
        finally
        {
            DeleteRoot(
                root);
        }
    }

    [Fact]
    public async Task ExporterTranscodesDdsTextureToPng()
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
                CreateDxt1Dds());

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

            var texturePath =
                Assert.Single(
                    result.Package!
                        .TexturePaths);

            Assert.EndsWith(
                "road.png",
                texturePath,
                StringComparison
                    .OrdinalIgnoreCase);

            var bytes =
                File.ReadAllBytes(
                    texturePath);

            Assert.True(
                bytes.Length >
                8);

            Assert.Equal(
                new byte[]
                {
                    137,
                    80,
                    78,
                    71,
                    13,
                    10,
                    26,
                    10
                },
                bytes.Take(8)
                    .ToArray());
        }
        finally
        {
            DeleteRoot(
                root);
        }
    }


    private static byte[] CreateDxt1Dds()
    {
        using var stream =
            new MemoryStream();

        using var writer =
            new BinaryWriter(
                stream);

        writer.Write(
            new byte[]
            {
                68,
                68,
                83,
                32
            });

        writer.Write(
            124u);

        writer.Write(
            0x00081007u);

        writer.Write(
            4u);

        writer.Write(
            4u);

        writer.Write(
            8u);

        writer.Write(
            0u);

        writer.Write(
            0u);

        for (
            var index = 0;
            index <
            11;
            index++)
        {
            writer.Write(
                0u);
        }

        writer.Write(
            32u);

        writer.Write(
            0x00000004u);

        writer.Write(
            0x31545844u);

        writer.Write(
            0u);

        writer.Write(
            0u);

        writer.Write(
            0u);

        writer.Write(
            0u);

        writer.Write(
            0u);

        writer.Write(
            0x00001000u);

        writer.Write(
            0u);

        writer.Write(
            0u);

        writer.Write(
            0u);

        writer.Write(
            0u);

        writer.Write(
            (ushort)0xF800);

        writer.Write(
            (ushort)0x0000);

        writer.Write(
            0u);

        return stream.ToArray();
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
        byte[] textureBytes,
        bool includeVehiclePath = false)
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

        var lines =
            new List<string>
            {
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
            };

        if (includeVehiclePath)
        {
            lines.AddRange(
                [
                    "[path]",
                    "0",
                    "0",
                    "0.1",
                    "3",
                    "0"
                ]);
        }

        File.WriteAllText(
            splinePath,
            string.Join(
                Environment.NewLine,
                lines));

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
