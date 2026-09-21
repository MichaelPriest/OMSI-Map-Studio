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
}
