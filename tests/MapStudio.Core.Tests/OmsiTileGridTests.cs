using System.Linq;
using MapStudio.Core.Omsi.Maps;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiTileGridTests
{
    [Fact]
    public void PositiveNeighborsShareExactEdges()
    {
        var center = OmsiTileGrid.GetBounds(0, 0);
        var right = OmsiTileGrid.GetBounds(1, 0);
        var nextY = OmsiTileGrid.GetBounds(0, 1);

        Assert.Equal(center.MaxX, right.MinX);
        Assert.Equal(center.MinZ, right.MinZ);
        Assert.Equal(center.MaxZ, right.MaxZ);

        Assert.Equal(center.MaxZ, nextY.MinZ);
        Assert.Equal(center.MinX, nextY.MinX);
        Assert.Equal(center.MaxX, nextY.MaxX);
    }

    [Fact]
    public void NegativeCoordinatesUseFloorBasedContinuousGrid()
    {
        var left = OmsiTileGrid.GetBounds(-1, 0);
        var previousY = OmsiTileGrid.GetBounds(0, -1);
        var diagonal = OmsiTileGrid.GetBounds(-1, -1);

        Assert.Equal(-300.0, left.MinX);
        Assert.Equal(0.0, left.MaxX);
        Assert.Equal(-300.0, previousY.MinZ);
        Assert.Equal(0.0, previousY.MaxZ);
        Assert.Equal(-300.0, diagonal.MinX);
        Assert.Equal(-300.0, diagonal.MinZ);

        Assert.Equal(-1, OmsiTileGrid.WorldToTileX(-0.001));
        Assert.Equal(-1, OmsiTileGrid.WorldToTileX(-300.0));
        Assert.Equal(-2, OmsiTileGrid.WorldToTileX(-300.001));
        Assert.Equal(-1, OmsiTileGrid.WorldToTileY(-0.001));
    }

    [Fact]
    public void ThreeByThreeBoundsFormContinuousGrid()
    {
        var bounds =
            (
                from tileY in Enumerable.Range(-1, 3)
                from tileX in Enumerable.Range(-1, 3)
                select new
                {
                    X = tileX,
                    Y = tileY,
                    Bounds = OmsiTileGrid.GetBounds(tileX, tileY)
                }
            )
            .ToDictionary(
                item => (item.X, item.Y),
                item => item.Bounds);

        Assert.Equal(-300.0, bounds.Values.Min(item => item.MinX));
        Assert.Equal(600.0, bounds.Values.Max(item => item.MaxX));
        Assert.Equal(-300.0, bounds.Values.Min(item => item.MinZ));
        Assert.Equal(600.0, bounds.Values.Max(item => item.MaxZ));

        for (var y = -1; y <= 1; y++)
        {
            for (var x = -1; x < 1; x++)
            {
                Assert.Equal(
                    bounds[(x, y)].MaxX,
                    bounds[(x + 1, y)].MinX);
            }
        }

        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y < 1; y++)
            {
                Assert.Equal(
                    bounds[(x, y)].MaxZ,
                    bounds[(x, y + 1)].MinZ);
            }
        }
    }
}
