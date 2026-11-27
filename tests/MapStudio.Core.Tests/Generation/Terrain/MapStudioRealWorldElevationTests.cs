using System.Text;
using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Terrain;
using MapStudio.Core.Omsi.Maps;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioRealWorldElevationTests
{
    [Fact]
    public void SurfaceUsesBilinearInterpolationAndMinimumAsDefaultDatum()
    {
        var surface =
            new MapStudioGeoreferencedElevationSurface(
                new MapStudioElevationGrid(
                    2,
                    2,
                    [
                        100,
                        110,
                        120,
                        130
                    ],
                    100,
                    130,
                    "test"),
                south: 0,
                west: 0,
                north: 1,
                east: 1);

        Assert.True(
            surface.TrySampleAbsoluteHeight(
                0.5,
                0.5,
                out var absolute));

        Assert.Equal(
            115,
            absolute,
            6);

        Assert.True(
            surface.TrySampleRelativeHeight(
                0.5,
                0.5,
                out var relative));

        Assert.Equal(
            15,
            relative,
            6);

        Assert.False(
            surface.TrySampleRelativeHeight(
                -1,
                0.5,
                out _));
    }

    [Fact]
    public async Task TerrainBatchResamplesDemIntoStandardOmsiTerrain()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-elevation-" +
                Guid.NewGuid().ToString("N"));

        try
        {
            var mapDirectory =
                Path.Combine(
                    root,
                    "maps",
                    "ElevationTest");

            Directory.CreateDirectory(
                mapDirectory);

            await File.WriteAllTextAsync(
                Path.Combine(
                    mapDirectory,
                    "global.cfg"),
                "[name]\r\nElevation Test\r\n" +
                "[map]\r\n0\r\n0\r\ntile_0_0.map\r\n",
                Encoding.UTF8);

            var tilePath =
                Path.Combine(
                    mapDirectory,
                    "tile_0_0.map");

            await File.WriteAllTextAsync(
                tilePath,
                "[terrain]\r\n",
                Encoding.UTF8);

            await File.WriteAllBytesAsync(
                tilePath +
                    ".terrain",
                OmsiTerrainWriter.Write(
                    new OmsiTerrainGrid(
                        60,
                        new float[
                            61 *
                            61])));

            var anchor =
                new MapStudioGeographicAnchor(
                    0,
                    0,
                    0,
                    0);

            var northWest =
                MapStudioGeographicProjection
                    .Unproject(
                        anchor,
                        new MapStudioRoadPoint(
                            0,
                            0));

            var southEast =
                MapStudioGeographicProjection
                    .Unproject(
                        anchor,
                        new MapStudioRoadPoint(
                            300,
                            300));

            var surface =
                new MapStudioGeoreferencedElevationSurface(
                    new MapStudioElevationGrid(
                        2,
                        2,
                        [
                            100,
                            110,
                            120,
                            130
                        ],
                        100,
                        130,
                        "test"),
                    southEast.Latitude,
                    northWest.Longitude,
                    northWest.Latitude,
                    southEast.Longitude);

            var result =
                await new MapStudioTerrainElevationBatchApplier()
                    .ApplyAsync(
                        root,
                        mapDirectory,
                        anchor,
                        surface);

            Assert.Equal(
                1,
                result.ModifiedTileCount);

            Assert.True(
                result.ChangedSamples >
                3000);

            Assert.Equal(
                0,
                result.UncoveredSamples);

            var terrain =
                await new OmsiTerrainReader()
                    .ReadAsync(
                        tilePath +
                        ".terrain");

            Assert.InRange(
                terrain.Heights[0],
                -0.001f,
                0.001f);

            Assert.InRange(
                terrain.Heights[^1],
                29.999f,
                30.001f);

            Assert.InRange(
                terrain.Heights[
                    30 *
                        61 +
                    30],
                14.8f,
                15.2f);

            Assert.True(
                File.Exists(
                    Assert.Single(
                        result.Tiles)
                    .BackupPath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }
}
