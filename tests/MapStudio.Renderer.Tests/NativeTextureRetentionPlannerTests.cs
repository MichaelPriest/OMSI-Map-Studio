using MapStudio.Renderer.Graphics;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeTextureRetentionPlannerTests
{
    [Fact]
    public void SelectRetained_PrefersMostRecentlyUsedEntries()
    {
        NativeTextureRetentionCandidate[] candidates =
        [
            new("old.dds", 1, 4_000_000),
            new("middle.dds", 2, 4_000_000),
            new("new.dds", 3, 4_000_000)
        ];

        var retained =
            NativeTextureRetentionPlanner
                .SelectRetained(
                    candidates,
                    maxCount: 2,
                    maxBytes:
                        64_000_000);

        Assert.Equal(
            2,
            retained.Count);
        Assert.Contains(
            "new.dds",
            retained);
        Assert.Contains(
            "middle.dds",
            retained);
        Assert.DoesNotContain(
            "old.dds",
            retained);
    }

    [Fact]
    public void SelectRetained_RespectsByteBudgetAndKeepsSmallerFallback()
    {
        NativeTextureRetentionCandidate[] candidates =
        [
            new(
                "new-large.dds",
                30,
                80_000_000),
            new(
                "middle-large.dds",
                20,
                60_000_000),
            new(
                "old-small.dds",
                10,
                10_000_000)
        ];

        var retained =
            NativeTextureRetentionPlanner
                .SelectRetained(
                    candidates,
                    maxCount: 3,
                    maxBytes:
                        100_000_000);

        Assert.Contains(
            "new-large.dds",
            retained);
        Assert.DoesNotContain(
            "middle-large.dds",
            retained);
        Assert.Contains(
            "old-small.dds",
            retained);
    }

    [Fact]
    public void SelectRetained_ZeroLimitsRetainNothing()
    {
        var retained =
            NativeTextureRetentionPlanner
                .SelectRetained(
                    [
                        new(
                            "texture.dds",
                            1,
                            1024)
                    ],
                    maxCount: 0,
                    maxBytes: 0);

        Assert.Empty(
            retained);
    }

    [Fact]
    public void SelectRetained_RejectsInvalidLimits()
    {
        Assert.Throws<
            ArgumentOutOfRangeException>(
            () =>
                NativeTextureRetentionPlanner
                    .SelectRetained(
                        Array.Empty<
                            NativeTextureRetentionCandidate>(),
                        -1,
                        0));

        Assert.Throws<
            ArgumentOutOfRangeException>(
            () =>
                NativeTextureRetentionPlanner
                    .SelectRetained(
                        Array.Empty<
                            NativeTextureRetentionCandidate>(),
                        0,
                        -1));
    }
}
