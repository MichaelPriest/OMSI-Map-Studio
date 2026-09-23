using Windows.Security.Credentials;

namespace MapStudio.Native.Services;

public static class NativeMapCredentialStore
{
    private const string ResourceName =
        "OMSI Map Studio Maps";

    private const string GoogleMapsAccount =
        "google-maps-api-key";

    public static bool HasGoogleMapsApiKey() =>
        !string.IsNullOrWhiteSpace(
            TryGetGoogleMapsApiKey());

    public static string? TryGetGoogleMapsApiKey()
    {
        try
        {
            var credential =
                new PasswordVault()
                    .Retrieve(
                        ResourceName,
                        GoogleMapsAccount);

            credential.RetrievePassword();

            return string.IsNullOrWhiteSpace(
                credential.Password)
                ? null
                : credential.Password;
        }
        catch
        {
            return null;
        }
    }

    public static void SaveGoogleMapsApiKey(
        string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            apiKey);

        DeleteGoogleMapsApiKey();

        new PasswordVault()
            .Add(
                new PasswordCredential(
                    ResourceName,
                    GoogleMapsAccount,
                    apiKey.Trim()));
    }

    public static void DeleteGoogleMapsApiKey()
    {
        try
        {
            var vault =
                new PasswordVault();

            var credential =
                vault.Retrieve(
                    ResourceName,
                    GoogleMapsAccount);

            vault.Remove(
                credential);
        }
        catch
        {
            // Missing credential is already the desired state.
        }
    }
}
