using System.Text;
using MapStudio.Core.Omsi.Splines;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiSplineDefinitionTests
{
    [Fact]
    public async Task Reader_ExtractsTexturesAndProfileSurface()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"mapstudio-spline-{Guid.NewGuid():N}.sli");

        try
        {
            const string source =
                "[texture]\r\n" +
                "Asphalt.bmp\r\n" +
                "[matl]\r\n" +
                "Asphalt.bmp\r\n" +
                "0\r\n" +
                "[profile]\r\n" +
                "0\r\n" +
                "[profilepnt]\r\n" +
                "-4\r\n" +
                "0.1\r\n" +
                "0.005\r\n" +
                "0.2\r\n" +
                "[profilepnt]\r\n" +
                "4\r\n" +
                "0.1\r\n" +
                "0.995\r\n" +
                "0.2\r\n";

            await File.WriteAllTextAsync(
                path,
                source,
                new UTF8Encoding(false));

            var definition =
                await new OmsiSplineDefinitionReader()
                    .ReadAsync(path);

            Assert.True(definition.Exists);

            Assert.Equal(
                "Asphalt.bmp",
                Assert.Single(
                    definition.Textures));

            var surface =
                Assert.Single(
                    definition.Surfaces);

            Assert.Equal(
                0,
                surface.TextureIndex);

            Assert.Equal(
                "Asphalt.bmp",
                surface.TextureName);

            Assert.Equal(
                0,
                surface.AlphaMode);

            Assert.Equal(
                -4,
                surface.From.X);

            Assert.Equal(
                0.1,
                surface.From.Z);

            Assert.Equal(
                0.005,
                surface.From.TextureX);

            Assert.Equal(
                4,
                surface.To.X);

            Assert.Equal(
                0.995,
                surface.To.TextureX);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Reader_AssignsExplicitMaterialAlphaToTextureSurfaces()
    {
        const string source =
            "[texture]\n" +
            "Marking.tga\n" +
            "[matl_alpha]\n" +
            "1\n" +
            "[profile]\n" +
            "0\n" +
            "[profilepnt]\n" +
            "-0.1\n0.1\n0\n1\n" +
            "[profilepnt]\n" +
            "0.1\n0.1\n1\n1\n";

        var definition =
            new OmsiSplineDefinitionReader()
                .Read(
                    MapStudio.Core.Omsi.Config
                        .OmsiConfigParser.Parse(
                            source));

        Assert.Equal(
            1,
            Assert.Single(
                definition.Surfaces)
                .AlphaMode);
    }

    [Fact]
    public void Reader_ExpandsMultiPointProfileIntoAllAdjacentSurfaces()
    {
        const string source =
            "[texture]\n" +
            "Road.bmp\n" +
            "[profile]\n" +
            "0\n" +
            "[profilepnt]\n" +
            "-5\n0\n0\n0.25\n" +
            "[profilepnt]\n" +
            "0\n0.1\n0.5\n0.25\n" +
            "[profilepnt]\n" +
            "5\n0\n1\n0.25\n";

        var definition =
            new OmsiSplineDefinitionReader()
                .Read(
                    MapStudio.Core.Omsi.Config
                        .OmsiConfigParser.Parse(
                            source));

        Assert.Equal(
            2,
            definition.Surfaces.Count);

        Assert.Equal(
            -5,
            definition.Surfaces[0]
                .From.X);
        Assert.Equal(
            0,
            definition.Surfaces[0]
                .To.X);
        Assert.Equal(
            0,
            definition.Surfaces[1]
                .From.X);
        Assert.Equal(
            5,
            definition.Surfaces[1]
                .To.X);
    }

    [Fact]
    public void PathResolver_StaysInsideOmsiSplines()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "OMSI 2");

        Assert.True(
            OmsiSplinePathResolver.TryResolve(
                root,
                @"Splines\Roads\street.sli",
                out var fullPath));

        Assert.EndsWith(
            Path.Combine(
                "Splines",
                "Roads",
                "street.sli"),
            fullPath,
            StringComparison.OrdinalIgnoreCase);

        Assert.False(
            OmsiSplinePathResolver.TryResolve(
                root,
                @"Splines\..\maps\global.cfg",
                out _));

        Assert.False(
            OmsiSplinePathResolver.TryResolve(
                root,
                @"C:\outside\street.sli",
                out _));
    }
}
