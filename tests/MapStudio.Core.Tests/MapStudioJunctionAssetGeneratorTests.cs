using MapStudio.Core.Omsi.Junctions;
using MapStudio.Core.Omsi.Models;
using MapStudio.Core.Omsi.Scenery;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioJunctionAssetGeneratorTests
{
    [Fact]
    public void GeometryBuilderCreatesRenderableJunctionSurface()
    {
        var generator =
            new MapStudioJunctionAssetGenerator();

        var geometry =
            generator.BuildGeometry(
                new MapStudioJunctionSpec(
                    "Cross",
                    [
                        new(
                            0,
                            7),
                        new(
                            90,
                            7),
                        new(
                            180,
                            7),
                        new(
                            270,
                            7)
                    ]));

        Assert.True(
            geometry.IsLoaded);

        Assert.Equal(
            1,
            geometry.Materials.Count);

        Assert.Equal(
            24,
            geometry.TriangleMaterials.Length);

        Assert.Equal(
            24 *
                3,
            geometry.Indices.Length);
    }

    [Fact]
    public async Task GeneratorWritesScoO3dAndInternalTrafficPaths()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-Junction-" +
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            Directory.CreateDirectory(
                root);

            var result =
                await new MapStudioJunctionAssetGenerator()
                    .GenerateAsync(
                        root,
                        new MapStudioJunctionSpec(
                            "Cross 4x",
                            [
                                new(
                                    0,
                                    7),
                                new(
                                    90,
                                    7),
                                new(
                                    180,
                                    7),
                                new(
                                    270,
                                    7)
                            ]));

            Assert.True(
                File.Exists(
                    result
                        .SceneryObjectPath));

            Assert.True(
                File.Exists(
                    result.MeshPath));

            Assert.Equal(
                12,
                result.InternalPathCount);

            var metadata =
                await new OmsiSceneryObjectReader()
                    .ReadMetadataAsync(
                        result
                            .SceneryObjectPath);

            Assert.True(
                metadata.Exists);

            Assert.Equal(
                "Cross 4x",
                metadata.FriendlyName);

            Assert.Single(
                metadata.MeshPaths);

            Assert.Equal(
                12,
                metadata.Paths.Count);

            Assert.All(
                metadata.Paths,
                path =>
                {
                    Assert.Equal(
                        0,
                        path.Type);

                    Assert.True(
                        path.Length >
                        0);

                    Assert.InRange(
                        path.Width,
                        2.5,
                        4.5);
                });

            var mesh =
                new OmsiO3dGeometryReader()
                    .Read(
                        result.MeshPath);

            Assert.True(
                mesh.IsLoaded);

            Assert.True(
                mesh.Indices.Length >
                0);
        }
        finally
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
    }
}
