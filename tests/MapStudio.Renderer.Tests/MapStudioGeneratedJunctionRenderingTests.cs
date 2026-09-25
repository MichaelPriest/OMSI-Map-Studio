using MapStudio.Core.Omsi.Junctions;
using MapStudio.Renderer.Scene;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class MapStudioGeneratedJunctionRenderingTests
{
    [Fact]
    public async Task GeneratedJunctionLoadsItsOwnTextureAndMesh()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-GeneratedJunction-Render-" +
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
                            "RenderCross",
                            [
                                new(
                                    0,
                                    11,
                                    2,
                                    false,
                                    3.5),
                                new(
                                    90,
                                    8.5,
                                    2,
                                    false,
                                    2.75),
                                new(
                                    180,
                                    11,
                                    2,
                                    false,
                                    3.5),
                                new(
                                    270,
                                    8.5,
                                    2,
                                    false,
                                    2.75)
                            ]));

            var sceneryRoot =
                Path.Combine(
                    root,
                    "Sceneryobjects");

            var relativePath =
                Path.GetRelativePath(
                    sceneryRoot,
                    result.SceneryObjectPath)
                    .Replace(
                        Path.DirectorySeparatorChar,
                        '\\');

            var asset =
                await new NativeSceneryAssetLoader()
                    .LoadAssetAsync(
                        root,
                        relativePath);

            Assert.True(
                asset.IsLoaded,
                asset.ErrorCode);

            var mesh =
                Assert.Single(
                    asset.Meshes);

            Assert.True(
                mesh.Geometry.IsLoaded);

            var texture =
                Assert.Single(
                    mesh.MaterialTexturePaths);

            Assert.False(
                string.IsNullOrWhiteSpace(
                    texture));

            Assert.True(
                File.Exists(
                    texture!));
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
