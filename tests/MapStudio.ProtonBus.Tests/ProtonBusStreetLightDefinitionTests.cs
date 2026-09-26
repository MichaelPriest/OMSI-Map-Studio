using System.Numerics;
using MapStudio.Core.ProtonBus;
using Xunit;

namespace MapStudio.ProtonBus.Tests;

public sealed class ProtonBusStreetLightDefinitionTests
{
    [Fact]
    public void WriterSerializesRealAndFakeLight()
    {
        var definition =
            CreateDefinition();

        var text =
            ProtonBusStreetLightDefinitionWriter
                .Serialize(definition);

        Assert.Contains(
            "[streetlight]",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "prefix=luz001",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "alwaysOn=1",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "[real]",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "colorR=1",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "type=point",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "range=20",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "intensity=0.8",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "[fake]",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "shader=additive",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "texture=light_glow.png",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "alwaysFaceCamera=1",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "colorA=0.5",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "texScaleX=10",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "rotX=0",
            text,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DefinitionBuildsDocumentedMarkerNames()
    {
        var definition =
            CreateDefinition();

        Assert.Equal(
            "_luz001_real_",
            definition.RealObjectName);

        Assert.Equal(
            "_luz001_fake_",
            definition.FakeObjectName);

        Assert.Equal(
            "luz001.txt",
            definition.SuggestedFileName);
    }

    [Fact]
    public void WriterRequiresAtLeastOneLightMode()
    {
        Assert.Throws<
            ArgumentException>(
                () =>
                    ProtonBusStreetLightDefinitionWriter
                        .Serialize(
                            new(
                                "luz001")));
    }

    [Fact]
    public void WriterRejectsNonPngFakeTexture()
    {
        Assert.Throws<
            ArgumentException>(
                () =>
                    ProtonBusStreetLightDefinitionWriter
                        .Serialize(
                            new(
                                "luz001",
                                Fake:
                                    new(
                                        "glow.dds"))));
    }

    [Fact]
    public void PackageWriterPlacesStreetLightUnderStreetlights()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudioProtonBusStreetLightTests",
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            var result =
                ProtonBusMapPackageWriter
                    .Write(
                        root,
                        new(
                            new(
                                "Mapa",
                                "Mapa",
                                "Rota"),
                            [])
                        {
                            StreetLights =
                            [
                                CreateDefinition()
                            ]
                        });

            var path =
                Assert.Single(
                    result.StreetLightPaths);

            Assert.True(
                File.Exists(path));

            Assert.EndsWith(
                Path.Combine(
                    "maps",
                    "Mapa",
                    "tiles",
                    "Rota",
                    "streetlights",
                    "luz001.txt"),
                path,
                StringComparison
                    .OrdinalIgnoreCase);

            Assert.Contains(
                "[fake]",
                File.ReadAllText(path),
                StringComparison.Ordinal);
        }
        finally
        {
            if (
                Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }

    [Fact]
    public void PackageWriterRejectsDuplicateStreetLightPrefix()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudioProtonBusStreetLightTests",
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
                                    [])
                                {
                                    StreetLights =
                                    [
                                        CreateDefinition(),
                                        CreateDefinition()
                                    ]
                                }));
        }
        finally
        {
            if (
                Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }

    private static ProtonBusStreetLightDefinition
        CreateDefinition() =>
        new(
            Prefix:
                "luz001",
            AlwaysOn:
                true,
            Real:
                new(
                    Color:
                        new Vector3(
                            1,
                            0.85f,
                            0.6f),
                    Range:
                        20,
                    Intensity:
                        0.8),
            Fake:
                new(
                    TextureFileName:
                        "light_glow.png",
                    AlwaysFaceCamera:
                        true,
                    Color:
                        new Vector4(
                            1,
                            0.85f,
                            0.6f,
                            0.5f),
                    TextureScale:
                        new Vector3(
                            10,
                            10,
                            10),
                    RotationDegrees:
                        Vector3.Zero));
}
