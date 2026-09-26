using MapStudio.Core.ProtonBus;
using Xunit;

namespace MapStudio.ProtonBus.Tests;

public sealed class ProtonBusTargetProfileTests
{
    [Fact]
    public void Phase3UsesDocumentedMapModVersion()
    {
        var profile =
            ProtonBusTargetProfiles
                .Phase3;

        Assert.Equal(
            ProtonBusTargetProfiles
                .Phase3Id,
            profile.Id);

        Assert.Equal(
            3,
            profile.MapModVersion);

        Assert.False(
            profile.IsCustom);

        Assert.Empty(
            ProtonBusTargetProfiles
                .Validate(
                    profile));
    }

    [Fact]
    public void Phase3MobileKeepsVersionAndLimitsTextureSize()
    {
        var profile =
            ProtonBusTargetProfiles
                .Phase3Mobile;

        Assert.Equal(
            ProtonBusTargetProfiles
                .Phase3MobileId,
            profile.Id);

        Assert.Equal(
            3,
            profile.MapModVersion);

        Assert.Equal(
            ProtonBusTargetPlatform.Mobile,
            profile.Platform);

        Assert.Equal(
            2048,
            profile.MaxTextureDimension);

        Assert.False(
            profile.IsCustom);

        Assert.Empty(
            ProtonBusTargetProfiles
                .Validate(
                    profile));

        Assert.Same(
            profile,
            ProtonBusTargetProfiles
                .Resolve(
                    ProtonBusTargetProfiles
                        .Phase3MobileId));
    }

    [Fact]
    public void CustomProfileWarnsWhenNotPhase3()
    {
        var profile =
            ProtonBusTargetProfiles
                .Custom(
                    5);

        var issues =
            ProtonBusTargetProfiles
                .Validate(
                    profile);

        Assert.True(
            profile.IsCustom);

        Assert.Equal(
            5,
            profile.MapModVersion);

        Assert.Contains(
            issues,
            issue =>
                issue.Code ==
                "PBPROFILE_CUSTOM_UNVALIDATED");

        Assert.Contains(
            issues,
            issue =>
                issue.Code ==
                "PBPROFILE_VERSION_NOT_PHASE3");
    }

    [Fact]
    public void ApplySetsManifestVersion()
    {
        var definition =
            new ProtonBusMapDefinition(
                "Mapa",
                "Base",
                "Rota");

        var result =
            ProtonBusTargetProfiles
                .Apply(
                    definition,
                    ProtonBusTargetProfiles
                        .Custom(
                            7));

        Assert.Equal(
            7,
            result.MapModVersion);

        Assert.Contains(
            "mapModVersion=7",
            ProtonBusMapDefinitionWriter
                .Serialize(
                    result),
            StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveRejectsUnknownProfile()
    {
        Assert.Throws<
            ArgumentException>(
                () =>
                    ProtonBusTargetProfiles
                        .Resolve(
                            "unknown"));
    }
}
