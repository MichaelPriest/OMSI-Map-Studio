using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Maps;

namespace MapStudio.Core.Tests;

public sealed class OmsiConfigParserTests
{
    [Fact]
    public void Parse_RoundTripsOriginalTextExactly()
    {
        const string source = "[name]\r\nMy Map\r\n\r\n[unknown_command]\r\nabc\r\n123\r\n";
        var document = OmsiConfigParser.Parse(source);

        Assert.Equal("\r\n", document.NewLine);
        Assert.True(document.HasTrailingNewLine);
        Assert.Equal(source, document.ToText());
    }

    [Fact]
    public void ReadTiles_ExtractsMapEntriesWithoutLosingUnknownSections()
    {
        const string source =
            "[name]\nTest Map\n" +
            "[map]\n0\n3\ntile_0_3.map\n" +
            "[future_feature]\nkeep-me\n" +
            "[map]\n-1\n4\ntile_-1_4.map\n";

        var document = OmsiConfigParser.Parse(source);
        var tiles = OmsiMapCatalog.ReadTiles(document);

        Assert.Equal(2, tiles.Count);
        Assert.Equal(new OmsiTileReference(0, 3, "tile_0_3.map"), tiles[0]);
        Assert.Equal(new OmsiTileReference(-1, 4, "tile_-1_4.map"), tiles[1]);
        Assert.Equal("keep-me", document.FindFirstSection("future_feature")?.DataLines.Single());
        Assert.Equal(source, document.ToText());
    }
}
