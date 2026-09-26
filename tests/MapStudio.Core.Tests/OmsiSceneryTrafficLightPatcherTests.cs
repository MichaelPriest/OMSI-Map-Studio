using System.Text;
using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Scenery;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiSceneryTrafficLightPatcherTests
{
    [Fact]
    public void PatchReplacesOnlyTrafficProgramSections()
    {
        const string source =
            "[friendlyname]\n" +
            "Crossing\n\n" +
            "[mesh]\n" +
            "model\\cross.o3d\n\n" +
            "[traffic_lights_group]\n" +
            "60\n\n" +
            "[traffic_light]\n" +
            "OldProgram\n\n" +
            "[phase]\n" +
            "0\n30\n\n" +
            "[phase]\n" +
            "6\n30\n\n" +
            "[path]\n" +
            "0\n0\n0\n0\n0\n10\n0\n0\n0\n3\n0\n0\n";

        var document =
            OmsiConfigParser.Parse(
                source);

        var bytes =
            new OmsiSceneryTrafficLightPatcher()
                .Patch(
                    document,
                    [
                        new OmsiTrafficLightController(
                            45,
                            [
                                new OmsiTrafficLightProgram(
                                    "Main",
                                    [
                                        new OmsiTrafficLightPhase(
                                            0,
                                            20),
                                        new OmsiTrafficLightPhase(
                                            6,
                                            20),
                                        new OmsiTrafficLightPhase(
                                            9,
                                            5)
                                    ])
                            ])
                    ]);

        var text =
            Encoding.Latin1
                .GetString(
                    bytes);

        Assert.Contains(
            "[mesh]",
            text);

        Assert.Contains(
            "model\\cross.o3d",
            text);

        Assert.Contains(
            "[path]",
            text);

        Assert.DoesNotContain(
            "OldProgram",
            text);

        Assert.Contains(
            "Main",
            text);

        var metadata =
            OmsiSceneryObjectReader
                .ReadMetadata(
                    OmsiConfigParser
                        .ParseBytes(
                            bytes));

        var controller =
            Assert.Single(
                metadata
                    .TrafficLightControllers);

        Assert.Equal(
            45,
            controller.CycleDuration);

        var program =
            Assert.Single(
                controller.Programs);

        Assert.Equal(
            "Main",
            program.Name);

        Assert.Equal(
            3,
            program.Phases.Count);

        Assert.Single(
            metadata.Paths);
    }
}
