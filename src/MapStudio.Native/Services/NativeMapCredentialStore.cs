using Windows.Security.Credentials;

namespace MapStudio.Native.Services;

public static class NativeMapCredentialStore
{
    private const string ResourceName =
        "OMSI Map Studio Maps";

    private const string GoogleMapsAccount =
        "google-maps-api-key";

    private const string OpenMeteoAccount =
        "open-meteo-api-key";

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

    public static string? TryGetOpenMeteoApiKey()
    {
        try
        {
            var credential =
                new PasswordVault()
                    .Retrieve(
                        ResourceName,
                        OpenMeteoAccount);

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

    public static void SaveOpenMeteoApiKey(
        string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            apiKey);

        DeleteOpenMeteoApiKey();

        new PasswordVault()
            .Add(
                new PasswordCredential(
                    ResourceName,
                    OpenMeteoAccount,
                    apiKey.Trim()));
    }

    public static void DeleteOpenMeteoApiKey()
    {
        try
        {
            var vault =
                new PasswordVault();

            var credential =
                vault.Retrieve(
                    ResourceName,
                    OpenMeteoAccount);

            vault.Remove(
                credential);
        }
        catch
        {
            // Missing credential is already the desired state.
        }
    }
}
