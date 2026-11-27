using System.Text;
using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Terrain;
using MapStudio.Core.Omsi.Maps;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioElevationCoverageResolverTests
{
    [Fact]
    public void ResolveCoversCompleteOmsiTileRectangle()
    {
        var anchor =
            new MapStudioGeographicAnchor(
                -23.55,
                -46.63,
                300,
                150);

        var tiles =
            new[]
            {
                new OmsiTileReference(
                    0,
                    0,
                    "tile_0_0.map"),
                new OmsiTileReference(
                    1,
                    0,
                    "tile_1_0.map")
            };

        var bounds =
            MapStudioElevationCoverageResolver
                .Resolve(
                    tiles,
                    anchor,
                    marginMeters:
                        1.0);

        foreach (
            var world in
                new[]
                {
                    new MapStudioRoadPoint(
                        0,
                        0),
                    new MapStudioRoadPoint(
                        600,
                        0),
                    new MapStudioRoadPoint(
                        0,
                        300),
                    new MapStudioRoadPoint(
                        600,
                        300)
                })
        {
            var geographic =
                MapStudioGeographicProjection
                    .Unproject(
                        anchor,
                        world);

            Assert.InRange(
                geographic.Latitude,
                bounds.South,
                bounds.North);

            Assert.InRange(
                geographic.Longitude,
                bounds.West,
                bounds.East);
        }
    }

    [Fact]
    public async Task TerrainBatchRejectsPartialElevationCoverageBeforeWriting()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-elevation-coverage-" +
                Guid.NewGuid().ToString("N"));

        try
        {
            var mapDirectory =
                Path.Combine(
                    root,
                    "maps",
                    "Coverage");

            Directory.CreateDirectory(
                mapDirectory);

            await File.WriteAllTextAsync(
                Path.Combine(
                    mapDirectory,
                    "global.cfg"),
                "[name]\r\nCoverage\r\n" +
                "[map]\r\n0\r\n0\r\ntile_0_0.map\r\n",
                Encoding.UTF8);

            var tilePath =
                Path.Combine(
                    mapDirectory,
                    "tile_0_0.map");

            await File.WriteAllTextAsync(
                tilePath,
                "[version]\r\n14\r\n",
                Encoding.UTF8);

            var originalTerrain =
                new OmsiTerrainGrid(
                    1,
                    [
                        3f,
                        3f,
                        3f,
                        3f
                    ]);

            var terrainPath =
                tilePath +
                ".terrain";

            await File.WriteAllBytesAsync(
                terrainPath,
                OmsiTerrainWriter.Write(
                    originalTerrain));

            var anchor =
                new MapStudioGeographicAnchor(
                    0,
                    0,
                    150,
                    150);

            var surface =
                new MapStudioGeoreferencedElevationSurface(
                    new MapStudioElevationGrid(
                        2,
                        2,
                        [
                            100,
                            100,
                            100,
                            100
                        ],
                        100,
                        100,
                        "partial-test"),
                    south: -0.0001,
                    west: -0.0001,
                    north: 0.0001,
                    east: 0.0001);

            var exception =
                await Assert.ThrowsAsync<
                    InvalidDataException>(
                    () =>
                        new MapStudioTerrainElevationBatchApplier()
                            .ApplyAsync(
                                root,
                                mapDirectory,
                                anchor,
                                surface));

            Assert.StartsWith(
                "realWorldElevationCoverageIncomplete:",
                exception.Message,
                StringComparison.Ordinal);

            var after =
                await new OmsiTerrainReader()
                    .ReadAsync(
                        terrainPath);

            Assert.Equal(
                originalTerrain.Heights,
                after.Heights);
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
