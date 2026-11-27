using System.Text;
using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Maps;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiSplineLinkEditorTests
{
    [Fact]
    public void RewritesPreviousAndNextWithoutTouchingGeometry()
    {
        const string source =
            "[version]\r\n14\r\n" +
            "[spline]\r\n0\r\n" +
            "Splines\\Road.sli\r\n" +
            "10\r\n-1\r\n-1\r\n" +
            "1\r\n2\r\n3\r\n45\r\n20\r\n0\r\n1\r\n1\r\n" +
            "0\r\n0\r\n0\r\n0\r\n0\r\n";

        var document =
            OmsiConfigParser.Parse(
                source);

        var result =
            OmsiTileSplineLinkEditor
                .ApplyLinks(
                    document,
                    [
                        new OmsiSplineLinkEdit(
                            0,
                            @"Splines\Road.sli",
                            10,
                            -1,
                            -1,
                            false,
                            7,
                            11)
                    ]);

        var text =
            Encoding.UTF8.GetString(
                result.Bytes);

        Assert.Equal(
            1,
            result.AppliedEdits);

        Assert.Contains(
            "10\r\n7\r\n11\r\n1\r\n2\r\n3\r\n45\r\n20\r\n0\r\n1\r\n1\r\n",
            text);
    }

    [Fact]
    public void RejectsChangedSourceLinks()
    {
        const string source =
            "[version]\n14\n" +
            "[spline]\n0\n" +
            "Splines\\Road.sli\n" +
            "10\n5\n9\n" +
            "0\n0\n0\n0\n10\n0\n0\n0\n" +
            "0\n0\n0\n0\n0\n";

        var document =
            OmsiConfigParser.Parse(
                source);

        Assert.Throws<
            InvalidDataException>(
            () =>
                OmsiTileSplineLinkEditor
                    .ApplyLinks(
                        document,
                        [
                            new OmsiSplineLinkEdit(
                                0,
                                @"Splines\Road.sli",
                                10,
                                -1,
                                -1,
                                false,
                                -1,
                                11)
                        ]));
    }
}
