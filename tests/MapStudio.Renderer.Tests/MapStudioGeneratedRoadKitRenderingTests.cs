using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Splines;
using MapStudio.Renderer.Picking;
using MapStudio.Renderer.Scene;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class MapStudioGeneratedRoadKitRenderingTests
{
    [Fact]
    public async Task GeneratedRoadKitSplineLoadsAndBuildsRenderableTriangles()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-GeneratedRoadKit-Render-" +
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            Directory.CreateDirectory(
                root);

            await new MapStudioRoadKitGenerator()
                .InstallOrUpdateAsync(
                    root);

            var profile =
                MapStudioStandardRoadCatalog
                    .LocalWithSidewalk;

            var reference =
                new OmsiTileReference(
                    0,
                    0,
                    "tile_0_0.map");

            var spline =
                new OmsiPlacedSpline(
                    "0",
                    profile.RelativePath,
                    1,
                    -1,
                    -1,
                    20,
                    0,
                    30,
                    90,
                    80,
                    0,
                    0,
                    0,
                    false,
                    []);

            var scene =
                new NativeSceneSnapshot(
                    [
                        new NativeSceneTile(
                            reference,
                            new OmsiTileContent(
                                new OmsiTileSummary(
                                    true,
                                    0,
                                    1,
                                    0),
                                [],
                                [
                                    spline
                                ]))
                    ],
                    [],
                    [
                        new NativeSplineEntity(
                            new PickingId(
                                PickingKind.Spline,
                                1),
                            reference,
                            spline,
                            20,
                            0,
                            30)
                    ],
                    []);

            var assets =
                await new NativeSplineAssetLoader()
                    .LoadAsync(
                        root,
                        scene);

            var asset =
                Assert.Single(
                    assets)
                    .Value;

            Assert.True(
                asset.IsLoaded,
                asset.ErrorCode);

            Assert.Equal(
                profile.RelativePath,
                asset.DeclaredPath);

            Assert.NotEmpty(
                asset.Definition
                    .Surfaces);

            Assert.Contains(
                asset.TexturePaths,
                path =>
                    path is not null &&
                    File.Exists(
                        path));

            var geometry =
                new NativeSplineTriangleGeometryBuilder()
                    .Build(
                        scene,
                        assets);

            Assert.Equal(
                1,
                geometry.LoadedSplineCount);

            Assert.True(
                geometry.RenderedSurfaceCount >
                0);

            Assert.True(
                geometry.Vertices.Length >
                0);

            Assert.True(
                geometry.TriangleCount >
                0);

            Assert.True(
                geometry.TexturedBatchCount >
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
