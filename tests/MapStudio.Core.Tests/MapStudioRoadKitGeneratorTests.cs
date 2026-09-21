using MapStudio.Core.Omsi.Splines;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioRoadKitGeneratorTests
{
    [Fact]
    public async Task GeneratorCreatesOriginalSplinePackAndBacksUpUpdates()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-RoadKit-" +
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            Directory.CreateDirectory(
                root);

            var generator =
                new MapStudioRoadKitGenerator();

            var first =
                await generator
                    .InstallOrUpdateAsync(
                        root);

            Assert.Equal(
                6,
                first
                    .SplineRelativePaths
                    .Count);

            Assert.Null(
                first.BackupDirectory);

            var reader =
                new OmsiSplineDefinitionReader();

            foreach (
                var relativePath in
                    first
                        .SplineRelativePaths)
            {
                var fullPath =
                    Path.Combine(
                        root,
                        relativePath
                            .Replace(
                                '\\',
                                Path.DirectorySeparatorChar));

                Assert.True(
                    File.Exists(
                        fullPath));

                var definition =
                    await reader
                        .ReadAsync(
                            fullPath);

                Assert.True(
                    definition.Exists);

                Assert.NotEmpty(
                    definition.Surfaces);

                Assert.NotEmpty(
                    definition.Paths);
            }

            var asphalt =
                Path.Combine(
                    first.PackDirectory,
                    "Texture",
                    "ms_asphalt.bmp");

            var header =
                await File
                    .ReadAllBytesAsync(
                        asphalt);

            Assert.True(
                header.Length >
                54);

            Assert.Equal(
                (byte)'B',
                header[0]);

            Assert.Equal(
                (byte)'M',
                header[1]);

            var editedSpline =
                Path.Combine(
                    first.PackDirectory,
                    "ms_road_2lane_7m.sli");

            await File.AppendAllTextAsync(
                editedSpline,
                "\r\n; local edit");

            var second =
                await generator
                    .InstallOrUpdateAsync(
                        root);

            Assert.NotNull(
                second.BackupDirectory);

            Assert.True(
                File.Exists(
                    Path.Combine(
                        second.BackupDirectory!,
                        "ms_road_2lane_7m.sli")));

            var regenerated =
                await File
                    .ReadAllTextAsync(
                        editedSpline);

            Assert.DoesNotContain(
                "local edit",
                regenerated,
                StringComparison.Ordinal);
        }
        finally
        {
            if (
                Directory.Exists(
                    root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }

    [Fact]
    public async Task DividedAvenueProvidesTrafficPedestrianAndMedianProfiles()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-RoadKit-" +
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            var result =
                await new MapStudioRoadKitGenerator()
                    .InstallOrUpdateAsync(
                        root);

            var avenue =
                Path.Combine(
                    result.PackDirectory,
                    "ms_avenue_divided_4lane.sli");

            var definition =
                await new OmsiSplineDefinitionReader()
                    .ReadAsync(
                        avenue);

            Assert.Contains(
                definition.Paths,
                path =>
                    path.Type ==
                    0 &&
                    path.Direction ==
                    0);

            Assert.Contains(
                definition.Paths,
                path =>
                    path.Type ==
                    0 &&
                    path.Direction ==
                    1);

            Assert.Contains(
                definition.Paths,
                path =>
                    path.Type ==
                    1);

            Assert.True(
                definition.Surfaces.Count >=
                5);
        }
        finally
        {
            if (
                Directory.Exists(
                    root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }
}
