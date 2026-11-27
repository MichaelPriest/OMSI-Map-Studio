using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Maps;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiPlacedSplinePathEditorTests
{
    [Fact]
    public void ReplacePathKeepsPlacementAndLinks()
    {
        const string source =
            "[version]\n14\n" +
            "[spline]\n" +
            "0\nSplines\\OldRoad.sli\n" +
            "10\n5\n11\n" +
            "12\n34\n2\n45\n20\n80\n1\n2\n";

        var result =
            OmsiPlacedSplinePathEditor
                .ReplacePath(
                    OmsiConfigParser.Parse(
                        source),
                    0,
                    @"Splines\OldRoad.sli",
                    @"Splines\NewRoad.sli",
                    10,
                    5,
                    11,
                    false);

        var document =
            OmsiConfigParser.ParseBytes(
                result.Bytes);

        var spline =
            Assert.Single(
                OmsiTileReader
                    .ReadSplines(
                        document));

        Assert.Equal(
            @"Splines\NewRoad.sli",
            spline.SplinePath);

        Assert.Equal(
            10,
            spline.SplineId);

        Assert.Equal(
            5,
            spline.PreviousSplineId);

        Assert.Equal(
            11,
            spline.NextSplineId);

        Assert.Equal(
            20,
            spline.Length);

        Assert.Equal(
            80,
            spline.Radius);
    }
}
