using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Maps;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiTreePlacementTests
{
    [Fact]
    public void ReadObjects_PreservesTreeTextureHeightAndAspect()
    {
        const string source =
            "[object]\n" +
            "0\n" +
            "Sceneryobjects\\Trees_MC\\tree_medium_09.sco\n" +
            "5509\n" +
            "189.866382303691\n" +
            "287.30972414201\n" +
            "-11.9999986090902\n" +
            "2.88000011285823\n" +
            "0\n" +
            "0\n" +
            "4\n" +
            "Tree_Medium_09.tga\n" +
            "12.996\n" +
            "1.438\n";

        var placedObject =
            Assert.Single(
                OmsiTileReader.ReadObjects(
                    OmsiConfigParser.Parse(
                        source)));

        Assert.Equal(
            [
                "4",
                "Tree_Medium_09.tga",
                "12.996",
                "1.438"
            ],
            placedObject.ExtraValues);
    }
}
