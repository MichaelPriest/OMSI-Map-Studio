using MapStudio.Core.Omsi.Scenery;
using MapStudio.Renderer.Viewport;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeTrafficLightProgramInfoTests
{
    [Fact]
    public void PhasePreviewWrapsAcrossDeclaredCycle()
    {
        var info =
            new NativeTrafficLightProgramInfo(
                1,
                0,
                0,
                @"Sceneryobjects\Test\crossing.sco",
                0,
                0,
                "Main",
                10,
                [
                    new OmsiTrafficLightPhase(
                        0,
                        4),
                    new OmsiTrafficLightPhase(
                        6,
                        4),
                    new OmsiTrafficLightPhase(
                        9,
                        2)
                ]);

        Assert.Equal(
            0,
            info.GetPhaseAt(1)!
                .SignalCode);

        Assert.Equal(
            6,
            info.GetPhaseAt(5)!
                .SignalCode);

        Assert.Equal(
            9,
            info.GetPhaseAt(9)!
                .SignalCode);

        Assert.Equal(
            0,
            info.GetPhaseAt(11)!
                .SignalCode);
    }
}
