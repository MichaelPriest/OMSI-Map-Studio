using MapStudio.Core.Omsi.Maps;
using MapStudio.Renderer.Picking;
using MapStudio.Renderer.Scene;
using MapStudio.Renderer.Viewport;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeJunctionSuggestionBuilderTests
{
    [Fact]
    public void BuildDetectsPerpendicularSplineIntersection()
    {
        var tile =
            new OmsiTileReference(
                0,
                0,
                "tile_0_0.map");

        var a =
            new NativeSplineEntity(
                new PickingId(
                    PickingKind.Spline,
                    1),
                tile,
                new OmsiPlacedSpline(
                    "0",
                    @"Splines\Roads\a.sli",
                    1,
                    -1,
                    -1,
                    0,
                    0,
                    0,
                    0,
                    100,
                    0,
                    0,
                    0,
                    false,
                    []),
                0,
                0,
                0);

        var b =
            new NativeSplineEntity(
                new PickingId(
                    PickingKind.Spline,
                    2),
                tile,
                new OmsiPlacedSpline(
                    "0",
                    @"Splines\Roads\b.sli",
                    2,
                    -1,
                    -1,
                    -50,
                    0,
                    50,
                    90,
                    100,
                    0,
                    0,
                    0,
                    false,
                    []),
                -50,
                0,
                50);

        var scene =
            new NativeSceneSnapshot(
                [new NativeSceneTile(
                    tile,
                    new OmsiTileContent(
                        new OmsiTileSummary(
                            true,
                            0,
                            2,
                            0),
                        [],
                        [a.Spline,b.Spline]))],
                [],
                [a,b],
                []);

        var result =
            new NativeJunctionSuggestionBuilder()
                .Build(
                    scene);

        var suggestion =
            Assert.Single(
                result);

        Assert.Equal(
            1,
            suggestion.SplineA);

        Assert.Equal(
            2,
            suggestion.SplineB);

        Assert.InRange(
            suggestion.X,
            -0.01,
            0.01);

        Assert.InRange(
            suggestion.Y,
            49.99,
            50.01);

        Assert.InRange(
            suggestion.Rotation,
            -0.01,
            0.01);
    }

    [Fact]
    public void BuildIgnoresNearlyParallelSplines()
    {
        var tile =
            new OmsiTileReference(
                0,
                0,
                "tile.map");

        NativeSplineEntity Make(
            int id,
            double x,
            double z,
            double rotation) =>
            new(
                new PickingId(
                    PickingKind.Spline,
                    id),
                tile,
                new OmsiPlacedSpline(
                    "0",
                    @"Splines\Roads\road.sli",
                    id,
                    -1,
                    -1,
                    x,
                    0,
                    z,
                    rotation,
                    100,
                    0,
                    0,
                    0,
                    false,
                    []),
                (float)x,
                0,
                (float)z);

        var a =
            Make(
                1,
                0,
                0,
                0);

        var b =
            Make(
                2,
                -3,
                50,
                4);

        var scene =
            new NativeSceneSnapshot(
                [],
                [],
                [a,b],
                []);

        Assert.Empty(
            new NativeJunctionSuggestionBuilder()
                .Build(
                    scene));
    }
}
