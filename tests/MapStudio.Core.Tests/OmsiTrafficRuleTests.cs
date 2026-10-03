using System.Text;
using MapStudio.Core.Omsi.Traffic;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiTrafficRuleTests
{
    [Fact]
    public void PresetsExposeAllOriginalEditorChoices()
    {
        Assert.Equal(
            13,
            OmsiTrafficRulePresets
                .All.Count);

        Assert.Equal(
            "Speed Limit",
            OmsiTrafficRulePresets
                .TryMatch(
                    "speedlimit",
                    40)
                ?.DisplayName);

        Assert.Equal(
            "Higher Priority",
            OmsiTrafficRulePresets
                .TryMatch(
                    "priority",
                    192)
                ?.DisplayName);

        Assert.Equal(
            "No Unscheduled Traffic",
            OmsiTrafficRulePresets
                .TryMatch(
                    "trafficdensity",
                    0)
                ?.DisplayName);
    }

    [Fact]
    public async Task VehicleGroupReaderParsesMapGroups()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-groups-" +
                Guid.NewGuid()
                    .ToString("N"));

        Directory.CreateDirectory(
            root);

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(
                    root,
                    "unsched_vehgroups.txt"),
                "[group]\r\n" +
                "NormalCars\r\n" +
                "1\r\n\r\n" +
                "[group]\r\n" +
                "Trucks\r\n" +
                "0\r\n",
                Encoding.UTF8);

            var groups =
                await new OmsiUnscheduledVehicleGroupReader()
                    .ReadMapAsync(
                        root);

            Assert.Equal(
                2,
                groups.Count);

            Assert.Equal(
                "NormalCars",
                groups[0].Name);

            Assert.Equal(
                1,
                groups[0]
                    .DefaultDensityIndex);

            Assert.Equal(
                0,
                groups[1]
                    .DefaultDensityIndex);
        }
        finally
        {
            Directory.Delete(
                root,
                recursive: true);
        }
    }
}
