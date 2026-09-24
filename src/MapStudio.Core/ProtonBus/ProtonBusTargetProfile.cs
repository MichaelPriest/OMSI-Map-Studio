namespace MapStudio.Core.ProtonBus;

public enum ProtonBusTargetPlatform
{
    Pc,
    Mobile
}

public sealed record ProtonBusTargetProfile(
    string Id,
    string DisplayName,
    string Description,
    int MapModVersion,
    ProtonBusTargetPlatform Platform = ProtonBusTargetPlatform.Pc,
    int? MaxTextureDimension = null,
    bool IsCustom = false);

public static class ProtonBusTargetProfiles
{
    public const string Phase3Id =
        "map-mods-phase-3";

    public const string Phase3MobileId =
        "map-mods-phase-3-mobile";

    public const string CustomId =
        "custom";

    public static ProtonBusTargetProfile
        Phase3 { get; } =
        new(
            Phase3Id,
            "Map Mods Phase 3",
            "Perfil documentado para mapModVersion=3 e para os recursos funcionais implementados pelo Map Studio.",
            MapModVersion:
                3);

    public static ProtonBusTargetProfile
        Phase3Mobile { get; } =
        new(
            Phase3MobileId,
            "Map Mods Phase 3 · Mobile",
            "Perfil Phase 3 para Android/mobile. Mantém mapModVersion=3 e limita texturas a no máximo 2048 px por eixo.",
            MapModVersion:
                3,
            Platform:
                ProtonBusTargetPlatform.Mobile,
            MaxTextureDimension:
                2048);

    public static ProtonBusTargetProfile
        Custom(
            int mapModVersion)
    {
        if (
            mapModVersion <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(mapModVersion),
                "mapModVersion must be greater than zero.");
        }

        return new(
            CustomId,
            $"Personalizado (mapModVersion={mapModVersion})",
            "Perfil manual. Exige validação na build Proton Bus alvo antes de distribuir o mapa.",
            mapModVersion,
            IsCustom:
                true);
    }

    public static ProtonBusTargetProfile
        Resolve(
            string? profileId,
            int customMapModVersion = 3) =>
        string.Equals(
            profileId,
            Phase3Id,
            StringComparison.OrdinalIgnoreCase)
            ? Phase3
            : string.Equals(
                profileId,
                Phase3MobileId,
                StringComparison.OrdinalIgnoreCase)
                ? Phase3Mobile
                : string.Equals(
                    profileId,
                    CustomId,
                    StringComparison.OrdinalIgnoreCase)
                    ? Custom(
                        customMapModVersion)
                    : throw new ArgumentException(
                    $"Unknown Proton Bus target profile '{profileId}'.",
                    nameof(profileId));

    public static IReadOnlyList<
        ProtonBusValidationIssue>
        Validate(
            ProtonBusTargetProfile profile)
    {
        ArgumentNullException.ThrowIfNull(
            profile);

        var issues =
            new List<
                ProtonBusValidationIssue>();

        if (
            profile.MapModVersion <=
            0)
        {
            issues.Add(
                new(
                    ProtonBusValidationSeverity.Error,
                    "PBPROFILE_VERSION_INVALID",
                    "The target profile must use a positive mapModVersion."));
        }

        if (
            profile.MaxTextureDimension is
                <= 0)
        {
            issues.Add(
                new(
                    ProtonBusValidationSeverity.Error,
                    "PBPROFILE_TEXTURE_LIMIT_INVALID",
                    "MaxTextureDimension must be greater than zero when configured."));
        }

        if (
            profile.IsCustom)
        {
            issues.Add(
                new(
                    ProtonBusValidationSeverity.Warning,
                    "PBPROFILE_CUSTOM_UNVALIDATED",
                    "Custom Proton Bus profiles are not game-validated by Map Studio; test the exported package in the exact target build."));
        }

        if (
            profile.MapModVersion !=
            3)
        {
            issues.Add(
                new(
                    ProtonBusValidationSeverity.Warning,
                    "PBPROFILE_VERSION_NOT_PHASE3",
                    $"mapModVersion={profile.MapModVersion} differs from the documented Phase 3 profile used by the current exporter."));
        }

        return issues;
    }

    public static ProtonBusMapDefinition
        Apply(
            ProtonBusMapDefinition definition,
            ProtonBusTargetProfile profile)
    {
        ArgumentNullException.ThrowIfNull(
            definition);

        ArgumentNullException.ThrowIfNull(
            profile);

        return definition with
        {
            MapModVersion =
                profile.MapModVersion
        };
    }
}
