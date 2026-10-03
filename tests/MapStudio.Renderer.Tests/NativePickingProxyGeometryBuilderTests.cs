using MapStudio.Core.Omsi.Maps;
using MapStudio.Renderer.Picking;
using MapStudio.Renderer.Scene;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativePickingProxyGeometryBuilderTests
{
    [Fact]
    public void BuildCreatesObjectAndSplinePickingTriangles()
    {
        var tile =
            new OmsiTileReference(
                0,
                0,
                "tile_0_0.map");

        var placedObject =
            new OmsiPlacedObject(
                "object",
                @"Sceneryobjects\Test\tree.sco",
                1,
                40,
                50,
                0,
                0,
                0,
                0,
                Array.Empty<string>());

        var placedSpline =
            new OmsiPlacedSpline(
                "spline",
                @"Splines\Test\road.sli",
                2,
                -1,
                -1,
                30,
                0,
                30,
                0,
                60,
                0,
                0,
                0,
                false,
                Array.Empty<string>());

        var content =
            new OmsiTileContent(
                new OmsiTileSummary(
                    true,
                    1,
                    1,
                    0),
                [placedObject],
                [placedSpline]);

        var scene =
            new NativeSceneBuilder()
                .Build(
                    [
                        new NativeSceneTile(
                            tile,
                            content)
                    ],
                    new PickingRegistry<
                        object>());

        var result =
            new NativePickingProxyGeometryBuilder()
                .Build(scene);

        Assert.NotEmpty(
            result.Vertices);

        Assert.Equal(
            2,
            result.Ranges.Count);

        Assert.True(
            result.Ranges.ContainsKey(
                scene.Objects[0]
                    .PickingId));

        Assert.True(
            result.Ranges.ContainsKey(
                scene.Splines[0]
                    .PickingId));
    }
}
