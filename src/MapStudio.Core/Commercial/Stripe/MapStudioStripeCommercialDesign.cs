namespace MapStudio.Core.Commercial.Stripe;

public static class MapStudioStripeCommercialDesign
{
    public const string ProviderId =
        "stripe";

    public const string CheckoutMode =
        "subscription";

    public static bool RequiresServerSideIntegration =>
        true;

    public static IReadOnlySet<string>
        ServerResponsibilities { get; } =
        new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            "createCheckoutSession",
            "createCustomerPortalSession",
            "verifyStripeWebhooks",
            "mapSubscriptionToEntitlements",
            "revokeOrDowngradeEntitlements"
        };

    public static IReadOnlySet<string>
        DesktopResponsibilities { get; } =
        new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            "requestCheckoutUrlFromBackend",
            "requestPortalUrlFromBackend",
            "openReturnedUrl",
            "requestSignedEntitlementState",
            "neverStoreStripeSecretKey"
        };
}
