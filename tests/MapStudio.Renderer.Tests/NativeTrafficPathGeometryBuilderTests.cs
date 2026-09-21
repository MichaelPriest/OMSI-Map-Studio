using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Splines;
using MapStudio.Core.Omsi.Scenery;
using MapStudio.Renderer.Picking;
using MapStudio.Renderer.Scene;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeTrafficPathGeometryBuilderTests
{
    [Fact]
    public void BuildTransformsSplinePathWithWidthAndDirection()
    {
        var tile =
            new OmsiTileReference(
                0,
                0,
                "tile_0_0.map");

        var spline =
            new OmsiPlacedSpline(
                "0",
                @"Splines\Roads\street.sli",
                10,
                -1,
                -1,
                100,
                0,
                100,
                0,
                20,
                0,
                0,
                0,
                false,
                []);

        var scene =
            new NativeSceneBuilder()
                .Build(
                    [
                        new NativeSceneTile(
                            tile,
                            new OmsiTileContent(
                                new OmsiTileSummary(
                                    true,
                                    0,
                                    1,
                                    0),
                                [],
                                [spline]))
                    ],
                    new PickingRegistry<object>());

        var definition =
            new OmsiSplineDefinition(
                true,
                [],
                [])
            {
                Paths =
                    [
                        new OmsiSplinePathDefinition(
                            0,
                            -1.5,
                            0.1,
                            3.0,
                            0)
                    ]
            };

        var asset =
            new NativeSplineAsset(
                spline.SplinePath,
                @"C:\OMSI\Splines\Roads\street.sli",
                definition,
                [],
                "noRenderableProfile");

        var result =
            new NativeTrafficPathGeometryBuilder()
                .Build(
                    scene,
                    new Dictionary<
                        string,
                        NativeSplineAsset>(
                            StringComparer.OrdinalIgnoreCase)
                    {
                        [spline.SplinePath] =
                            asset
                    });

        Assert.Equal(
            1,
            result.PathCount);

        Assert.True(
            result.LineCount >
                6);

        Assert.Contains(
            result.Vertices,
            vertex =>
                Math.Abs(
                    vertex.Position.X -
                    98.5f) <
                0.01f);

        Assert.All(
            result.Vertices,
            vertex =>
                Assert.True(
                    vertex.Position.Y >
                    0.1f));
    }
    [Fact]
    public void BuildAddsCrossingPathsFromSceneryObjects()
    {
        var tile =
            new OmsiTileReference(
                0,
                0,
                "tile_0_0.map");

        var placed =
            new OmsiPlacedObject(
                "0",
                @"Sceneryobjects\Crossings\x.sco",
                50,
                10,
                20,
                0,
                0,
                0,
                0,
                []);

        var scene =
            new NativeSceneBuilder()
                .Build(
                    [
                        new NativeSceneTile(
                            tile,
                            new OmsiTileContent(
                                new OmsiTileSummary(
                                    true,
                                    1,
                                    0,
                                    0),
                                [placed],
                                []))
                    ],
                    new PickingRegistry<object>());

        var asset =
            new NativeSceneryAsset(
                placed.SceneryObjectPath,
                @"C:\OMSI\Sceneryobjects\Crossings\x.sco",
                [],
                null,
                false,
                null)
            {
                Paths =
                    [
                        new OmsiSceneryPathDefinition(
                            0,
                            0,
                            0,
                            0,
                            0,
                            10,
                            0,
                            0,
                            0,
                            3,
                            0,
                            0,
                            1,
                            null,
                            false)
                    ]
            };

        var result =
            new NativeTrafficPathGeometryBuilder()
                .Build(
                    scene,
                    new Dictionary<
                        string,
                        NativeSplineAsset>(
                            StringComparer.OrdinalIgnoreCase),
                    new Dictionary<
                        string,
                        NativeSceneryAsset>(
                            StringComparer.OrdinalIgnoreCase)
                    {
                        [placed.SceneryObjectPath] =
                            asset
                    });

        Assert.Equal(
            1,
            result.PathCount);

        Assert.True(
            result.LineCount >
            6);

        Assert.Contains(
            result.Vertices,
            vertex =>
                vertex.Position.X >
                    9.8f &&
                vertex.Position.X <
                    10.2f &&
                vertex.Position.Z >
                    19.8f);
    }

}
