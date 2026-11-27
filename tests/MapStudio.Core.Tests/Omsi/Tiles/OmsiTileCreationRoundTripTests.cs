using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Workspace;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiTileCreationRoundTripTests
{
    [Fact]
    public async Task PersistedThreeByThreeGridReopensWithContinuousTerrainEdges()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-TileRoundTrip-" +
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            var mapDirectory =
                await new MapStudioWorkspaceBootstrapper()
                    .CreateBlankMapAsync(
                        root,
                        "Tile_RoundTrip",
                        "Tile RoundTrip");

            var templateTerrain =
                new OmsiTerrainGrid(
                    2,
                    new float[]
                    {
                        0, 1, 2,
                        10, 11, 12,
                        20, 21, 22
                    });

            await File.WriteAllBytesAsync(
                Path.Combine(
                    mapDirectory,
                    "tile_0_0.map.terrain"),
                OmsiTerrainWriter.Write(
                    templateTerrain));

            var creationOrder =
                new (int X, int Y)[]
                {
                    (-1, -1),
                    (0, -1),
                    (1, -1),
                    (-1, 0),
                    (1, 0),
                    (-1, 1),
                    (0, 1),
                    (1, 1)
                };

            foreach (var coordinate in creationOrder)
            {
                await CreateAndPersistTileAsync(
                    mapDirectory,
                    coordinate.X,
                    coordinate.Y,
                    templateTerrain);
            }

            var reopened =
                await OmsiMapCatalog
                    .OpenMapAsync(
                        mapDirectory);

            Assert.Equal(
                9,
                reopened.Tiles.Count);

            var tileCoordinates =
                reopened.Tiles
                    .Select(
                        tile =>
                            (
                                tile.X,
                                tile.Y
                            ))
                    .ToHashSet();

            for (
                var y = -1;
                y <= 1;
                y++)
            {
                for (
                    var x = -1;
                    x <= 1;
                    x++)
                {
                    Assert.Contains(
                        (x, y),
                        tileCoordinates);

                    var bounds =
                        OmsiTileGrid
                            .GetBounds(
                                x,
                                y);

                    Assert.Equal(
                        x *
                            OmsiTileGrid.TileSize,
                        bounds.MinX);

                    Assert.Equal(
                        y *
                            OmsiTileGrid.TileSize,
                        bounds.MinZ);
                }
            }

            Assert.Equal(
                -300.0,
                reopened.Tiles
                    .Min(
                        tile =>
                            OmsiTileGrid
                                .GetBounds(
                                    tile.X,
                                    tile.Y)
                                .MinX));

            Assert.Equal(
                600.0,
                reopened.Tiles
                    .Max(
                        tile =>
                            OmsiTileGrid
                                .GetBounds(
                                    tile.X,
                                    tile.Y)
                                .MaxX));

            for (
                var y = -1;
                y <= 1;
                y++)
            {
                for (
                    var x = -1;
                    x < 1;
                    x++)
                {
                    var left =
                        await ReadTerrainAsync(
                            reopened,
                            x,
                            y);

                    var right =
                        await ReadTerrainAsync(
                            reopened,
                            x + 1,
                            y);

                    AssertVerticalSharedEdge(
                        left,
                        right);
                }
            }

            for (
                var x = -1;
                x <= 1;
                x++)
            {
                for (
                    var y = -1;
                    y < 1;
                    y++)
                {
                    var top =
                        await ReadTerrainAsync(
                            reopened,
                            x,
                            y);

                    var bottom =
                        await ReadTerrainAsync(
                            reopened,
                            x,
                            y + 1);

                    AssertHorizontalSharedEdge(
                        top,
                        bottom);
                }
            }

            foreach (var tile in reopened.Tiles)
            {
                var content =
                    await new OmsiTileReader()
                        .ReadContentAsync(
                            Path.Combine(
                                mapDirectory,
                                tile.RelativeMapPath));

                Assert.NotNull(
                    content.Terrain);

                Assert.Equal(
                    2,
                    content.Terrain!
                        .CellCount);
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive:
                        true);
            }
        }
    }

    private static async Task CreateAndPersistTileAsync(
        string mapDirectory,
        int tileX,
        int tileY,
        OmsiTerrainGrid templateTerrain)
    {
        var map =
            await OmsiMapCatalog
                .OpenMapAsync(
                    mapDirectory);

        var targetRelativePath =
            $"tile_{tileX}_{tileY}.map";

        var targetMapPath =
            Path.Combine(
                mapDirectory,
                targetRelativePath);

        File.Copy(
            Path.Combine(
                mapDirectory,
                "tile_0_0.map"),
            targetMapPath,
            overwrite:
                false);

        async Task<OmsiTerrainGrid?>
            ReadNeighborAsync(
                int neighborX,
                int neighborY)
        {
            var neighbor =
                map.Tiles
                    .FirstOrDefault(
                        tile =>
                            tile.X ==
                                neighborX &&
                            tile.Y ==
                                neighborY);

            if (neighbor is null)
            {
                return null;
            }

            return await new OmsiTerrainReader()
                .ReadAsync(
                    Path.Combine(
                        mapDirectory,
                        neighbor.RelativeMapPath) +
                    ".terrain");
        }

        var stitched =
            OmsiTerrainBorderStitcher
                .StitchToNeighbors(
                    templateTerrain,
                    await ReadNeighborAsync(
                        tileX - 1,
                        tileY),
                    await ReadNeighborAsync(
                        tileX + 1,
                        tileY),
                    await ReadNeighborAsync(
                        tileX,
                        tileY - 1),
                    await ReadNeighborAsync(
                        tileX,
                        tileY + 1),
                    await ReadNeighborAsync(
                        tileX - 1,
                        tileY - 1),
                    await ReadNeighborAsync(
                        tileX + 1,
                        tileY - 1),
                    await ReadNeighborAsync(
                        tileX - 1,
                        tileY + 1),
                    await ReadNeighborAsync(
                        tileX + 1,
                        tileY + 1));

        await File.WriteAllBytesAsync(
            targetMapPath +
                ".terrain",
            OmsiTerrainWriter.Write(
                stitched.Terrain));

        var globalPath =
            Path.Combine(
                mapDirectory,
                "global.cfg");

        var globalDocument =
            await OmsiConfigParser
                .ParseFileAsync(
                    globalPath);

        var globalBytes =
            OmsiGlobalTileCatalogEditor
                .AppendTile(
                    globalDocument,
                    new OmsiTileReference(
                        tileX,
                        tileY,
                        targetRelativePath));

        await File.WriteAllBytesAsync(
            globalPath,
            globalBytes);
    }

    private static async Task<OmsiTerrainGrid>
        ReadTerrainAsync(
            OmsiMapDescriptor map,
            int tileX,
            int tileY)
    {
        var tile =
            Assert.Single(
                map.Tiles,
                candidate =>
                    candidate.X ==
                        tileX &&
                    candidate.Y ==
                        tileY);

        return await new OmsiTerrainReader()
            .ReadAsync(
                Path.Combine(
                    map.DirectoryPath,
                    tile.RelativeMapPath) +
                ".terrain");
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
