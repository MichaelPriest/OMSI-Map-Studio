using System.Text;
using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Maps;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiTileDeletionSafetyTests
{
    [Fact]
    public void LastEmptyTileCanBeRemovedWithoutTouchingLaterSections()
    {
        const string source =
            "[name]\r\nTest\r\n\r\n" +
            "[map]\r\n0\r\n0\r\ntile_0_0.map\r\n\r\n" +
            "[map]\r\n1\r\n0\r\ntile_1_0.map\r\n\r\n" +
            "[future_section]\r\nkeep-me\r\n";

        var document =
            OmsiConfigParser.Parse(
                source);

        var tile =
            new OmsiTileReference(
                1,
                0,
                "tile_1_0.map");

        var safety =
            OmsiTileDeletionSafetyAnalyzer
                .Analyze(
                    document,
                    tile,
                    EmptyContent());

        Assert.True(
            safety.CanDelete);

        Assert.Equal(
            1,
            safety.MapIndex);

        var bytes =
            OmsiGlobalTileCatalogRemover
                .RemoveLastTile(
                    document,
                    tile);

        var text =
            Encoding.UTF8
                .GetString(
                    bytes);

        Assert.DoesNotContain(
            "tile_1_0.map",
            text);

        Assert.Contains(
            "[future_section]\r\nkeep-me",
            text);

        Assert.Single(
            OmsiMapCatalog
                .ReadTiles(
                    OmsiConfigParser
                        .Parse(
                            text)));
    }

    [Fact]
    public void NonLastTileIsBlockedToAvoidIndexShift()
    {
        var document =
            OmsiConfigParser.Parse(
                "[map]\r\n0\r\n0\r\ntile_0_0.map\r\n\r\n" +
                "[map]\r\n1\r\n0\r\ntile_1_0.map\r\n");

        var safety =
            OmsiTileDeletionSafetyAnalyzer
                .Analyze(
                    document,
                    new OmsiTileReference(
                        0,
                        0,
                        "tile_0_0.map"),
                    EmptyContent());

        Assert.False(
            safety.CanDelete);

        Assert.Contains(
            "tileDeletionWouldShiftMapIndices",
            safety.Reasons);
    }

    [Fact]
    public void TileWithObjectsIsBlocked()
    {
        var document =
            OmsiConfigParser.Parse(
                "[map]\r\n0\r\n0\r\ntile_0_0.map\r\n\r\n" +
                "[map]\r\n1\r\n0\r\ntile_1_0.map\r\n");

        var content =
            EmptyContent() with
            {
                Objects =
                    [
                        new OmsiPlacedObject(
                            "0",
                            @"Sceneryobjects\Pack\House.sco",
                            10,
                            1,
                            2,
                            0,
                            0,
                            0,
                            0,
                            [])
                    ]
            };

        var safety =
            OmsiTileDeletionSafetyAnalyzer
                .Analyze(
                    document,
                    new OmsiTileReference(
                        1,
                        0,
                        "tile_1_0.map"),
                    content);

        Assert.False(
            safety.CanDelete);

        Assert.Contains(
            "tileNotEmpty",
            safety.Reasons);
    }

    [Fact]
    public void EntrypointReferenceBlocksDeletion()
    {
        var document =
            OmsiConfigParser.Parse(
                "[map]\r\n0\r\n0\r\ntile_0_0.map\r\n\r\n" +
                "[map]\r\n1\r\n0\r\ntile_1_0.map\r\n\r\n" +
                "[entrypoints]\r\n" +
                "0\r\n" +
                "100\r\n" +
                "a\r\n" +
                "b\r\n" +
                "c\r\n" +
                "d\r\n" +
                "e\r\n" +
                "f\r\n" +
                "g\r\n" +
                "h\r\n" +
                "1\r\n" +
                "Entry Name\r\n");

        var safety =
            OmsiTileDeletionSafetyAnalyzer
                .Analyze(
                    document,
                    new OmsiTileReference(
                        1,
                        0,
                        "tile_1_0.map"),
                    EmptyContent());

        Assert.False(
            safety.CanDelete);

        Assert.Contains(
            "tileReferencedByEntrypoint",
            safety.Reasons);
    }

    [Fact]
    public void OnlyTileCannotBeDeleted()
    {
        var document =
            OmsiConfigParser.Parse(
                "[map]\r\n0\r\n0\r\ntile_0_0.map\r\n");

        var safety =
            OmsiTileDeletionSafetyAnalyzer
                .Analyze(
                    document,
                    new OmsiTileReference(
                        0,
                        0,
                        "tile_0_0.map"),
                    EmptyContent());

        Assert.False(
            safety.CanDelete);

        Assert.Contains(
            "cannotDeleteOnlyMapTile",
            safety.Reasons);
    }

    private static OmsiTileContent EmptyContent() =>
        new(
            new OmsiTileSummary(
                true,
                0,
                0,
                0),
            [],
            []);
}
