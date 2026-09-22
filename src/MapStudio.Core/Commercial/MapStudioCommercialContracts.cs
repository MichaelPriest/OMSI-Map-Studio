namespace MapStudio.Core.Commercial;

public enum MapStudioLicenseStatus
{
    DevelopmentPreview,
    Trial,
    Active,
    GracePeriod,
    PastDue,
    Expired,
    Revoked,
    Unavailable
}

public static class MapStudioEntitlementKeys
{
    public const string CoreEditor =
        "editor.core";

    public const string BuildingStudio =
        "creation.building-studio";

    public const string ProceduralRoads =
        "creation.procedural-roads";

    public const string AiAssistance =
        "creation.ai-assistance";

    public const string Omsi2 =
        "simulator.omsi2";

    public const string ProtonBus =
        "simulator.proton-bus";

    public const string Lotus =
        "simulator.lotus";

    public const string All =
        "*";
}

public sealed record MapStudioCommercialState(
    bool EnforcementEnabled,
    MapStudioLicenseStatus Status,
    string? SubscriptionPlanId,
    IReadOnlySet<string> Entitlements,
    DateTimeOffset? ValidUntil = null,
    string? AccountId = null)
{
    public bool HasEntitlement(
        string entitlement)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            entitlement);

        if (!EnforcementEnabled)
        {
            return true;
        }

        if (
            Status is
                MapStudioLicenseStatus.Expired or
                MapStudioLicenseStatus.Revoked or
                MapStudioLicenseStatus.Unavailable)
        {
            return false;
        }

        return
            Entitlements.Contains(
                MapStudioEntitlementKeys
                    .All) ||
            Entitlements.Contains(
                entitlement);
    }

    public static MapStudioCommercialState
        DevelopmentPreview() =>
        new(
            EnforcementEnabled:
                false,
            Status:
                MapStudioLicenseStatus
                    .DevelopmentPreview,
            SubscriptionPlanId:
                null,
            Entitlements:
                new HashSet<string>(
                    StringComparer
                        .OrdinalIgnoreCase)
                {
                    MapStudioEntitlementKeys
                        .All
                });

    public static MapStudioCommercialState
        ProductionUnlicensed() =>
        new(
            EnforcementEnabled:
                true,
            Status:
                MapStudioLicenseStatus
                    .Unavailable,
            SubscriptionPlanId:
                null,
            Entitlements:
                new HashSet<string>(
                    StringComparer
                        .OrdinalIgnoreCase));
}

public sealed record MapStudioFeatureGateResult(
    bool Allowed,
    string? RequiredEntitlement,
    MapStudioLicenseStatus Status,
    string? Reason);

public static class MapStudioFeatureGate
{
    public static MapStudioFeatureGateResult
        Evaluate(
            MapStudioCommercialState state,
            string entitlement)
    {
        ArgumentNullException.ThrowIfNull(
            state);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            entitlement);

        if (
            state.HasEntitlement(
                entitlement))
        {
            return new MapStudioFeatureGateResult(
                Allowed:
                    true,
                RequiredEntitlement:
                    entitlement,
                Status:
                    state.Status,
                Reason:
                    null);
        }

        return new MapStudioFeatureGateResult(
            Allowed:
                false,
            RequiredEntitlement:
                entitlement,
            Status:
                state.Status,
            Reason:
                "subscriptionRequired");
    }
}

public interface IMapStudioEntitlementProvider
{
    Task<MapStudioCommercialState>
        GetStateAsync(
            CancellationToken cancellationToken =
                default);
}

public interface IMapStudioBillingGateway
{
    string ProviderId
    {
        get;
    }

    Task<Uri> CreateCheckoutUriAsync(
        string subscriptionPlanId,
        Uri returnUri,
        CancellationToken cancellationToken =
            default);

    Task<Uri> CreateCustomerPortalUriAsync(
        Uri returnUri,
        CancellationToken cancellationToken =
            default);
}

public sealed record MapStudioSubscriptionPlan(
    string Id,
    string DisplayName,
    IReadOnlySet<string> Entitlements,
    bool IsActive = true);
