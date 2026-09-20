using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Splines;
using MapStudio.Renderer.Picking;
using MapStudio.Renderer.Scene;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeSplineTriangleGeometryBuilderTests
{
    [Fact]
    public void BuildExtrudesRealSplineProfileInWorldSpace()
    {
        var tile =
            new OmsiTileReference(
                0,
                0,
                "tile_0_0.map");

        var placedSpline =
            new OmsiPlacedSpline(
                "spline",
                @"Splines\Test\road.sli",
                7,
                -1,
                -1,
                10,
                5,
                20,
                0,
                12,
                0,
                0,
                0,
                false,
                Array.Empty<string>());

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
                                [placedSpline]))
                    ],
                    new PickingRegistry<
                        object>());

        var definition =
            new OmsiSplineDefinition(
                true,
                ["road.bmp"],
                [
                    new OmsiSplineSurface(
                        0,
                        "road.bmp",
                        0,
                        new OmsiSplineProfilePoint(
                            -4,
                            0.1,
                            0,
                            0.2),
                        new OmsiSplineProfilePoint(
                            4,
                            0.1,
                            1,
                            0.2))
                ]);

        var assets =
            new Dictionary<
                string,
                NativeSplineAsset>(
                StringComparer.OrdinalIgnoreCase)
            {
                [placedSpline.SplinePath] =
                    new(
                        placedSpline.SplinePath,
                        @"C:\OMSI 2\Splines\Test\road.sli",
                        definition,
                        [@"C:\OMSI 2\Splines\Test\Texture\road.dds"],
                        null)
            };

        var geometry =
            new NativeSplineTriangleGeometryBuilder()
                .Build(
                    scene,
                    assets);

        Assert.Equal(
            6,
            geometry.TriangleCount);

        Assert.Equal(
            1,
            geometry.LoadedSplineCount);

        Assert.Equal(
            1,
            geometry.RenderedSurfaceCount);

        var batch =
            Assert.Single(
                geometry.MaterialBatches);

        Assert.Equal(
            @"C:\OMSI 2\Splines\Test\Texture\road.dds",
            batch.TexturePath);

        Assert.Equal(
            new System.Numerics.Vector2(
                0,
                0),
            geometry.Vertices[0].TexCoord);

        Assert.InRange(
            geometry.Vertices
                .Max(
                    vertex =>
                        vertex.TexCoord.Y),
            2.399f,
            2.401f);

        Assert.True(
            geometry.Ranges.ContainsKey(
                scene.Splines[0]
                    .PickingId));

        Assert.All(
            geometry.Vertices,
            vertex =>
            {
                Assert.InRange(
                    vertex.Position.X,
                    6.0f,
                    14.0f);

                Assert.InRange(
                    vertex.Position.Y,
                    5.099f,
                    5.101f);

                Assert.InRange(
                    vertex.Position.Z,
                    20.0f,
                    32.0f);
            });
    }

    [Fact]
    public void PathMathAppliesRadiusAndIntegratedGradient()
    {
        var tile =
            new OmsiTileReference(
                0,
                0,
                "tile_0_0.map");

        var length =
            Math.PI *
            50.0;

        var placedSpline =
            new OmsiPlacedSpline(
                "spline",
                @"Splines\Test\curve.sli",
                8,
                -1,
                -1,
                0,
                10,
                0,
                0,
                length,
                100,
                0,
                10,
                false,
                Array.Empty<string>());

        var entity =
            new NativeSplineEntity(
                new PickingId(
                    PickingKind.Spline,
                    1),
                tile,
                placedSpline,
                0,
                10,
                0);

        var frame =
            NativeSplinePathMath
                .GetFrame(
                    entity,
                    length);

        Assert.InRange(
            frame.Center.X,
            99.999f,
            100.001f);

        Assert.InRange(
            frame.Center.Z,
            99.999f,
            100.001f);

        Assert.InRange(
            frame.Center.Y,
            (float)(
                10 +
                length *
                0.05 -
                0.001),
            (float)(
                10 +
                length *
                0.05 +
                0.001));
    }
}
