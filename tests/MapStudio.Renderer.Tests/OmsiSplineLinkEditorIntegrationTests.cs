using System.Text;
using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Maps;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class
    OmsiSplineLinkEditorIntegrationTests
{
    [Fact]
    public void LinkEditorPreservesSplineGeometryFields()
    {
        const string source =
            "[version]\r\n14\r\n" +
            "[spline]\r\n0\r\n" +
            "Splines\\Road.sli\r\n" +
            "10\r\n-1\r\n-1\r\n" +
            "1\r\n3\r\n2\r\n45\r\n20\r\n50\r\n1\r\n2\r\n" +
            "0\r\n0\r\n0\r\n0\r\n0\r\n";

        var result =
            OmsiTileSplineLinkEditor.ApplyLinks(
                OmsiConfigParser.Parse(
                    source),
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
                ]);

        var text =
            Encoding.UTF8.GetString(
                result.Bytes);

        Assert.Contains(
            "10\r\n-1\r\n11\r\n1\r\n3\r\n2\r\n45\r\n20\r\n50\r\n1\r\n2\r\n",
            text);
    }
}
