using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Maps;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiSplineTests
{
    [Fact]
    public void ReadSplines_ExtractsBasePlacementAndCurveData()
    {
        const string source =
            "[spline]\r\n" +
            "0\r\n" +
            "Splines\\Roads\\street.sli\r\n" +
            "120\r\n" +
            "-1\r\n" +
            "121\r\n" +
            "10.5\r\n" +
            "1.25\r\n" +
            "20.75\r\n" +
            "90\r\n" +
            "50\r\n" +
            "-200\r\n" +
            "2\r\n" +
            "4\r\n" +
            "0\r\n" +
            "0\r\n";

        var document =
            OmsiConfigParser.Parse(source);

        var spline =
            Assert.Single(
                OmsiTileReader.ReadSplines(
                    document));

        Assert.Equal(
            "Splines\\Roads\\street.sli",
            spline.SplinePath);

        Assert.Equal(
            120,
            spline.SplineId);

        Assert.Equal(
            -1,
            spline.PreviousSplineId);

        Assert.Equal(
            121,
            spline.NextSplineId);

        Assert.Equal(
            10.5,
            spline.X);

        Assert.Equal(
            1.25,
            spline.Z);

        Assert.Equal(
            20.75,
            spline.Y);

        Assert.Equal(
            90,
            spline.Rotation);

        Assert.Equal(
            50,
            spline.Length);

        Assert.Equal(
            -200,
            spline.Radius);

        Assert.Equal(
            2,
            spline.GradientStart);

        Assert.Equal(
            4,
            spline.GradientEnd);

        Assert.False(
            spline.IsHeightSpline);

        Assert.Equal(
            source,
            document.ToText());
    }

    [Fact]
    public void ReadSplines_RecognizesHeightSpline()
    {
        const string source =
            "[spline_h]\n" +
            "0\n" +
            "Splines\\Roads\\hill.sli\n" +
            "1\n" +
            "-1\n" +
            "-1\n" +
            "0\n" +
            "2\n" +
            "0\n" +
            "0\n" +
            "30\n" +
            "0\n" +
            "5\n" +
            "5\n" +
            "1.5\n";

        var document =
            OmsiConfigParser.Parse(source);

        var spline =
            Assert.Single(
                OmsiTileReader.ReadSplines(
                    document));

        Assert.True(
            spline.IsHeightSpline);

        Assert.Equal(
            "1.5",
            Assert.Single(
                spline.ExtraValues));
    }
}
