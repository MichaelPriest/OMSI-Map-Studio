using System.Text;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.ProtonBus;
using Xunit;

namespace MapStudio.ProtonBus.Tests;

public sealed class ProtonBusOmsiTrafficLightPackageIntegrationTests
{
    [Fact]
    public async Task ExporterConvertsScoTrafficMachineAndTriggerMarkers()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudioProtonBusTrafficPackageTests",
                Guid.NewGuid()
                    .ToString("N"));

        var output =
            Path.Combine(
                root,
                "export");

        try
        {
            var sceneryDirectory =
                Path.Combine(
                    root,
                    "Sceneryobjects",
                    "Test");

            Directory.CreateDirectory(
                sceneryDirectory);

            await File.WriteAllTextAsync(
                Path.Combine(
                    sceneryDirectory,
                    "traffic.sco"),
                CreateTrafficSco());

            var placed =
                new OmsiPlacedObject(
                    HeaderValue:
                        "object",
                    SceneryObjectPath:
                        @"Sceneryobjects\Test\traffic.sco",
                    ObjectId:
                        50,
                    X:
                        10,
                    Y:
                        20,
                    Z:
                        0,
                    Rotation:
                        0,
                    Pitch:
                        0,
                    Bank:
                        0,
                    ExtraValues:
                        []);

            var tile =
                new OmsiTileReference(
                    0,
                    0,
                    "tile_0_0.map");

            var content =
                new OmsiTileContent(
                    new(
                        true,
                        ObjectCount:
                            1,
                        SplineCount:
                            0,
                        SplineAttachmentCount:
                            0),
                    [
                        placed
                    ],
                    []);

            var result =
                await new ProtonBusOmsiMapPackageExporter()
                    .ExportAsync(
                        root,
                        output,
                        new(
                            "Mapa",
                            "Mapa",
                            "Rota"),
                        [
                            new(
                                tile,
                                content)
                        ]);

            Assert.True(
                result.IsExported);

            Assert.NotNull(
                result.Package);

            var tileResult =
                Assert.Single(
                    result.Tiles);

            Assert.NotNull(
                tileResult.TrafficLights);

            var machine =
                Assert.Single(
                    tileResult
                        .TrafficLights!
                        .TrafficLights);

            Assert.Equal(
                "tl_t0_0_o50_c0",
                machine.Prefix);

            Assert.Equal(
                2,
                machine.PathCount);

            var machinePath =
                Assert.Single(
                    result.Package!
                        .TrafficLightPaths);

            Assert.EndsWith(
                Path.Combine(
                    "trafficlights",
                    "tl_t0_0_o50_c0.txt"),
                machinePath,
                StringComparison
                    .OrdinalIgnoreCase);

            var machineText =
                File.ReadAllText(
                    machinePath);

            Assert.Contains(
                "howManyPaths=2",
                machineText,
                StringComparison.Ordinal);

            Assert.Contains(
                "path1_green=1",
                machineText,
                StringComparison.Ordinal);

            Assert.Contains(
                "path2_trigger=1",
                machineText,
                StringComparison.Ordinal);

            var modelPath =
                Assert.Single(
                    result.Package
                        .ModelPaths);

            var bytes =
                File.ReadAllBytes(
                    modelPath);

            AssertContainsAsciiCString(
                bytes,
                "_tl_t0_0_o50_c0_path1_trigger_");

            AssertContainsAsciiCString(
                bytes,
                "_tl_t0_0_o50_c0_path2_trigger_");

            Assert.DoesNotContain(
                result.Issues,
                issue =>
                    issue.Code.StartsWith(
                        "trafficLight",
                        StringComparison.Ordinal));
        }
        finally
        {
            if (
                Directory.Exists(
                    root))
            {
                Directory.Delete(
                    root,
                    recursive:
                        true);
            }
        }
    }

    [Fact]
    public async Task TileExporterAlsoPackagesConvertedTrafficLights()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudioProtonBusTrafficTileTests",
                Guid.NewGuid()
                    .ToString("N"));

        var output =
            Path.Combine(
                root,
                "export");

        try
        {
            var sceneryDirectory =
                Path.Combine(
                    root,
                    "Sceneryobjects",
                    "Test");

            Directory.CreateDirectory(
                sceneryDirectory);

            await File.WriteAllTextAsync(
                Path.Combine(
                    sceneryDirectory,
                    "traffic.sco"),
                CreateTrafficSco());

            var tile =
                new OmsiTileReference(
                    0,
                    0,
                    "tile_0_0.map");

            var content =
                new OmsiTileContent(
                    new(
                        true,
                        ObjectCount:
                            1,
                        SplineCount:
                            0,
                        SplineAttachmentCount:
                            0),
                    [
                        new(
                            HeaderValue:
                                "object",
                            SceneryObjectPath:
                                @"Sceneryobjects\Test\traffic.sco",
                            ObjectId:
                                50,
                            X:
                                10,
                            Y:
                                20,
                            Z:
                                0,
                            Rotation:
                                0,
                            Pitch:
                                0,
                            Bank:
                                0,
                            ExtraValues:
                                [])
                    ],
                    []);

            var result =
                await new ProtonBusOmsiTilePackageExporter()
                    .ExportAsync(
                        root,
                        output,
                        new(
                            "Mapa",
                            "Mapa",
                            "Rota"),
                        tile,
                        content);

            Assert.True(
                result.IsExported);

            Assert.NotNull(
                result.Package);

            Assert.NotNull(
                result.TrafficLights);

            var machine =
                Assert.Single(
                    result
                        .TrafficLights!
                        .TrafficLights);

            Assert.Equal(
                "tl_t0_0_o50_c0",
                machine.Prefix);

            var path =
                Assert.Single(
                    result
                        .Package!
                        .TrafficLightPaths);

            Assert.True(
                File.Exists(
                    path));

            Assert.Contains(
                "prefix=tl_t0_0_o50_c0",
                File.ReadAllText(
                    path),
                StringComparison.Ordinal);

            Assert.DoesNotContain(
                result.Issues,
                issue =>
                    issue.Code.StartsWith(
                        "trafficLight",
                        StringComparison.Ordinal));
        }
        finally
        {
            if (
                Directory.Exists(
                    root))
            {
                Directory.Delete(
                    root,
                    recursive:
                        true);
            }
        }
    }

    private static string CreateTrafficSco() =>
        string.Join(
            Environment.NewLine,
            [
                "[friendlyname]",
                "Traffic Test",
                "[traffic_lights_group]",
                "60",
                "[traffic_light]",
                "Main",
                "[phase]",
                "3",
                "2",
                "[phase]",
                "6",
                "25",
                "[phase]",
                "9",
                "3",
                "[phase]",
                "0",
                "30",
                "[traffic_light]",
                "Side",
                "[phase]",
                "0",
                "30",
                "[phase]",
                "3",
                "2",
                "[phase]",
                "6",
                "25",
                "[phase]",
                "9",
                "3",
                "[path]",
                "1",
                "0",
                "0.1",
                "0",
                "0",
                "5",
                "0",
                "0",
                "0",
                "3",
                "0",
                "0",
                "[use_traffic_light]",
                "0",
                "[path]",
                "5",
                "0",
                "0.1",
                "0",
                "0",
                "5",
                "0",
                "0",
                "0",
                "3",
                "0",
                "0",
                "[use_traffic_light]",
                "1"
            ]);

    private static void AssertContainsAsciiCString(
        byte[] bytes,
        string value)
    {
        var needle =
            Encoding.ASCII
                .GetBytes(
                    value +
                    "\0");

        Assert.True(
            bytes
                .AsSpan()
                .IndexOf(
                    needle) >=
            0);
    }
}
