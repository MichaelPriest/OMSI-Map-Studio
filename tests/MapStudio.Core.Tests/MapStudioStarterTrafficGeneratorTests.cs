using MapStudio.Core.Omsi.Models;
using MapStudio.Core.Omsi.Props;
using MapStudio.Core.Omsi.Scenery;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioStarterTrafficGeneratorTests
{
    [Fact]
    public async Task EnsureCreatesTrafficLightWithEditableProgramAndPath()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-Traffic-" +
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            var generator =
                new MapStudioStarterTrafficGenerator();

            Assert.True(
                await generator
                    .EnsureAsync(
                        root));

            var directory =
                Path.Combine(
                    root,
                    "Sceneryobjects",
                    MapStudioStarterTrafficGenerator
                        .RootFolderName,
                    "Starter_TrafficLight");

            var scoPath =
                Path.Combine(
                    directory,
                    "starter_trafficlight.sco");

            var metadata =
                await new OmsiSceneryObjectReader()
                    .ReadMetadataAsync(
                        scoPath);

            Assert.True(
                metadata
                    .IsTrafficLightObject);

            var controller =
                Assert.Single(
                    metadata
                        .TrafficLightControllers);

            Assert.Equal(
                60,
                controller.CycleDuration);

            var program =
                Assert.Single(
                    controller.Programs);

            Assert.Equal(
                "Main",
                program.Name);

            Assert.Equal(
                4,
                program.Phases.Count);

            var path =
                Assert.Single(
                    metadata.Paths);

            Assert.Equal(
                0,
                path.TrafficLightIndex);

            var geometry =
                new OmsiO3dGeometryReader()
                    .Read(
                        Path.Combine(
                            directory,
                            "model",
                            "trafficlight.o3d"));

            Assert.True(
                geometry.IsLoaded);

            Assert.True(
                geometry.Indices.Length >
                0);

            Assert.All(
                geometry.Materials,
                material =>
                    Assert.False(
                        string.IsNullOrWhiteSpace(
                            material.TextureName)));

            Assert.True(
                File.Exists(
                    Path.Combine(
                        directory,
                        "Texture",
                        "ms_traffic_red.bmp")));

            Assert.False(
                await generator
                    .EnsureAsync(
                        root));
        }
        finally
        {
            if (Directory.Exists(
                    root))
            {
                Directory.Delete(
                    root,
                    recursive:
                        true);
            }
        }
    }
}
