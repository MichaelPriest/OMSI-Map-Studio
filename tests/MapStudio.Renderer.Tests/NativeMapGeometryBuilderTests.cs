using MapStudio.Core.Omsi.Maps;
using MapStudio.Renderer.Picking;
using MapStudio.Renderer.Scene;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeMapGeometryBuilderTests
{
    [Fact]
    public void BuildCreatesTerrainObjectAndSplineLines()
    {
        var tile =
            new OmsiTileReference(
                0,
                0,
                "tile_0_0.map");

        var placedObject =
            new OmsiPlacedObject(
                "object",
                "Sceneryobjects\\Test\\house.sco",
                1,
                20,
                30,
                2,
                0,
                0,
                0,
                Array.Empty<string>());

        var placedSpline =
            new OmsiPlacedSpline(
                "spline",
                "Splines\\Test\\road.sli",
                2,
                -1,
                -1,
                10,
                0,
                10,
                0,
                40,
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

        var picking =
            new PickingRegistry<object>();

        var scene =
            new NativeSceneBuilder()
                .Build(
                    [
                        new NativeSceneTile(
                            tile,
                            content)
                    ],
                    picking);

        var geometry =
            new NativeMapGeometryBuilder()
                .Build(scene);

        Assert.NotEmpty(
            geometry.Vertices);

        Assert.True(
            geometry.LineCount >= 10);

        Assert.All(
            geometry.Vertices,
            vertex =>
            {
                Assert.InRange(
                    vertex.Position.X,
                    -1.1f,
                    1.1f);

                Assert.InRange(
                    vertex.Position.Y,
                    -1.1f,
                    1.1f);
            });
    }
}
