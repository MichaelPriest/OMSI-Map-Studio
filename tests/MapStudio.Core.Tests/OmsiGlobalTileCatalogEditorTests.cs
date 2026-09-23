using System.Text;
using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Maps;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiGlobalTileCatalogEditorTests
{
    [Fact]
    public void AppendTilePreservesExistingGlobalContent()
    {
        const string source =
            "[name]\r\nTest Map\r\n\r\n" +
            "[future_section]\r\nkeep-me\r\n\r\n" +
            "[map]\r\n0\r\n0\r\ntile_0_0.map\r\n";

        var document =
            OmsiConfigParser.Parse(source);

        var bytes =
            OmsiGlobalTileCatalogEditor
                .AppendTile(
                    document,
                    new OmsiTileReference(
                        1,
                        0,
                        "tile_1_0.map"));

        var text =
            Encoding.UTF8.GetString(bytes);

        Assert.StartsWith(source, text);
        Assert.Contains(
            "[future_section]\r\nkeep-me\r\n",
            text);
        Assert.EndsWith(
            "[map]\r\n1\r\n0\r\ntile_1_0.map\r\n",
            text);

        var parsed =
            OmsiConfigParser.Parse(text);

        Assert.Equal(
            2,
            OmsiMapCatalog
                .ReadTiles(parsed)
                .Count);
    }

    [Fact]
    public void AppendTileRejectsDuplicateCoordinates()
    {
        var document =
            OmsiConfigParser.Parse(
                "[map]\r\n0\r\n0\r\ntile_0_0.map\r\n");

        Assert.Throws<InvalidDataException>(
            () =>
                OmsiGlobalTileCatalogEditor.AppendTile(
                    document,
                    new OmsiTileReference(
                        0,
                        0,
                        "tile_new.map")));
    }

    [Fact]
    public void AppendTileRejectsUnsafePaths()
    {
        var document =
            OmsiConfigParser.Parse(
                "[map]\r\n0\r\n0\r\ntile_0_0.map\r\n");

        Assert.Throws<InvalidDataException>(
            () =>
                OmsiGlobalTileCatalogEditor.AppendTile(
                    document,
                    new OmsiTileReference(
                        1,
                        0,
                        @"..\outside.map")));
    }
    [Fact]
    public void AppendTilePreservesNegativeCoordinates()
    {
        var document =
            OmsiConfigParser.Parse(
                "[map]\r\n0\r\n0\r\ntile_0_0.map\r\n");

        var bytes =
            OmsiGlobalTileCatalogEditor
                .AppendTile(
                    document,
                    new OmsiTileReference(
                        -1,
                        -1,
                        "tile_-1_-1.map"));

        var parsed =
            OmsiConfigParser.Parse(
                Encoding.UTF8.GetString(
                    bytes));

        var tile =
            Assert.Single(
                OmsiMapCatalog
                    .ReadTiles(parsed),
                item =>
                    item.X ==
                        -1 &&
                    item.Y ==
                        -1);

        Assert.Equal(
            "tile_-1_-1.map",
            tile.RelativeMapPath);
    }

    [Fact]
    public void AppendTileBuildsContinuousPersistedThreeByThreeCatalog()
    {
        var document =
            OmsiConfigParser.Parse(
                "[map]\r\n0\r\n0\r\ntile_0_0.map\r\n");

        foreach (
            var tile in
                (
                    from y in
                        Enumerable.Range(
                            -1,
                            3)
                    from x in
                        Enumerable.Range(
                            -1,
                            3)
                    where
                        x != 0 ||
                        y != 0
                    select new OmsiTileReference(
                        x,
                        y,
                        $"tile_{x}_{y}.map")
                ))
        {
            document =
                OmsiConfigParser.Parse(
                    Encoding.UTF8.GetString(
                        OmsiGlobalTileCatalogEditor
                            .AppendTile(
                                document,
                                tile)));
        }

        var tiles =
            OmsiMapCatalog
                .ReadTiles(
                    document)
                .ToDictionary(
                    tile =>
                        (
                            tile.X,
                            tile.Y
                        ));

        Assert.Equal(
            9,
            tiles.Count);

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
                Assert.True(
                    tiles.ContainsKey(
                        (
                            x,
                            y
                        )));

                Assert.Equal(
                    $"tile_{x}_{y}.map",
                    tiles[(x, y)]
                        .RelativeMapPath);
            }
        }
    }

}
