using System.Text;
using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Maps;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiObjectInsertionTests
{
    [Fact]
    public void Analyzer_UsesGlobalObjectAndSplineIds()
    {
        var tileA = new OmsiTileContent(
            new OmsiTileSummary(
                true,
                1,
                1,
                0),
            [
                new OmsiPlacedObject(
                    "0",
                    @"Sceneryobjects\Pack\House.sco",
                    20,
                    1,
                    2,
                    3,
                    0,
                    0,
                    0,
                    ["0"])
            ],
            [
                new OmsiPlacedSpline(
                    "0",
                    @"Splines\Road.sli",
                    75,
                    -1,
                    -1,
                    0,
                    0,
                    0,
                    0,
                    10,
                    0,
                    0,
                    0,
                    false,
                    [])
            ]);

        var tileB = new OmsiTileContent(
            new OmsiTileSummary(
                true,
                1,
                0,
                0),
            [
                new OmsiPlacedObject(
                    "0",
                    @"Sceneryobjects\Pack\Tree.sco",
                    120,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    ["4", "Tree.tga"])
            ],
            []);

        var analysis =
            OmsiObjectInsertionAnalyzer
                .Analyze(
                    [tileA, tileB],
                    @"Sceneryobjects\Pack\Tree.sco");

        Assert.Equal(
            120,
            analysis.MaxUsedId);

        Assert.Equal(
            121,
            analysis.GetNextId());

        Assert.NotNull(
            analysis.MatchingObjectTemplate);

        Assert.Equal(
            new[] { "4", "Tree.tga" },
            analysis
                .MatchingObjectTemplate!
                .ExtraValues);
    }

    [Fact]
    public void Inserter_AppendsObjectAndPreservesExistingContent()
    {
        const string source =
            "[version]\r\n14\r\n" +
            "[future_section]\r\nkeep-me\r\n";

        var document =
            OmsiConfigParser.Parse(source);

        var result =
            OmsiTileObjectInserter.Append(
                document,
                new OmsiNewPlacedObject(
                    "0",
                    @"Sceneryobjects\Pack\Tree.sco",
                    121,
                    12.5,
                    22.75,
                    1.25,
                    45,
                    0,
                    0,
                    ["4", "Tree.tga"]));

        var text =
            Encoding.UTF8.GetString(
                result.Bytes);

        Assert.Equal(
            0,
            result.SourceSectionOrdinal);

        Assert.StartsWith(
            source,
            text);

        Assert.Contains(
            "[future_section]\r\nkeep-me\r\n",
            text);

        Assert.Contains(
            "[object]\r\n0\r\n" +
            "Sceneryobjects\\Pack\\Tree.sco\r\n" +
            "121\r\n12.5\r\n22.75\r\n1.25\r\n45\r\n0\r\n0\r\n" +
            "4\r\nTree.tga\r\n",
            text);
    }

    [Fact]
    public void Inserter_AppendsBatchWithStableOrdinals()
    {
        const string source =
            "[object]\r\n0\r\n" +
            "Sceneryobjects\\Pack\\Existing.sco\r\n" +
            "10\r\n0\r\n0\r\n0\r\n0\r\n0\r\n0\r\n";

        var document =
            OmsiConfigParser.Parse(source);

        var result =
            OmsiTileObjectInserter.AppendMany(
                document,
                [
                    new OmsiNewPlacedObject(
                        "0",
                        @"Sceneryobjects\Pack\Tree.sco",
                        11,
                        1,
                        2,
                        3,
                        0,
                        0,
                        0,
                        ["4", "Tree.tga"]),
                    new OmsiNewPlacedObject(
                        "0",
                        @"Sceneryobjects\Pack\Tree.sco",
                        12,
                        4,
                        5,
                        6,
                        90,
                        0,
                        0,
                        ["4", "Tree.tga"])
                ]);

        Assert.Equal(
            new[] { 1, 2 },
            result.SourceSectionOrdinals);

        var text =
            Encoding.UTF8.GetString(
                result.Bytes);

        Assert.Contains(
            "11\r\n1\r\n2\r\n3\r\n",
            text);
        Assert.Contains(
            "12\r\n4\r\n5\r\n6\r\n90\r\n",
            text);
    }

    [Fact]
    public void Inserter_AppendsMixedAssetBatch()
    {
        const string source =
            "[version]\r\n14\r\n";

        var document =
            OmsiConfigParser.Parse(source);

        var result =
            OmsiTileObjectInserter.AppendMany(
                document,
                [
                    new OmsiNewPlacedObject(
                        "0",
                        @"Sceneryobjects\Pack\Tree.sco",
                        1,
                        1,
                        2,
                        0,
                        0,
                        0,
                        0,
                        ["4", "Tree.tga"]),
                    new OmsiNewPlacedObject(
                        "1",
                        @"Sceneryobjects\Pack\Lamp.sco",
                        2,
                        3,
                        4,
                        0,
                        90,
                        0,
                        0,
                        ["lamp-extra"])
                ]);

        var text =
            Encoding.UTF8.GetString(
                result.Bytes);

        Assert.Equal(
            new[] { 0, 1 },
            result.SourceSectionOrdinals);

        Assert.Contains(
            "Sceneryobjects\\Pack\\Tree.sco\r\n1\r\n",
            text);
        Assert.Contains(
            "4\r\nTree.tga\r\n",
            text);
        Assert.Contains(
            "Sceneryobjects\\Pack\\Lamp.sco\r\n2\r\n",
            text);
        Assert.Contains(
            "lamp-extra\r\n",
            text);
    }

    [Fact]
    public void Analyzer_DoesNotInventTemplateForNewAsset()
    {
        var tile = new OmsiTileContent(
            new OmsiTileSummary(
                true,
                1,
                0,
                0),
            [
                new OmsiPlacedObject(
                    "0",
                    @"Sceneryobjects\Pack\Known.sco",
                    9,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    ["0"])
            ],
            []);

        var analysis =
            OmsiObjectInsertionAnalyzer
                .Analyze(
                    [tile],
                    @"Sceneryobjects\Pack\New.sco");

        Assert.Null(
            analysis.MatchingObjectTemplate);

        Assert.Equal(
            10,
            analysis.GetNextId());
    }
}
