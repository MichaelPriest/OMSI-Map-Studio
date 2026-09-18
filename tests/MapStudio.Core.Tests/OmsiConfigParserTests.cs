using System.Text;
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
    public void ParseBytes_PreservesWindows1252Bytes()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var windows1252 = Encoding.GetEncoding(1252);
        const string source =
            "[name]\r\nSão Paulo\r\n" +
            "[unknown_command]\r\nação\r\n";

        var originalBytes = windows1252.GetBytes(source);
        var document = OmsiConfigParser.ParseBytes(originalBytes);

        Assert.Equal(1252, document.TextEncoding.CodePage);
        Assert.False(document.HasByteOrderMark);
        Assert.Equal(source, document.ToText());
        Assert.Equal(originalBytes, document.ToBytes());
    }

    [Fact]
    public void ParseBytes_PreservesUtf8Bom()
    {
        const string source = "[name]\r\nSão Paulo\r\n";
        var utf8WithBom = new UTF8Encoding(true);
        var body = utf8WithBom.GetBytes(source);
        var preamble = utf8WithBom.GetPreamble();
        var originalBytes = preamble.Concat(body).ToArray();

        var document = OmsiConfigParser.ParseBytes(originalBytes);

        Assert.Equal(65001, document.TextEncoding.CodePage);
        Assert.True(document.HasByteOrderMark);
        Assert.Equal(originalBytes, document.ToBytes());
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

    [Fact]
    public async Task TileReader_CountsObjectsSplinesAndAttachments()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"mapstudio-{Guid.NewGuid():N}.map");

        const string source =
            "[version]\r\n14\r\n" +
            "[spline]\r\n0\r\nSplines\\Street.sli\r\n" +
            "[object]\r\n0\r\nSceneryobjects\\Building.sco\r\n" +
            "[splineAttachement]\r\n0\r\nSceneryobjects\\BusStop.sco\r\n" +
            "[object]\r\n0\r\nSceneryobjects\\Tree.sco\r\n" +
            "[future_section]\r\nkeep-me\r\n";

        try
        {
            await File.WriteAllTextAsync(path, source, new UTF8Encoding(false));

            var summary = await new OmsiTileReader().ReadSummaryAsync(path);

            Assert.True(summary.Exists);
            Assert.Equal(2, summary.ObjectCount);
            Assert.Equal(1, summary.SplineCount);
            Assert.Equal(1, summary.SplineAttachmentCount);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task TileReader_ReturnsMissingSummaryForAbsentTile()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"mapstudio-missing-{Guid.NewGuid():N}.map");

        var summary = await new OmsiTileReader().ReadSummaryAsync(path);

        Assert.False(summary.Exists);
        Assert.Equal(0, summary.ObjectCount);
        Assert.Equal(0, summary.SplineCount);
        Assert.Equal(0, summary.SplineAttachmentCount);
    }
}
