using System.Numerics;
using MapStudio.Core.ProtonBus;
using Xunit;

namespace MapStudio.ProtonBus.Tests;

public sealed class ProtonBusTrafficLightDefinitionTests
{
    [Fact]
    public void WriterSerializesMachineTicksAndRealLights()
    {
        var definition = CreateDefinition();

        var text = ProtonBusTrafficLightDefinitionWriter.Serialize(definition);

        Assert.Contains("[trafficlight]", text, StringComparison.Ordinal);
        Assert.Contains("prefix=farol1", text, StringComparison.Ordinal);
        Assert.Contains("howManyPaths=2", text, StringComparison.Ordinal);
        Assert.Contains("howManyTicks=4", text, StringComparison.Ordinal);
        Assert.Contains("tickInterval=3", text, StringComparison.Ordinal);
        Assert.Contains("triggerRadius=1.1", text, StringComparison.Ordinal);
        Assert.Contains("useRealLights=1", text, StringComparison.Ordinal);
        Assert.Contains("randomTimestampAtStart=1", text, StringComparison.Ordinal);
        Assert.Contains("firstTickToRun=1", text, StringComparison.Ordinal);

        Assert.Contains("[green_light]", text, StringComparison.Ordinal);
        Assert.Contains("colorG=1", text, StringComparison.Ordinal);
        Assert.Contains("intensity=1", text, StringComparison.Ordinal);
        Assert.Contains("range=10", text, StringComparison.Ordinal);

        Assert.Contains("[tick1]", text, StringComparison.Ordinal);
        Assert.Contains("repeat=7", text, StringComparison.Ordinal);
        Assert.Contains("path1_green=1", text, StringComparison.Ordinal);
        Assert.Contains("path1_trigger=0", text, StringComparison.Ordinal);
        Assert.Contains("path2_red=1", text, StringComparison.Ordinal);
        Assert.Contains("path2_trigger=1", text, StringComparison.Ordinal);

        Assert.Contains("[tick4]", text, StringComparison.Ordinal);
        Assert.Contains("path2_yellow=1", text, StringComparison.Ordinal);
    }

    [Fact]
    public void DefinitionBuildsDocumented3dMarkerNames()
    {
        var definition = CreateDefinition();

        Assert.Equal(
            "_farol1_path1_red_additive_",
            definition.GetLightObjectName(1, ProtonBusTrafficLightColor.Red));

        Assert.Equal(
            "_farol1_path2_green_additive_",
            definition.GetLightObjectName(2, ProtonBusTrafficLightColor.Green));

        Assert.Equal(
            "_farol1_path2_trigger_",
            definition.GetTriggerObjectName(2));
    }

    [Fact]
    public void WriterRejectsOutOfRangeTickPath()
    {
        var definition = CreateDefinition() with
        {
            Ticks =
            [
                new(
                    1,
                    new Dictionary<int, ProtonBusTrafficLightPathState>
                    {
                        [3] = new(Green: true)
                    })
            ],
            FirstTickToRun = 1
        };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => ProtonBusTrafficLightDefinitionWriter.Serialize(definition));
    }

    [Fact]
    public void WriterRejectsRealLightIntensityAboveOne()
    {
        var definition =
            CreateDefinition() with
            {
                GreenLight =
                    new(
                        new Vector4(
                            0,
                            1,
                            0,
                            1),
                        Intensity:
                            1.1,
                        Range:
                            10)
            };

        Assert.Throws<
            ArgumentOutOfRangeException>(
                () =>
                    ProtonBusTrafficLightDefinitionWriter
                        .Serialize(
                            definition));
    }

    [Fact]
    public void PackageWriterPlacesTrafficLightUnderTrafficlights()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "MapStudioProtonBusTrafficLightTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            var result = ProtonBusMapPackageWriter.Write(
                root,
                new(
                    new("Mapa", "Mapa", "Rota"),
                    [])
                {
                    TrafficLights =
                    [
                        CreateDefinition()
                    ]
                });

            var path = Assert.Single(result.TrafficLightPaths);

            Assert.EndsWith(
                Path.Combine("maps", "Mapa", "tiles", "Rota", "trafficlights", "farol1.txt"),
                path,
                StringComparison.OrdinalIgnoreCase);

            Assert.True(File.Exists(path));
            Assert.Contains("howManyTicks=4", File.ReadAllText(path), StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static ProtonBusTrafficLightDefinition CreateDefinition() =>
        new(
            Prefix: "farol1",
            PathCount: 2,
            TickInterval: 3,
            TriggerRadius: 1.1,
            UseRealLights: true,
            Ticks:
            [
                new(
                    7,
                    new Dictionary<int, ProtonBusTrafficLightPathState>
                    {
                        [1] = new(Green: true),
                        [2] = new(Red: true, Trigger: true)
                    }),
                new(
                    2,
                    new Dictionary<int, ProtonBusTrafficLightPathState>
                    {
                        [1] = new(Yellow: true, Trigger: true),
                        [2] = new(Red: true, Trigger: true)
                    }),
                new(
                    5,
                    new Dictionary<int, ProtonBusTrafficLightPathState>
                    {
                        [1] = new(Red: true, Trigger: true),
                        [2] = new(Green: true)
                    }),
                new(
                    1,
                    new Dictionary<int, ProtonBusTrafficLightPathState>
                    {
                        [1] = new(Red: true, Trigger: true),
                        [2] = new(Yellow: true, Trigger: true)
                    })
            ],
            GreenLight: new(
                new Vector4(0, 1, 0, 1),
                Intensity: 1,
                Range: 10),
            RedLight: new(
                new Vector4(1, 0, 0, 1),
                Intensity: 1,
                Range: 10),
            YellowLight: new(
                new Vector4(1, 1, 0, 1),
                Intensity: 1,
                Range: 10));
}
