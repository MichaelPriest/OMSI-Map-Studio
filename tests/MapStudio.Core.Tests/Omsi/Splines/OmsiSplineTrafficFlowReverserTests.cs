using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Splines;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiSplineTrafficFlowReverserTests
{
    [Fact]
    public void ReverseVehiclePathsFlipsOnlyOneWayVehiclePaths()
    {
        const string source =
            "[path]\n0\n-3.5\n0\n2.5\n0\n" +
            "[path]\n0\n3.5\n0\n2.5\n1\n" +
            "[path]\n0\n0\n0\n2.5\n2\n" +
            "[path]\n1\n0\n0\n1.0\n0\n";

        var result =
            OmsiSplineTrafficFlowReverser
                .ReverseVehiclePaths(
                    OmsiConfigParser.Parse(
                        source));

        Assert.Equal(
            2,
            result.ReversedPathCount);

        var paths =
            new OmsiSplineDefinitionReader()
                .Read(
                    OmsiConfigParser
                        .ParseBytes(
                            result.Bytes))
                .Paths;

        Assert.Equal(
            1,
            paths[0].Direction);

        Assert.Equal(
            0,
            paths[1].Direction);

        Assert.Equal(
            2,
            paths[2].Direction);

        Assert.Equal(
            0,
            paths[3].Direction);
    }
}
