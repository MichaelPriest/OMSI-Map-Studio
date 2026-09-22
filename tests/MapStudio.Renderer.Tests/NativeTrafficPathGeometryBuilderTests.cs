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

        Assert.True(
            result.TriangleCount >
                0);

        Assert.Equal(
            0,
            result.TriangleVertices.Length %
                3);

        Assert.Contains(
            result.TriangleVertices,
            vertex =>
                vertex.Color.X >
                    0.90f &&
                vertex.Color.Y <
                    0.20f &&
                vertex.Color.W <
                    1.0f);

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


    [Fact]
    public void BuildCanFilterKindsAndHideWidthEdges()
    {
        var tile =
            new OmsiTileReference(
                0,
                0,
                "tile_0_0.map");

        var spline =
            new OmsiPlacedSpline(
                "0",
                @"Splines\Roads\mixed.sli",
                20,
                -1,
                -1,
                0,
                0,
                0,
                0,
                30,
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
                            0,
                            3.0,
                            0),
                        new OmsiSplinePathDefinition(
                            1,
                            2.0,
                            0,
                            1.5,
                            0),
                        new OmsiSplinePathDefinition(
                            2,
                            0,
                            0,
                            1.0,
                            2)
                    ]
            };

        var asset =
            new NativeSplineAsset(
                spline.SplinePath,
                @"C:\OMSI\Splines\Roads\mixed.sli",
                definition,
                [],
                "noRenderableProfile");

        var clean =
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
                    },
                    displayOptions:
                        NativeTrafficPathDisplayOptions
                            .CleanVehicles);

        var detailed =
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
                    },
                    displayOptions:
                        NativeTrafficPathDisplayOptions
                            .AllDetailed);

        Assert.Equal(
            1,
            clean.PathCount);

        Assert.Equal(
            1,
            clean.VehiclePathCount);

        Assert.Equal(
            0,
            clean.PedestrianPathCount);

        Assert.Equal(
            0,
            clean.RailPathCount);

        Assert.Equal(
            3,
            detailed.PathCount);

        Assert.True(
            clean.LineCount <
                detailed.LineCount);

        Assert.True(
            clean.TriangleCount >
                0);

        Assert.True(
            detailed.TriangleCount >
                clean.TriangleCount);

        Assert.Contains(
            detailed.TriangleVertices,
            vertex =>
                vertex.Color.X >
                    0.90f &&
                vertex.Color.Y >
                    0.90f &&
                vertex.Color.Z >
                    0.90f);

        Assert.Contains(
            detailed.TriangleVertices,
            vertex =>
                vertex.Color.Z >
                    0.90f &&
                vertex.Color.X <
                    0.30f);
    }
    [Fact]
    public void BuildCanFocusOnePathIndex()
    {
        var tile =
            new OmsiTileReference(
                0,
                0,
                "tile_0_0.map");

        var spline =
            new OmsiPlacedSpline(
                "0",
                @"Splines\Roads\lanes.sli",
                30,
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
                            -2.0,
                            0,
                            3.0,
                            0),
                        new OmsiSplinePathDefinition(
                            0,
                            2.0,
                            0,
                            3.0,
                            0)
                    ]
            };

        var asset =
            new NativeSplineAsset(
                spline.SplinePath,
                @"C:\OMSI\Splines\Roads\lanes.sli",
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
                    },
                    displayOptions:
                        NativeTrafficPathDisplayOptions
                            .CleanVehicles,
                    focusedPathIndex:
                        1);

        Assert.Equal(
            1,
            result.PathCount);

        Assert.Equal(
            1,
            result.VehiclePathCount);

        Assert.True(
            result.TriangleCount >
                0);

        Assert.All(
            result.TriangleVertices,
            vertex =>
                Assert.True(
                    vertex.Color.W >
                        0.80f));

        Assert.Contains(
            result.Vertices,
            vertex =>
                Math.Abs(
                    vertex.Position.X -
                    102.0f) <
                0.01f);

        Assert.DoesNotContain(
            result.Vertices,
            vertex =>
                Math.Abs(
                    vertex.Position.X -
                    98.0f) <
                0.01f);
    }

}
