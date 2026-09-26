using MapStudio.Core.Omsi.Models;
using MapStudio.Core.Omsi.Props;
using MapStudio.Core.Omsi.Scenery;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioStarterPropGeneratorTests
{
    [Fact]
    public async Task EnsureCreatesEditableOriginalProps()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-Props-" +
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            var generator =
                new MapStudioStarterPropGenerator();

            Assert.True(
                await generator
                    .EnsureAsync(
                        root));

            var propRoot =
                Path.Combine(
                    root,
                    "Sceneryobjects",
                    MapStudioStarterPropGenerator
                        .RootFolderName);

            var lampSco =
                Path.Combine(
                    propRoot,
                    "Starter_Lamp",
                    "starter_lamp.sco");

            var benchSco =
                Path.Combine(
                    propRoot,
                    "Starter_Bench",
                    "starter_bench.sco");

            var busStopSco =
                Path.Combine(
                    propRoot,
                    "Starter_BusStop",
                    "starter_busstop.sco");

            var utilityBoxSco =
                Path.Combine(
                    propRoot,
                    "Starter_UtilityBox",
                    "starter_utilitybox.sco");

            Assert.True(
                File.Exists(
                    lampSco));

            Assert.True(
                File.Exists(
                    benchSco));

            Assert.True(
                File.Exists(
                    busStopSco));

            Assert.True(
                File.Exists(
                    utilityBoxSco));

            var lamp =
                await new OmsiSceneryObjectReader()
                    .ReadMetadataAsync(
                        lampSco);

            Assert.NotEmpty(
                lamp.LightPoints);

            var mesh =
                new OmsiO3dGeometryReader()
                    .Read(
                        Path.Combine(
                            propRoot,
                            "Starter_Lamp",
                            "model",
                            "prop.o3d"));

            Assert.True(
                mesh.IsLoaded);

            Assert.True(
                mesh.Indices.Length >
                0);

            Assert.All(
                mesh.Materials,
                material =>
                    Assert.False(
                        string.IsNullOrWhiteSpace(
                            material.TextureName)));

            Assert.True(
                File.Exists(
                    Path.Combine(
                        propRoot,
                        "Starter_Lamp",
                        "Texture",
                        "ms_prop_metal.bmp")));

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
