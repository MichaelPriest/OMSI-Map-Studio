using MapStudio.Core.Omsi.Textures;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiTextureAssetPathResolverTests
{
    [Fact]
    public void ResolvesSceneryTextureInsidePack()
    {
        var root =
            CreateTempRoot();

        try
        {
            var objectDirectory =
                Path.Combine(
                    root,
                    "Sceneryobjects",
                    "Pack");

            var modelDirectory =
                Path.Combine(
                    objectDirectory,
                    "model");

            var textureDirectory =
                Path.Combine(
                    objectDirectory,
                    "Texture");

            Directory.CreateDirectory(
                modelDirectory);
            Directory.CreateDirectory(
                textureDirectory);

            var objectPath =
                Path.Combine(
                    objectDirectory,
                    "house.sco");

            var meshPath =
                Path.Combine(
                    modelDirectory,
                    "house.o3d");

            var texturePath =
                Path.Combine(
                    textureDirectory,
                    "wall.dds");

            File.WriteAllText(
                objectPath,
                string.Empty);
            File.WriteAllText(
                meshPath,
                string.Empty);
            File.WriteAllBytes(
                texturePath,
                [1, 2, 3]);

            Assert.True(
                OmsiTextureAssetPathResolver
                    .TryResolveSceneryTexture(
                        root,
                        objectPath,
                        meshPath,
                        "wall.dds",
                        out var resolved));

            Assert.Equal(
                Path.GetFullPath(
                    texturePath),
                resolved);
        }
        finally
        {
            Directory.Delete(
                root,
                recursive: true);
        }
    }

    [Fact]
    public void ResolvesSplineTextureInsidePack()
    {
        var root =
            CreateTempRoot();

        try
        {
            var splineDirectory =
                Path.Combine(
                    root,
                    "Splines",
                    "Pack");

            var textureDirectory =
                Path.Combine(
                    splineDirectory,
                    "Texture");

            Directory.CreateDirectory(
                textureDirectory);

            var splinePath =
                Path.Combine(
                    splineDirectory,
                    "road.sli");

            var texturePath =
                Path.Combine(
                    textureDirectory,
                    "road.tga");

            File.WriteAllText(
                splinePath,
                string.Empty);
            File.WriteAllBytes(
                texturePath,
                [1, 2, 3]);

            Assert.True(
                OmsiTextureAssetPathResolver
                    .TryResolveSplineTexture(
                        root,
                        splinePath,
                        "road.tga",
                        out var resolved));

            Assert.Equal(
                Path.GetFullPath(
                    texturePath),
                resolved);
        }
        finally
        {
            Directory.Delete(
                root,
                recursive: true);
        }
    }

    [Fact]
    public void RejectsTraversalOutsideAllowedRoot()
    {
        var root =
            CreateTempRoot();

        try
        {
            var splineDirectory =
                Path.Combine(
                    root,
                    "Splines",
                    "Pack");

            Directory.CreateDirectory(
                splineDirectory);

            var splinePath =
                Path.Combine(
                    splineDirectory,
                    "road.sli");

            File.WriteAllText(
                splinePath,
                string.Empty);

            var outside =
                Path.Combine(
                    root,
                    "outside.png");

            File.WriteAllBytes(
                outside,
                [1, 2, 3]);

            Assert.False(
                OmsiTextureAssetPathResolver
                    .TryResolveSplineTexture(
                        root,
                        splinePath,
                        @"..\..\outside.png",
                        out _));
        }
        finally
        {
            Directory.Delete(
                root,
                recursive: true);
        }
    }

    private static string CreateTempRoot()
    {
        var path =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-TextureTests",
                Guid.NewGuid()
                    .ToString("N"));

        Directory.CreateDirectory(path);
        return path;
    }
}
