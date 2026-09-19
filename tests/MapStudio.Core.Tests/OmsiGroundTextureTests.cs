using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Textures;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiGroundTextureTests
{
    [Fact]
    public void ReadGroundTextures_ParsesLayerOrderAndRepeating()
    {
        const string source =
            "[groundtex]\n" +
            "texture\\gras.bmp\n" +
            "texture\\gras_det.bmp\n" +
            "0\n" +
            "1\n" +
            "60\n" +
            "[groundtex]\n" +
            "Texture\\str_k_kopfstein.bmp\n" +
            "Texture\\noise_low.bmp\n" +
            "9\n" +
            "20\n" +
            "1\n";

        var document =
            OmsiConfigParser.Parse(
                source);

        var textures =
            OmsiMapCatalog
                .ReadGroundTextures(
                    document);

        Assert.Equal(
            2,
            textures.Count);

        Assert.Equal(
            new OmsiGroundTexture(
                "texture\\gras.bmp",
                "texture\\gras_det.bmp",
                0,
                1,
                60),
            textures[0]);

        Assert.Equal(
            new OmsiGroundTexture(
                "Texture\\str_k_kopfstein.bmp",
                "Texture\\noise_low.bmp",
                9,
                20,
                1),
            textures[1]);
    }

    [Fact]
    public void ReadGroundTextures_SkipsMalformedLayer()
    {
        const string source =
            "[groundtex]\n" +
            "texture\\broken.bmp\n" +
            "texture\\detail.bmp\n" +
            "0\n" +
            "0\n" +
            "60\n";

        var document =
            OmsiConfigParser.Parse(
                source);

        Assert.Empty(
            OmsiMapCatalog
                .ReadGroundTextures(
                    document));
    }

    [Fact]
    public void GroundTextureResolver_UsesMapAndOmsiRoots()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-GroundTextureTests",
                Guid.NewGuid()
                    .ToString("N"));

        var mapDirectory =
            Path.Combine(
                root,
                "maps",
                "Map");

        try
        {
            var mapTexture =
                Path.Combine(
                    mapDirectory,
                    "texture",
                    "grass.bmp");

            var sharedTexture =
                Path.Combine(
                    root,
                    "Sceneryobjects",
                    "Pack",
                    "texture",
                    "sand.dds");

            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    mapTexture)!);

            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    sharedTexture)!);

            File.WriteAllBytes(
                mapTexture,
                [1, 2, 3]);

            File.WriteAllBytes(
                sharedTexture,
                [4, 5, 6]);

            Assert.True(
                OmsiTextureAssetPathResolver
                    .TryResolveGroundTexture(
                        root,
                        mapDirectory,
                        @"texture\grass.bmp",
                        out var resolvedMap));

            Assert.Equal(
                Path.GetFullPath(
                    mapTexture),
                resolvedMap);

            Assert.True(
                OmsiTextureAssetPathResolver
                    .TryResolveGroundTexture(
                        root,
                        mapDirectory,
                        @"Sceneryobjects\Pack\texture\sand.dds",
                        out var resolvedShared));

            Assert.Equal(
                Path.GetFullPath(
                    sharedTexture),
                resolvedShared);
        }
        finally
        {
            Directory.Delete(
                root,
                recursive: true);
        }
    }

    [Fact]
    public void GroundTextureResolver_RejectsTraversal()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-GroundTextureTests",
                Guid.NewGuid()
                    .ToString("N"));

        var mapDirectory =
            Path.Combine(
                root,
                "maps",
                "Map");

        try
        {
            Directory.CreateDirectory(
                mapDirectory);

            Assert.False(
                OmsiTextureAssetPathResolver
                    .TryResolveGroundTexture(
                        root,
                        mapDirectory,
                        @"..\..\..\outside.bmp",
                        out _));
        }
        finally
        {
            Directory.Delete(
                root,
                recursive: true);
        }
    }
}
