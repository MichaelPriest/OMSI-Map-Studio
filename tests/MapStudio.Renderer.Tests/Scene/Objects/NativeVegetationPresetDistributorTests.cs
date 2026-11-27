using System.Numerics;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Renderer.Viewport;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeVegetationPresetDistributorTests
{
    [Fact]
    public void DistributeUsesEveryPresetAssetAcrossEnoughPlacements()
    {
        var tile =
            new OmsiTileReference(
                0,
                0,
                "tile_0_0.map");

        var placements =
            Enumerable.Range(
                    0,
                    9)
                .Select(
                    index =>
                        new NativeSceneryPlacementRequest(
                            tile,
                            "Sceneryobjects/base/tree.sco",
                            index,
                            index,
                            0,
                            0,
                            0,
                            0,
                            new Vector3(
                                index * 3,
                                0,
                                index * 5),
                            false))
                .ToArray();

        var paths =
            new[]
            {
                "Sceneryobjects/trees/oak.sco",
                "Sceneryobjects/trees/pine.sco",
                "Sceneryobjects/trees/palm.sco"
            };

        var result =
            NativeVegetationPresetDistributor
                .Distribute(
                    placements,
                    paths);

        Assert.Equal(
            placements.Length,
            result.Count);

        Assert.Equal(
            paths.Length,
            result
                .Select(
                    placement =>
                        placement
                            .SceneryObjectPath)
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .Count());

        Assert.All(
            result,
            placement =>
                Assert.Contains(
                    placement
                        .SceneryObjectPath,
                    paths));
    }

    [Fact]
    public void DistributeIsStableForSamePlacements()
    {
        var tile =
            new OmsiTileReference(
                0,
                0,
                "tile_0_0.map");

        var placement =
            new NativeSceneryPlacementRequest(
                tile,
                "Sceneryobjects/base/tree.sco",
                4,
                8,
                0,
                0,
                0,
                0,
                new Vector3(
                    4,
                    0,
                    8),
                false);

        var paths =
            new[]
            {
                "b.sco",
                "a.sco",
                "c.sco"
            };

        var first =
            NativeVegetationPresetDistributor
                .Distribute(
                    [placement, placement],
                    paths);

        var second =
            NativeVegetationPresetDistributor
                .Distribute(
                    [placement, placement],
                    paths);

        Assert.Equal(
            first
                .Select(
                    item =>
                        item.SceneryObjectPath),
            second
                .Select(
                    item =>
                        item.SceneryObjectPath));
    }
}
