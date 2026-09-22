using System.Text;
using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Scenery;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiSceneryPathPatcherTests
{
    [Fact]
    public void PatchUpdatesOnlySelectedPathAndPreservesUnknownModifier()
    {
        const string source =
            "[friendlyname]\n" +
            "Crossing\n\n" +
            "[path]\n" +
            "# keep this comment\n" +
            "0\n0\n0\n0\n0\n10\n0\n0\n2\n3\n0\n0\n\n" +
            "[custom_path_modifier]\n" +
            "KEEP_ME\n\n" +
            "[use_traffic_light]\n" +
            "1\n\n" +
            "[path]\n" +
            "5\n6\n7\n8\n9\n20\n1\n2\n1\n4\n1\n3\n";

        var document =
            OmsiConfigParser.Parse(
                source);

        var updated =
            new OmsiSceneryPathDefinition(
                1.25,
                2.5,
                3.75,
                45,
                12,
                30,
                0.1,
                0.2,
                2,
                3.5,
                1,
                4,
                3,
                -1,
                true);

        var bytes =
            new OmsiSceneryPathPatcher()
                .Patch(
                    document,
                    0,
                    updated);

        var text =
            Encoding.UTF8
                .GetString(
                    bytes);

        Assert.Contains(
            "# keep this comment",
            text);

        Assert.Contains(
            "[custom_path_modifier]\nKEEP_ME",
            text);

        Assert.Contains(
            "[use_traffic_light]\n3",
            text);

        Assert.Contains(
            "[switchdir]\n-1",
            text);

        Assert.Contains(
            "[crossingproblem]",
            text);

        var parsed =
            OmsiConfigParser.ParseBytes(
                bytes);

        var metadata =
            OmsiSceneryObjectReader
                .ReadMetadata(
                    parsed);

        Assert.Equal(
            2,
            metadata.Paths.Count);

        Assert.Equal(
            updated,
            metadata.Paths[0]);

        Assert.Equal(
            5,
            metadata.Paths[1].X);

        Assert.Equal(
            20,
            metadata.Paths[1].Length);
    }

    [Fact]
    public void PatchCanRemoveKnownOptionalModifiers()
    {
        const string source =
            "[path]\n" +
            "0\n0\n0\n0\n0\n10\n0\n0\n2\n3\n0\n0\n\n" +
            "[use_traffic_light]\n" +
            "2\n\n" +
            "[switchdir]\n" +
            "1\n\n" +
            "[crossingproblem]\n\n" +
            "[mesh]\n" +
            "model.o3d\n";

        var document =
            OmsiConfigParser.Parse(
                source);

        var existing =
            Assert.Single(
                OmsiSceneryObjectReader
                    .ReadMetadata(
                        document)
                    .Paths);

        var updated =
            existing with
            {
                TrafficLightIndex =
                    null,
                SwitchDirection =
                    null,
                CrossingProblem =
                    false
            };

        var bytes =
            new OmsiSceneryPathPatcher()
                .Patch(
                    document,
                    0,
                    updated);

        var text =
            Encoding.UTF8
                .GetString(
                    bytes);

        Assert.DoesNotContain(
            "[use_traffic_light]",
            text);

        Assert.DoesNotContain(
            "[switchdir]",
            text);

        Assert.DoesNotContain(
            "[crossingproblem]",
            text);

        Assert.Contains(
            "[mesh]",
            text);

        Assert.Equal(
            updated,
            Assert.Single(
                OmsiSceneryObjectReader
                    .ReadMetadata(
                        OmsiConfigParser
                            .ParseBytes(
                                bytes))
                    .Paths));
    }

    [Fact]
    public void InsertAfterAddsNewPathWithoutChangingExistingOnes()
    {
        const string source =
            "[path]\n" +
            "0\n0\n0\n0\n0\n10\n0\n0\n0\n3\n0\n0\n\n" +
            "[mesh]\nmodel.o3d\n";

        var document =
            OmsiConfigParser.Parse(
                source);

        var duplicate =
            new OmsiSceneryPathDefinition(
                1.5,
                0,
                0,
                0,
                0,
                10,
                0,
                0,
                0,
                3,
                1,
                0,
                null,
                null,
                false);

        var bytes =
            new OmsiSceneryPathPatcher()
                .InsertAfter(
                    document,
                    0,
                    duplicate);

        var metadata =
            OmsiSceneryObjectReader
                .ReadMetadata(
                    OmsiConfigParser
                        .ParseBytes(
                            bytes));

        Assert.Equal(
            2,
            metadata.Paths.Count);

        Assert.Equal(
            0,
            metadata.Paths[0]
                .X);

        Assert.Equal(
            duplicate,
            metadata.Paths[1]);
    }

    [Fact]
    public void PatchRejectsMissingOrdinal()
    {
        var document =
            OmsiConfigParser.Parse(
                "[path]\n0\n0\n0\n0\n0\n10\n0\n0\n2\n3\n0\n0\n");

        var path =
            Assert.Single(
                OmsiSceneryObjectReader
                    .ReadMetadata(
                        document)
                    .Paths);

        var exception =
            Assert.Throws<
                InvalidDataException>(
                    () =>
                        new OmsiSceneryPathPatcher()
                            .Patch(
                                document,
                                1,
                                path));

        Assert.Equal(
            "sceneryPathOrdinalInvalid",
            exception.Message);
    }
}
