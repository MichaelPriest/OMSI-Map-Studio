using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Maps;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiSplineAdvancedEditorTests
{
    [Fact]
    public void ApplyAddsCantAndMirrorWithoutChangingBaseTransform()
    {
        const string source =
            "[version]\n14\n" +
            "[spline]\n" +
            "0\nSplines\\Road.sli\n" +
            "10\n-1\n-1\n" +
            "1\n2\n3\n45\n20\n0\n1\n2\n";

        var result =
            OmsiTileSplineAdvancedEditor
                .Apply(
                    OmsiConfigParser.Parse(
                        source),
                    [
                        new OmsiSplineAdvancedEdit(
                            0,
                            @"Splines\Road.sli",
                            10,
                            -1,
                            -1,
                            false,
                            3.5,
                            -2.25,
                            true)
                    ]);

        var text =
            System.Text.Encoding.UTF8
                .GetString(
                    result.Bytes);

        Assert.Contains(
            "1\n2\n3\n45\n20\n0\n1\n2\n3.5\n-2.25\n0\n0\n0\nmirror",
            text);
    }

    [Fact]
    public void ApplyUsesHeightSplineDeltaBeforeCant()
    {
        const string source =
            "[version]\n14\n" +
            "[spline_h]\n" +
            "0\nSplines\\Hill.sli\n" +
            "11\n-1\n-1\n" +
            "0\n0\n0\n0\n30\n0\n5\n5\n" +
            "1.5\n0\n0\n0\n0\n0\n";

        var result =
            OmsiTileSplineAdvancedEditor
                .Apply(
                    OmsiConfigParser.Parse(
                        source),
                    [
                        new OmsiSplineAdvancedEdit(
                            0,
                            @"Splines\Hill.sli",
                            11,
                            -1,
                            -1,
                            true,
                            4,
                            6,
                            false)
                    ]);

        var document =
            OmsiConfigParser.ParseBytes(
                result.Bytes);

        var spline =
            Assert.Single(
                OmsiTileReader
                    .ReadSplines(
                        document));

        Assert.Equal(
            1.5,
            double.Parse(
                spline.ExtraValues[0],
                System.Globalization
                    .CultureInfo.InvariantCulture));

        Assert.Equal(
            4,
            spline.CantStart);

        Assert.Equal(
            6,
            spline.CantEnd);
    }
}
