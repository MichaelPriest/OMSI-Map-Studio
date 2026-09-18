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
