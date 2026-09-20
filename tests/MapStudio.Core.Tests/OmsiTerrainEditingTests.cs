using MapStudio.Core.Omsi.Maps;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiTerrainEditingTests
{
    [Fact]
    public void Writer_RoundTripsTerrainGrid()
    {
        var terrain =
            new OmsiTerrainGrid(
                2,
                new float[]
                {
                    1, 2, 3,
                    4, 5, 6,
                    7, 8, 9
                });

        var bytes =
            OmsiTerrainWriter.Write(
                terrain);

        var roundTrip =
            OmsiTerrainReader.Read(
                bytes);

        Assert.Equal(
            terrain.CellCount,
            roundTrip.CellCount);

        Assert.Equal(
            terrain.Heights,
            roundTrip.Heights);
    }

    [Fact]
    public void LevelCircularBrush_ChangesOnlySamplesInsideRadius()
    {
        var terrain =
            new OmsiTerrainGrid(
                2,
                Enumerable
                    .Repeat(
                        0f,
                        9)
                    .ToArray());

        var result =
            OmsiTerrainLeveler
                .LevelCircularBrush(
                    terrain,
                    localX: 150,
                    localY: 150,
                    targetHeight: 10,
                    radius: 20,
                    feather: 0);

        Assert.Equal(
            1,
            result.ChangedSamples);

        Assert.Equal(
            10f,
            result.Terrain.Heights[4]);

        Assert.Equal(
            0f,
            result.Terrain.Heights[0]);
    }

    [Fact]
    public void LevelCircularBrush_FeathersOuterRing()
    {
        var terrain =
            new OmsiTerrainGrid(
                4,
                Enumerable
                    .Repeat(
                        0f,
                        25)
                    .ToArray());

        var result =
            OmsiTerrainLeveler
                .LevelCircularBrush(
                    terrain,
                    localX: 150,
                    localY: 150,
                    targetHeight: 20,
                    radius: 120,
                    feather: 0.5);

        Assert.True(
            result.ChangedSamples > 1);

        Assert.Equal(
            20f,
            result.Terrain.Heights[12]);

        Assert.InRange(
            result.Terrain.Heights[7],
            0.01f,
            19.99f);
    }
}
