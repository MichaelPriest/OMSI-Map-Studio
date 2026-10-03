using MapStudio.Core.Omsi.Splines;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioTunnelSplineGeneratorTests
{
    [Fact]
    public async Task GeneratorCreatesRenderableTunnelSplineWithTrafficPaths()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-Tunnel-" +
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            Directory.CreateDirectory(
                root);

            var result =
                await new MapStudioTunnelSplineGenerator()
                    .GenerateAsync(
                        root,
                        new MapStudioTunnelSpec(
                            "Túnel Central",
                            2,
                            3.5,
                            5.2,
                            0.75,
                            10));

            Assert.True(
                File.Exists(
                    result.SplinePath));

            Assert.Equal(
                3,
                result.TexturePaths.Count);

            Assert.All(
                result.TexturePaths,
                path =>
                    Assert.True(
                        File.Exists(
                            path)));

            var definition =
                await new OmsiSplineDefinitionReader()
                    .ReadAsync(
                        result.SplinePath);

            Assert.True(
                definition.Exists);

            Assert.Equal(
                2,
                definition.Paths.Count);

            Assert.Contains(
                definition.Paths,
                path =>
                    path.Direction ==
                    0);

            Assert.Contains(
                definition.Paths,
                path =>
                    path.Direction ==
                    1);

            Assert.True(
                definition.Surfaces.Count >=
                14);

            var highest =
                definition.Surfaces
                    .SelectMany(
                        surface =>
                            new[]
                            {
                                surface.From.Z,
                                surface.To.Z
                            })
                    .Max();

            Assert.InRange(
                highest,
                5.1,
                5.3);
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
    public async Task GeneratorBacksUpExistingTunnelBeforeRegeneration()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-Tunnel-" +
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            var generator =
                new MapStudioTunnelSplineGenerator();

            var spec =
                new MapStudioTunnelSpec(
                    "Tunnel A",
                    4,
                    3.25,
                    6,
                    1,
                    8);

            var first =
                await generator
                    .GenerateAsync(
                        root,
                        spec);

            await File
                .AppendAllTextAsync(
                    first.SplinePath,
                    "\r\n; local edit");

            var second =
                await generator
                    .GenerateAsync(
                        root,
                        spec);

            Assert.NotNull(
                second.BackupDirectory);

            Assert.True(
                File.Exists(
                    Path.Combine(
                        second.BackupDirectory!,
                        Path.GetFileName(
                            second.SplinePath))));

            var regenerated =
                await File
                    .ReadAllTextAsync(
                        second.SplinePath);

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
                    recursive:
                        true);
            }
        }
    }

    [Fact]
    public void SpecNormalizesUnsafeDimensions()
    {
        var normalized =
            new MapStudioTunnelSpec(
                "",
                99,
                double.NaN,
                1,
                -4,
                1)
                .Normalize();

        Assert.Equal(
            "Tunnel",
            normalized.Name);

        Assert.Equal(
            8,
            normalized.LaneCount);

        Assert.Equal(
            3.5,
            normalized.LaneWidthMeters);

        Assert.Equal(
            0,
            normalized.ShoulderWidthMeters);

        Assert.Equal(
            4,
            normalized.ArchSegments);

        Assert.True(
            normalized.InnerHeightMeters >=
            3.5);
    }
}
