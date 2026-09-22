using System.Text;
using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Splines;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiSplinePathPatcherTests
{
    [Fact]
    public void PatchUpdatesOnlySelectedPathAndPreservesUnknownBodyData()
    {
        const string source =
            "[path]\n" +
            "# first lane\n" +
            "0\n-1.5\n0.1\n3\n0\n" +
            "KEEP_EXTRA\n\n" +
            "[path]\n" +
            "1\n4.2\n0.25\n2\n2\n";

        var document =
            OmsiConfigParser.Parse(
                source);

        var updated =
            new OmsiSplinePathDefinition(
                2,
                -2.25,
                0.35,
                3.5,
                1);

        var bytes =
            new OmsiSplinePathPatcher()
                .Patch(
                    document,
                    0,
                    updated);

        var text =
            Encoding.UTF8
                .GetString(
                    bytes);

        Assert.Contains(
            "# first lane",
            text);

        Assert.Contains(
            "KEEP_EXTRA",
            text);

        var definition =
            new OmsiSplineDefinitionReader()
                .Read(
                    OmsiConfigParser
                        .ParseBytes(
                            bytes));

        Assert.Equal(
            2,
            definition.Paths.Count);

        Assert.Equal(
            updated,
            definition.Paths[0]);

        Assert.Equal(
            1,
            definition.Paths[1]
                .Type);

        Assert.Equal(
            4.2,
            definition.Paths[1]
                .X);
    }

    [Fact]
    public void PatchPreservesEncodingAndNewline()
    {
        const string source =
            "[path]\r\n" +
            "0\r\n0\r\n0\r\n3\r\n0\r\n";

        var document =
            OmsiConfigParser.ParseBytes(
                Encoding.GetEncoding(
                    1252)
                    .GetBytes(
                        source));

        var bytes =
            new OmsiSplinePathPatcher()
                .Patch(
                    document,
                    0,
                    new OmsiSplinePathDefinition(
                        0,
                        1.25,
                        0.2,
                        3.25,
                        2));

        var text =
            Encoding.GetEncoding(
                1252)
                .GetString(
                    bytes);

        Assert.Contains(
            "\r\n",
            text);

        Assert.DoesNotContain(
            "\n",
            text.Replace(
                "\r\n",
                string.Empty,
                StringComparison.Ordinal));
    }

    [Fact]
    public void PatchRejectsInvalidPath()
    {
        var document =
            OmsiConfigParser.Parse(
                "[path]\n0\n0\n0\n3\n0\n");

        var exception =
            Assert.Throws<
                InvalidDataException>(
                    () =>
                        new OmsiSplinePathPatcher()
                            .Patch(
                                document,
                                0,
                                new OmsiSplinePathDefinition(
                                    4,
                                    0,
                                    0,
                                    3,
                                    0)));

        Assert.Equal(
            "splinePathInvalid",
            exception.Message);
    }
}
