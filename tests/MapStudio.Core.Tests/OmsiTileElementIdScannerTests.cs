using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Maps;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiTileElementIdScannerTests
{
    [Fact]
    public void Scanner_ConsidersObjectsSplinesAndAttachments()
    {
        var document =
            OmsiConfigParser.Parse(
                "[object]\n0\nSceneryobjects\\A.sco\n10\n0\n0\n0\n0\n0\n0\n" +
                "[attachObj]\n0\nSceneryobjects\\B.sco\n20\n10\n0\n0\n0\n0\n0\n0\n" +
                "[splineAttachement]\n0\nSceneryobjects\\C.sco\n30\n0\n0\n0\n0\n0\n0\n0\n0\n0\n0\n" +
                "[splineAttachement_repeater]\n0\n7\n4\nSceneryobjects\\D.sco\n40\n0\n0\n0\n0\n0\n0\n0\n0\n0\n0\n" +
                "[spline]\n0\nSplines\\Road.sli\n50\n-1\n-1\n0\n0\n0\n0\n10\n0\n0\n0\n");

        Assert.Equal(
            50,
            OmsiTileElementIdScanner
                .FindMaxUsedId(
                    document));
    }

    [Fact]
    public void Scanner_IgnoresMalformedOrNegativeIds()
    {
        var document =
            OmsiConfigParser.Parse(
                "[object]\n0\nSceneryobjects\\A.sco\nnot-an-id\n0\n0\n0\n0\n0\n0\n" +
                "[spline_h]\n0\nSplines\\Road.sli\n-5\n-1\n-1\n0\n0\n0\n0\n10\n0\n0\n0\n");

        Assert.Equal(
            0,
            OmsiTileElementIdScanner
                .FindMaxUsedId(
                    document));
    }
}
