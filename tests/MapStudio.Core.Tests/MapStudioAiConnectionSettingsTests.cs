using MapStudio.Core.AI;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioAiConnectionSettingsTests
{
    [Fact]
    public void NormalizeKeepsVendorNeutralConnectionMetadata()
    {
        var settings =
            new MapStudioAiConnectionSettings(
                "local",
                [
                    new MapStudioAiConnectionProfile(
                        " local ",
                        " Local Vision ",
                        " openai-compatible ",
                        "http://127.0.0.1:1234/v1",
                        "vision-model",
                        IsLocal:
                            true)
                ])
                .Normalize();

        var active =
            Assert.Single(
                settings.Profiles);

        Assert.Equal(
            "local",
            active.Id);

        Assert.Equal(
            "Local Vision",
            active.DisplayName);

        Assert.Equal(
            "openai-compatible",
            active.AdapterId);

        Assert.True(
            active.IsLocal);

        Assert.Equal(
            active,
            settings
                .GetActiveProfile());
    }

    [Fact]
    public void NormalizeDropsMissingActiveProfile()
    {
        var settings =
            new MapStudioAiConnectionSettings(
                "missing",
                [
                    new MapStudioAiConnectionProfile(
                        "one",
                        "One",
                        "adapter",
                        null,
                        null)
                ])
                .Normalize();

        Assert.Null(
            settings.ActiveProfileId);

        Assert.Null(
            settings.GetActiveProfile());
    }

    [Fact]
    public void ProfileRejectsNonHttpEndpoint()
    {
        Assert.Throws<
            InvalidDataException>(
                () =>
                    new MapStudioAiConnectionProfile(
                        "one",
                        "One",
                        "adapter",
                        "file:///secret",
                        null)
                    .Normalize());
    }

    [Fact]
    public void DuplicateProfileIdsUseLatestDefinition()
    {
        var settings =
            new MapStudioAiConnectionSettings(
                "same",
                [
                    new MapStudioAiConnectionProfile(
                        "same",
                        "Old",
                        "adapter-a",
                        null,
                        null),
                    new MapStudioAiConnectionProfile(
                        "same",
                        "New",
                        "adapter-b",
                        null,
                        null)
                ])
                .Normalize();

        var profile =
            Assert.Single(
                settings.Profiles);

        Assert.Equal(
            "New",
            profile.DisplayName);

        Assert.Equal(
            "adapter-b",
            profile.AdapterId);
    }
}
