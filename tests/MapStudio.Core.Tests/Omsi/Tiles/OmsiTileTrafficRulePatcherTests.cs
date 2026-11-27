using System.Text;
using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Maps;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiTileTrafficRulePatcherTests
{
    [Fact]
    public void PatchRewritesOnlyRulesOfSelectedObject()
    {
        const string source =
            "[object]\n" +
            "0\nSceneryobjects\\A.sco\n1\n0\n0\n0\n0\n0\n0\n" +
            "[varparent]\n7\n" +
            "[rule]\n0\nspeedlimit\n40\n0\n" +
            "[object]\n" +
            "0\nSceneryobjects\\B.sco\n2\n0\n0\n0\n0\n0\n0\n" +
            "[rule]\n1\ntrafficdensity\n0.5\n4\n";

        var document =
            OmsiConfigParser.Parse(
                source);

        var bytes =
            new OmsiTileTrafficRulePatcher()
                .Patch(
                    document,
                    splineOwner:
                        false,
                    sourceSectionOrdinal:
                        0,
                    [
                        new OmsiTrafficRule(
                            false,
                            2,
                            "priority",
                            "192",
                            192,
                            0,
                            [])
                    ]);

        var text =
            Encoding.Latin1
                .GetString(
                    bytes);

        Assert.Contains(
            "[varparent]",
            text);

        Assert.Contains(
            "priority",
            text);

        Assert.DoesNotContain(
            "speedlimit",
            text);

        Assert.Contains(
            "trafficdensity",
            text);

        var parsed =
            OmsiConfigParser
                .ParseBytes(
                    bytes);

        var objects =
            OmsiTileReader
                .ReadObjects(
                    parsed);

        Assert.Equal(
            "priority",
            objects[0]
                .TrafficRules[0]
                .RuleName);

        Assert.Equal(
            "trafficdensity",
            objects[1]
                .TrafficRules[0]
                .RuleName);
    }

    [Fact]
    public void PatchSupportsSplineOwnerOrdinal()
    {
        const string source =
            "[spline]\n" +
            "0\nSplines\\Road.sli\n10\n-1\n-1\n" +
            "0\n0\n0\n0\n20\n0\n0\n0\n" +
            "[rule]\n0\nspeedlimit\n30\n0\n";

        var document =
            OmsiConfigParser.Parse(
                source);

        var bytes =
            new OmsiTileTrafficRulePatcher()
                .Patch(
                    document,
                    splineOwner:
                        true,
                    sourceSectionOrdinal:
                        0,
                    [
                        new OmsiTrafficRule(
                            false,
                            0,
                            "speedlimit",
                            "50",
                            50,
                            0,
                            [])
                    ]);

        var spline =
            Assert.Single(
                OmsiTileReader
                    .ReadSplines(
                        OmsiConfigParser
                            .ParseBytes(
                                bytes)));

        Assert.Equal(
            "50",
            Assert.Single(
                spline.TrafficRules)
                .RawValue);
    }
}
