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
    public void OffsetCircularBrush_RaisesCenterByDelta()
    {
        var terrain =
            new OmsiTerrainGrid(
                2,
                Enumerable
                    .Repeat(
                        5f,
                        9)
                    .ToArray());

        var result =
            OmsiTerrainLeveler
                .OffsetCircularBrush(
                    terrain,
                    localX: 150,
                    localY: 150,
                    deltaHeight: 2.5,
                    radius: 20,
                    feather: 0);

        Assert.Equal(
            1,
            result.ChangedSamples);

        Assert.Equal(
            7.5f,
            result.Terrain.Heights[4]);

        Assert.Equal(
            5f,
            result.Terrain.Heights[0]);
    }

    [Fact]
    public void OffsetCircularBrush_LowersAndFeathersOuterSamples()
    {
        var terrain =
            new OmsiTerrainGrid(
                4,
                Enumerable
                    .Repeat(
                        20f,
                        25)
                    .ToArray());

        var result =
            OmsiTerrainLeveler
                .OffsetCircularBrush(
                    terrain,
                    localX: 150,
                    localY: 150,
                    deltaHeight: -4,
                    radius: 120,
                    feather: 0.5);

        Assert.Equal(
            16f,
            result.Terrain.Heights[12]);

        Assert.InRange(
            result.Terrain.Heights[7],
            16.01f,
            19.99f);

        Assert.True(
            result.ChangedSamples >
            1);
    }


    [Fact]
    public void ConformToSpline_FollowsSplineElevationAndLeavesOutsideUntouched()
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
                .ConformToSpline(
                    terrain,
                    tileOriginX:
                        0,
                    tileOriginZ:
                        0,
                    splineWorldX:
                        150,
                    splineWorldY:
                        10,
                    splineWorldZ:
                        0,
                    rotationDegrees:
                        0,
                    length:
                        300,
                    radius:
                        0,
                    gradientStart:
                        10,
                    gradientEnd:
                        10,
                    halfWidth:
                        20,
                    featherWidth:
                        20,
                    verticalOffset:
                        0);

        Assert.Equal(
            10f,
            result.Terrain
                .Heights[2]);

        Assert.Equal(
            25f,
            result.Terrain
                .Heights[12]);

        Assert.Equal(
            40f,
            result.Terrain
                .Heights[22]);

        Assert.Equal(
            0f,
            result.Terrain
                .Heights[0]);

        Assert.True(
            result.ChangedSamples >=
            5);
    }

    [Fact]
    public void GetSplineInfluenceBounds_ContainsCurvedArc()
    {
        var length =
            Math.PI *
            100 /
            2;

        var bounds =
            OmsiTerrainLeveler
                .GetSplineInfluenceBounds(
                    splineWorldX:
                        100,
                    splineWorldY:
                        5,
                    splineWorldZ:
                        100,
                    rotationDegrees:
                        0,
                    length,
                    radius:
                        100,
                    gradientStart:
                        0,
                    gradientEnd:
                        0,
                    influenceWidth:
                        10);

        Assert.InRange(
            bounds.MinX,
            89.9,
            90.1);

        Assert.InRange(
            bounds.MinZ,
            89.9,
            90.1);

        Assert.InRange(
            bounds.MaxX,
            209.9,
            210.1);

        Assert.InRange(
            bounds.MaxZ,
            209.9,
            210.1);
    }

    [Fact]
    public void ApplyElevationGrid_ResamplesCornersAndCenter()
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
                .ApplyElevationGrid(
                    terrain,
                    rows: 2,
                    columns: 2,
                    elevations:
                        new double[]
                        {
                            10,
                            20,
                            30,
                            40
                        },
                    verticalOffset: 5);

        Assert.Equal(
            15f,
            result.Terrain.Heights[0]);

        Assert.Equal(
            45f,
            result.Terrain.Heights[8]);

        Assert.Equal(
            30f,
            result.Terrain.Heights[4]);

        Assert.Equal(
            9,
            result.ChangedSamples);
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
