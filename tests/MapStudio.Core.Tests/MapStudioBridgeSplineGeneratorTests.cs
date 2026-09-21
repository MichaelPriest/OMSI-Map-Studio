using MapStudio.Core.Omsi.Splines;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioBridgeSplineGeneratorTests
{
    [Fact]
    public void BuildSplineCreatesDeckRoadAndTrafficPaths()
    {
        var source =
            new MapStudioBridgeSplineGenerator()
                .BuildSpline(
                    new MapStudioBridgeSpec(
                        "Test Bridge",
                        2,
                        3.5,
                        1.5,
                        0.55));

        Assert.Contains(
            "[profile]",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            "ms_bridge_asphalt.bmp",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            "ms_bridge_concrete.bmp",
            source,
            StringComparison.Ordinal);

        Assert.Equal(
            2,
            CountOccurrences(
                source,
                "[path]"));
    }

    [Fact]
    public async Task GenerateCreatesStandaloneBridgePack()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-Bridge-" +
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            var result =
                await new MapStudioBridgeSplineGenerator()
                    .GenerateAsync(
                        root,
                        new MapStudioBridgeSpec(
                            "Starter Bridge",
                            2,
                            3.5,
                            1.5,
                            0.55));

            Assert.True(
                File.Exists(
                    result.SplinePath));

            Assert.StartsWith(
                @"Splines\MapStudio_Bridges\",
                result.RelativeSplinePath,
                StringComparison.Ordinal);

            Assert.Equal(
                3,
                result.TexturePaths.Count);

            Assert.All(
                result.TexturePaths,
                path =>
                    Assert.True(
                        File.Exists(
                            path)));
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

    private static int CountOccurrences(
        string value,
        string term)
    {
        var count =
            0;

        var offset =
            0;

        while (
            (
                offset =
                    value.IndexOf(
                        term,
                        offset,
                        StringComparison.Ordinal)
            ) >=
            0)
        {
            count++;
            offset +=
                term.Length;
        }

        return count;
    }
}
