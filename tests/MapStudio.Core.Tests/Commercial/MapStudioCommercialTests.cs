using MapStudio.Core.Commercial;
using MapStudio.Core.Commercial.Stripe;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioCommercialTests
{
    [Fact]
    public void DevelopmentPreviewDoesNotEnforceSubscription()
    {
        var state =
            MapStudioCommercialState
                .DevelopmentPreview();

        var result =
            MapStudioFeatureGate
                .Evaluate(
                    state,
                    MapStudioEntitlementKeys
                        .BuildingStudio);

        Assert.True(
            result.Allowed);

        Assert.False(
            state.EnforcementEnabled);

        Assert.Equal(
            MapStudioLicenseStatus
                .DevelopmentPreview,
            state.Status);
    }

    [Fact]
    public void ProductionStateRequiresExplicitEntitlement()
    {
        var state =
            new MapStudioCommercialState(
                EnforcementEnabled:
                    true,
                Status:
                    MapStudioLicenseStatus
                        .Active,
                SubscriptionPlanId:
                    "creator",
                Entitlements:
                    new HashSet<string>(
                        StringComparer
                            .OrdinalIgnoreCase)
                    {
                        MapStudioEntitlementKeys
                            .CoreEditor,
                        MapStudioEntitlementKeys
                            .Omsi2
                    });

        Assert.True(
            MapStudioFeatureGate
                .Evaluate(
                    state,
                    MapStudioEntitlementKeys
                        .CoreEditor)
                .Allowed);

        Assert.False(
            MapStudioFeatureGate
                .Evaluate(
                    state,
                    MapStudioEntitlementKeys
                        .BuildingStudio)
                .Allowed);
    }

    [Fact]
    public void StripeDesignKeepsSecretOperationsOffDesktop()
    {
        Assert.True(
            MapStudioStripeCommercialDesign
                .RequiresServerSideIntegration);

        Assert.Contains(
            "verifyStripeWebhooks",
            MapStudioStripeCommercialDesign
                .ServerResponsibilities);

        Assert.Contains(
            "neverStoreStripeSecretKey",
            MapStudioStripeCommercialDesign
                .DesktopResponsibilities);

        Assert.DoesNotContain(
            "createCheckoutSession",
            MapStudioStripeCommercialDesign
                .DesktopResponsibilities);
    }
}
