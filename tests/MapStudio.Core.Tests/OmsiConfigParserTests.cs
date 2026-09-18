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

    [Fact]
    public void ReadObjects_ExtractsConfirmedBaseTransformAndKeepsExtras()
    {
        const string source =
            "[object]\r\n" +
            "0\r\n" +
            "Sceneryobjects\\Trees_MC\\tree_medium_06.sco\r\n" +
            "5702\r\n" +
            "10.0813293437846\r\n" +
            "8.29534931351765\r\n" +
            "1.25\r\n" +
            "170.279991404134\r\n" +
            "7.5\r\n" +
            "-2.25\r\n" +
            "4\r\n" +
            "Tree_Medium_06.tga\r\n" +
            "14.160\r\n" +
            "0.926\r\n";

        var document = OmsiConfigParser.Parse(source);
        var objects = OmsiTileReader.ReadObjects(document);

        var placedObject = Assert.Single(objects);
        Assert.Equal("0", placedObject.HeaderValue);
        Assert.Equal("Sceneryobjects\\Trees_MC\\tree_medium_06.sco", placedObject.SceneryObjectPath);
        Assert.Equal(5702, placedObject.ObjectId);
        Assert.Equal(10.0813293437846, placedObject.X);
        Assert.Equal(8.29534931351765, placedObject.Y);
        Assert.Equal(1.25, placedObject.Z);
        Assert.Equal(170.279991404134, placedObject.Rotation);
        Assert.Equal(7.5, placedObject.Pitch);
        Assert.Equal(-2.25, placedObject.Bank);
        Assert.Equal(
            new[] { "4", "Tree_Medium_06.tga", "14.160", "0.926" },
            placedObject.ExtraValues);
    }

    [Fact]
    public void ReadObjects_SkipsMalformedObjectWithoutThrowing()
    {
        const string source =
            "[object]\r\n" +
            "0\r\n" +
            "Sceneryobjects\\Broken.sco\r\n" +
            "not-an-id\r\n" +
            "10\r\n20\r\n30\r\n0\r\n0\r\n0\r\n";

        var document = OmsiConfigParser.Parse(source);

        Assert.Empty(OmsiTileReader.ReadObjects(document));
        Assert.Equal(source, document.ToText());
    }
}
