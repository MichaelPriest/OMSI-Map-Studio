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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SequentialThreeByThreeCreationKeepsEverySharedEdgeContinuous(
        bool reverseCreationOrder)
    {
        var template =
            new OmsiTerrainGrid(
                2,
                new float[]
                {
                    0, 1, 2,
                    10, 11, 12,
                    20, 21, 22
                });

        (int X, int Y)[] creationOrder =
        [
            (-1, -1), (0, -1), (1, -1),
            (-1, 0), (0, 0), (1, 0),
            (-1, 1), (0, 1), (1, 1)
        ];

        if (reverseCreationOrder)
        {
            Array.Reverse(
                creationOrder);
        }

        var terrains =
            new Dictionary<
                (int X, int Y),
                OmsiTerrainGrid>();

        foreach (var coordinate in creationOrder)
        {
            var result =
                OmsiTerrainBorderStitcher
                    .StitchToNeighbors(
                        template,
                        GetTerrain(
                            terrains,
                            coordinate.X - 1,
                            coordinate.Y),
                        GetTerrain(
                            terrains,
                            coordinate.X + 1,
                            coordinate.Y),
                        GetTerrain(
                            terrains,
                            coordinate.X,
                            coordinate.Y - 1),
                        GetTerrain(
                            terrains,
                            coordinate.X,
                            coordinate.Y + 1));

            terrains[coordinate] =
                result.Terrain;
        }

        Assert.Equal(
            9,
            terrains.Count);

        foreach (var coordinate in creationOrder)
        {
            var terrain =
                terrains[coordinate];

            if (
                terrains.TryGetValue(
                    (
                        coordinate.X + 1,
                        coordinate.Y
                    ),
                    out var positiveX))
            {
                AssertVerticalSharedEdge(
                    terrain,
                    positiveX);
            }

            if (
                terrains.TryGetValue(
                    (
                        coordinate.X,
                        coordinate.Y + 1
                    ),
                    out var positiveY))
            {
                AssertHorizontalSharedEdge(
                    terrain,
                    positiveY);
            }
        }
    }

    private static OmsiTerrainGrid? GetTerrain(
        IReadOnlyDictionary<
            (int X, int Y),
            OmsiTerrainGrid> terrains,
        int tileX,
        int tileY)
    {
        return
            terrains.TryGetValue(
                (tileX, tileY),
                out var terrain)
                ? terrain
                : null;
    }

    private static void AssertVerticalSharedEdge(
        OmsiTerrainGrid left,
        OmsiTerrainGrid right)
    {
        Assert.Equal(
            left.CellCount,
            right.CellCount);

        var sampleCount =
            left.CellCount +
            1;

        for (
            var row = 0;
            row < sampleCount;
            row++)
        {
            Assert.Equal(
                left.Heights[
                    row *
                        sampleCount +
                    left.CellCount],
                right.Heights[
                    row *
                    sampleCount]);
        }
    }

    private static void AssertHorizontalSharedEdge(
        OmsiTerrainGrid top,
        OmsiTerrainGrid bottom)
    {
        Assert.Equal(
            top.CellCount,
            bottom.CellCount);

        var sampleCount =
            top.CellCount +
            1;

        for (
            var column = 0;
            column < sampleCount;
            column++)
        {
            Assert.Equal(
                top.Heights[
                    top.CellCount *
                        sampleCount +
                    column],
                bottom.Heights[
                    column]);
        }
    }
}
