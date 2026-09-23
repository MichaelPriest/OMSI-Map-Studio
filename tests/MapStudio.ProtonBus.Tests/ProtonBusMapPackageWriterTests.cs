using System.Buffers.Binary;
using System.Numerics;
using MapStudio.Core.ProtonBus;
using Xunit;

namespace MapStudio.ProtonBus.Tests;

public sealed class ProtonBusMapPackageWriterTests
{
    [Fact]
    public void WriterCreatesManifestModelFoldersAndTexture()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudioProtonBusTests",
                Guid.NewGuid()
                    .ToString("N"));

        var sourceTexture =
            Path.Combine(
                root,
                "source.png");

        try
        {
            Directory.CreateDirectory(
                root);

            File.WriteAllBytes(
                sourceTexture,
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

            var definition =
                new ProtonBusMapDefinition(
                    "Cidade Teste",
                    "Cidade Teste",
                    "Rota 1");

            var result =
                ProtonBusMapPackageWriter
                    .Write(
                        root,
                        new(
                            definition,
                            [
                                new(
                                    "cenario.3ds",
                                    CreateScene())
                            ],
                            [
                                new(
                                    sourceTexture,
                                    "asfalto.png")
                            ]));

            Assert.True(
                File.Exists(
                    result.MapDefinitionPath));

            Assert.Contains(
                "mapModVersion=3",
                File.ReadAllText(
                    result.MapDefinitionPath),
                StringComparison.Ordinal);

            var modelPath =
                Assert.Single(
                    result.ModelPaths);

            Assert.True(
                File.Exists(
                    modelPath));

            var bytes =
                File.ReadAllBytes(
                    modelPath);

            Assert.Equal(
                0x4D4D,
                BinaryPrimitives
                    .ReadUInt16LittleEndian(
                        bytes.AsSpan(
                            0,
                            2)));

            var texturePath =
                Assert.Single(
                    result.TexturePaths);

            Assert.True(
                File.Exists(
                    texturePath));

            var layout =
                ProtonBusMapPackageLayout
                    .From(
                        definition);

            Assert.True(
                Directory.Exists(
                    Path.Combine(
                        root,
                        layout
                            .BusStopsDirectoryPath
                            .Replace(
                                '/',
                                Path.DirectorySeparatorChar))));

            Assert.True(
                Directory.Exists(
                    Path.Combine(
                        root,
                        layout
                            .TrafficLightsDirectoryPath
                            .Replace(
                                '/',
                                Path.DirectorySeparatorChar))));
        }
        finally
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

    [Fact]
    public void WriterRejectsTextureThatIsNotPng()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudioProtonBusTests",
                Guid.NewGuid()
                    .ToString("N"));

        var source =
            Path.Combine(
                root,
                "source.dds");

        try
        {
            Directory.CreateDirectory(
                root);

            File.WriteAllBytes(
                source,
                [1, 2, 3]);

            Assert.Throws<
                ArgumentException>(
                    () =>
                        ProtonBusMapPackageWriter
                            .Write(
                                root,
                                new(
                                    new(
                                        "Mapa",
                                        "Mapa",
                                        "Rota"),
                                    [],
                                    [
                                        new(
                                            source,
                                            "source.dds")
                                    ])));
        }
        finally
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

    [Fact]
    public void WriterRejectsNonPngSourceEvenWithPngTargetName()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudioProtonBusTests",
                Guid.NewGuid()
                    .ToString("N"));

        var source =
            Path.Combine(
                root,
                "source.dds");

        try
        {
            Directory.CreateDirectory(
                root);

            File.WriteAllBytes(
                source,
                [68, 68, 83, 32]);

            var error =
                Assert.Throws<
                    ArgumentException>(
                        () =>
                            ProtonBusMapPackageWriter
                                .Write(
                                    root,
                                    new(
                                        new(
                                            "Mapa",
                                            "Mapa",
                                            "Rota"),
                                        [],
                                        [
                                            new(
                                                source,
                                                "source.png")
                                        ])));

            Assert.Contains(
                "valid PNG",
                error.Message,
                StringComparison.Ordinal);
        }
        finally
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

    [Fact]
    public void WriterRejectsModelPathTraversal()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudioProtonBusTests",
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            Assert.Throws<
                ArgumentException>(
                    () =>
                        ProtonBusMapPackageWriter
                            .Write(
                                root,
                                new(
                                    new(
                                        "Mapa",
                                        "Mapa",
                                        "Rota"),
                                    [
                                        new(
                                            @"..\escape.3ds",
                                            CreateScene())
                                    ])));
        }
        finally
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

    private static ProtonBusExportScene
        CreateScene() =>
        new(
            [
                new(
                    "triangle_gencol_",
                    [
                        new(
                            new Vector3(
                                0,
                                0,
                                0),
                            Vector2.Zero),
                        new(
                            new Vector3(
                                1,
                                0,
                                0),
                            Vector2.UnitX),
                        new(
                            new Vector3(
                                0,
                                0,
                                1),
                            Vector2.UnitY)
                    ],
                    [
                        new(
                            0,
                            1,
                            2,
                            "ground")
                    ],
                    [
                        new(
                            "ground",
                            "asfalto.png")
                    ])
            ]);
}
