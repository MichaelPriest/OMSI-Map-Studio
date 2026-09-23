using MapStudio.Core.Omsi.Maps;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiTerrainBorderStitcherTests
{
    [Fact]
    public void StitchCopiesAllFourOpposingNeighborEdges()
    {
        var target =
            new OmsiTerrainGrid(
                2,
                new float[9]);

        var negativeX =
            new OmsiTerrainGrid(
                2,
                new float[]
                {
                    0, 0, 1,
                    0, 0, 2,
                    0, 0, 3
                });

        var positiveX =
            new OmsiTerrainGrid(
                2,
                new float[]
                {
                    4, 0, 0,
                    5, 0, 0,
                    6, 0, 0
                });

        var negativeY =
            new OmsiTerrainGrid(
                2,
                new float[]
                {
                    0, 0, 0,
                    0, 0, 0,
                    1, 8, 4
                });

        var positiveY =
            new OmsiTerrainGrid(
                2,
                new float[]
                {
                    3, 9, 6,
                    0, 0, 0,
                    0, 0, 0
                });

        var result =
            OmsiTerrainBorderStitcher.StitchToNeighbors(
                target,
                negativeX,
                positiveX,
                negativeY,
                positiveY);

        Assert.Equal(
            new float[]
            {
                1, 8, 4,
                2, 0, 5,
                3, 9, 6
            },
            result.Terrain.Heights);

        Assert.True(result.ChangedSamples > 0);
    }

    [Fact]
    public void StitchResamplesNeighborEdgeWhenGridResolutionDiffers()
    {
        var target =
            new OmsiTerrainGrid(
                2,
                new float[9]);

        var negativeX =
            new OmsiTerrainGrid(
                1,
                new float[]
                {
                    0, 10,
                    0, 20
                });

        var result =
            OmsiTerrainBorderStitcher.StitchToNeighbors(
                target,
                negativeX,
                positiveX: null,
                negativeY: null,
                positiveY: null);

        Assert.Equal(10f, result.Terrain.Heights[0]);
        Assert.Equal(15f, result.Terrain.Heights[3]);
        Assert.Equal(20f, result.Terrain.Heights[6]);
    }
}
