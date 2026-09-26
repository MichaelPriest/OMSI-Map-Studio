using System.IO.Compression;
using System.Text;
using MapStudio.Core.ProtonBus;
using Xunit;

namespace MapStudio.ProtonBus.Tests;

public sealed class ProtonBusValidationFixtureBuilderTests
{
    [Fact]
    public void BuilderCreatesCompleteValidationPackageAndZip()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudioProtonBusFixtureTests",
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            var result =
                ProtonBusValidationFixtureBuilder
                    .Build(
                        root);

            Assert.Equal(
                3,
                result.Definition
                    .MapModVersion);

            Assert.True(
                File.Exists(
                    result.Package
                        .MapDefinitionPath));

            Assert.EndsWith(
                Path.Combine(
                    "maps",
                    "MapStudioValidation.map.txt"),
                result.Package
                    .MapDefinitionPath,
                StringComparison
                    .OrdinalIgnoreCase);

            var model =
                Assert.Single(
                    result.Package
                        .ModelPaths);

            Assert.True(
                File.Exists(
                    model));

            Assert.Single(
                result.Package
                    .VehiclePathPaths);

            Assert.Single(
                result.Package
                    .PedestrianPathPaths);

            Assert.Single(
                result.Package
                    .TrainPathPaths);

            Assert.Single(
                result.Package
                    .BusStopPaths);

            Assert.Single(
                result.Package
                    .TrafficLightPaths);

            Assert.Single(
                result.Package
                    .StreetLightPaths);

            Assert.NotNull(
                result.Package
                    .EntrypointsPath);

            Assert.NotNull(
                result.Package
                    .EntrypointsListPath);

            Assert.NotNull(
                result.ArchivePath);

            Assert.True(
                File.Exists(
                    result.ArchivePath!));

            var modelBytes =
                File.ReadAllBytes(
                    model);

            AssertContainsAscii(
                modelBytes,
                "validation_ground_gencol_");

            AssertContainsAscii(
                modelBytes,
                "validation_vehicle.000");

            AssertContainsAscii(
                modelBytes,
                "validation_people.000");

            AssertContainsAscii(
                modelBytes,
                "validation_train.000");

            AssertContainsAscii(
                modelBytes,
                "validation_stop_trigger");

            AssertContainsAscii(
                modelBytes,
                "_gps_Validation Line_");

            AssertContainsAscii(
                modelBytes,
                "_validation_signal_path1_trigger_");

            AssertContainsAscii(
                modelBytes,
                "_validation_signal_path1_red_additive_");

            AssertContainsAscii(
                modelBytes,
                "_validation_signal_path1_yellow_additive_");

            AssertContainsAscii(
                modelBytes,
                "_validation_signal_path1_green_additive_");

            AssertContainsAscii(
                modelBytes,
                "_validation_light_real_");

            using var archive =
                ZipFile.OpenRead(
                    result.ArchivePath!);

            var entries =
                archive
                    .Entries
                    .Select(
                        entry =>
                            entry.FullName)
                    .ToHashSet(
                        StringComparer
                            .OrdinalIgnoreCase);

            Assert.Contains(
                "maps/MapStudioValidation.map.txt",
                entries);

            Assert.Contains(
                "maps/MapStudioValidation/tiles/validation/validation.3ds",
                entries);

            Assert.Contains(
                "maps/MapStudioValidation/tiles/validation/aivehicles/validation_vehicle.txt",
                entries);

            Assert.Contains(
                "maps/MapStudioValidation/tiles/validation/aipeople/validation_people.txt",
                entries);

            Assert.Contains(
                "maps/MapStudioValidation/tiles/validation/aitrains/validation_train.txt",
                entries);

            Assert.Contains(
                "maps/MapStudioValidation/tiles/validation/busstops/validation_stop.txt",
                entries);

            Assert.Contains(
                "maps/MapStudioValidation/tiles/validation/trafficlights/validation_signal.txt",
                entries);

            Assert.Contains(
                "maps/MapStudioValidation/tiles/validation/streetlights/validation_light.txt",
                entries);

            Assert.Contains(
                "maps/MapStudioValidation/tiles/validation/entrypoints.txt",
                entries);

            Assert.Contains(
                "maps/MapStudioValidation/tiles/validation/entrypoints_list.txt",
                entries);
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
    public void BuilderCanSkipArchive()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudioProtonBusFixtureTests",
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            var result =
                ProtonBusValidationFixtureBuilder
                    .Build(
                        root,
                        createZipArchive:
                            false);

            Assert.Null(
                result.ArchivePath);

            Assert.True(
                File.Exists(
                    result.Package
                        .MapDefinitionPath));
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

    private static void AssertContainsAscii(
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
            0,
            $"Expected 3DS to contain object name '{value}'.");
    }
}
