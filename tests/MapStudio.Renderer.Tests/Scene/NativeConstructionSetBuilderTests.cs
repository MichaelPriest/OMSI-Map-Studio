using MapStudio.Core.Omsi.Maps;
using MapStudio.Renderer.Picking;
using MapStudio.Renderer.Scene;
using MapStudio.Renderer.Viewport;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeConstructionSetBuilderTests
{
    [Fact]
    public void BuildFollowsSplineAndBothSides()
    {
        var tile =
            new OmsiTileReference(
                0,
                0,
                "tile_0_0.map");

        var placed =
            new OmsiPlacedSpline(
                "0",
                @"Splines\Roads\street.sli",
                100,
                -1,
                -1,
                10,
                2,
                20,
                0,
                20,
                0,
                0,
                0,
                false,
                []);

        var entity =
            new NativeSplineEntity(
                new PickingId(PickingKind.Spline, 1),
                tile,
                placed,
                10,
                2,
                20);

        var set =
            new NativeConstructionSetDefinition(
                "lights",
                "Postes",
                placed.SplinePath,
                [
                    new NativeConstructionSetCompanion(
                        "lamp",
                        @"Sceneryobjects\Lights\lamp.sco",
                        10,
                        4,
                        NativeConstructionSetSide.Both,
                        0)
                ]);

        var groups =
            NativeConstructionSetBuilder
                .Build(
                    entity,
                    set);

        var group =
            Assert.Single(
                groups);

        Assert.Equal(
            6,
            group.Placements.Count);

        Assert.Contains(
            group.Placements,
            item =>
                item.WorldX <
                entity.WorldX);

        Assert.Contains(
            group.Placements,
            item =>
                item.WorldX >
                entity.WorldX);

        Assert.Equal(
            2,
            group.Placements[0].Z);
    }

    [Fact]
    public void BuildCapsWholeSetAt512Objects()
    {
        var tile =
            new OmsiTileReference(
                0,
                0,
                "tile.map");

        var placed =
            new OmsiPlacedSpline(
                "0",
                @"Splines\Roads\long.sli",
                1,
                -1,
                -1,
                0,
                0,
                0,
                0,
                1000,
                0,
                0,
                0,
                false,
                []);

        var entity =
            new NativeSplineEntity(
                new PickingId(PickingKind.Spline, 1),
                tile,
                placed,
                0,
                0,
                0);

        var companions =
            Enumerable.Range(
                    0,
                    16)
                .Select(
                    index =>
                        new NativeConstructionSetCompanion(
                            index.ToString(),
                            $@"Sceneryobjects\Set\{index}.sco",
                            1,
                            2,
                            NativeConstructionSetSide.Both,
                            0))
                .ToArray();

        var groups =
            NativeConstructionSetBuilder
                .Build(
                    entity,
                    new NativeConstructionSetDefinition(
                        "max",
                        "Max",
                        null,
                        companions));

        Assert.Equal(
            512,
            groups.Sum(
                group =>
                    group.Placements.Count));
    }
}
