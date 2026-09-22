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
    public void AppendDuplicateKeepsExistingSplinePathIndexesStable()
    {
        const string source =
            "[path]\n" +
            "0\n-1.5\n0.1\n3\n0\n" +
            "[path]\n" +
            "1\n4.2\n0.25\n2\n2\n" +
            "[profile]\n0\n";

        var document =
            OmsiConfigParser.Parse(
                source);

        var duplicate =
            new OmsiSplinePathDefinition(
                0,
                1.5,
                0.1,
                3,
                1);

        var bytes =
            new OmsiSplinePathPatcher()
                .AppendDuplicate(
                    document,
                    0,
                    duplicate);

        var definition =
            new OmsiSplineDefinitionReader()
                .Read(
                    OmsiConfigParser
                        .ParseBytes(
                            bytes));

        Assert.Equal(
            3,
            definition.Paths.Count);

        Assert.Equal(
            -1.5,
            definition.Paths[0]
                .X);

        Assert.Equal(
            4.2,
            definition.Paths[1]
                .X);

        Assert.Equal(
            duplicate,
            definition.Paths[2]);
    }

    [Fact]
    public void RemoveDeletesSelectedSplinePathAndKeepsFollowingProfile()
    {
        const string source =
            "[path]\n" +
            "0\n-1.5\n0.1\n3\n0\n" +
            "KEEP_WITH_REMOVED_PATH\n\n" +
            "[path]\n" +
            "1\n4.2\n0.25\n2\n2\n" +
            "[profile]\n0\n";

        var bytes =
            new OmsiSplinePathPatcher()
                .Remove(
                    OmsiConfigParser.Parse(
                        source),
                    0);

        var text =
            Encoding.UTF8
                .GetString(
                    bytes);

        Assert.DoesNotContain(
            "KEEP_WITH_REMOVED_PATH",
            text);

        Assert.Contains(
            "[profile]\n0",
            text);

        var definition =
            new OmsiSplineDefinitionReader()
                .Read(
                    OmsiConfigParser
                        .ParseBytes(
                            bytes));

        var remaining =
            Assert.Single(
                definition.Paths);

        Assert.Equal(
            4.2,
            remaining.X);
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
