using MapStudio.Core.Omsi.Maps;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiTileRegionSelectorTests
{
    [Fact]
    public void Select_LoadsOnlyNeighborhood()
    {
        var tiles = new List<OmsiTileReference>();

        for (var y = 0; y < 100; y++)
        {
            for (var x = 0; x < 100; x++)
            {
                tiles.Add(
                    new OmsiTileReference(
                        x,
                        y,
                        $"tile_{x}_{y}.map"));
            }
        }

        var region =
            OmsiTileRegionSelector.Select(
                tiles,
                centerX: 50,
                centerY: 50,
                radius: 1);

        Assert.Equal(
            9,
            region.Count);

        Assert.Contains(
            region,
            tile =>
                tile.X == 50 &&
                tile.Y == 50);

        Assert.DoesNotContain(
            region,
            tile =>
                tile.X == 52 ||
                tile.Y == 52);
    }

    [Fact]
    public void FindInitialTile_ChoosesExistingTileNearMapCenter()
    {
        OmsiTileReference[] tiles =
        [
            new(-20, -20, "a.map"),
            new(2, 3, "b.map"),
            new(30, 25, "c.map")
        ];

        var tile =
            OmsiTileRegionSelector
                .FindInitialTile(tiles);

        Assert.NotNull(tile);
        Assert.Equal(2, tile.X);
        Assert.Equal(3, tile.Y);
    }

    [Fact]
    public void Select_HandlesSparseMapsWithoutInventingTiles()
    {
        OmsiTileReference[] tiles =
        [
            new(0, 0, "a.map"),
            new(1, 0, "b.map"),
            new(10, 10, "c.map")
        ];

        var region =
            OmsiTileRegionSelector.Select(
                tiles,
                centerX: 0,
                centerY: 0,
                radius: 1);

        Assert.Equal(
            2,
            region.Count);

        Assert.DoesNotContain(
            region,
            tile =>
                tile.X == 1 &&
                tile.Y == 1);
    }
}
