using Windows.Security.Credentials;

namespace MapStudio.Native.Services;

public static class NativeAiCredentialStore
{
    private const string ResourceName =
        "OMSI Map Studio AI";

    public static bool HasSecret(
        string profileId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            profileId);

        try
        {
            var credential =
                new PasswordVault()
                    .Retrieve(
                        ResourceName,
                        profileId.Trim());

            credential
                .RetrievePassword();

            return
                !string.IsNullOrWhiteSpace(
                    credential.Password);
        }
        catch
        {
            return false;
        }
    }

    public static string? TryGetSecret(
        string profileId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            profileId);

        try
        {
            var credential =
                new PasswordVault()
                    .Retrieve(
                        ResourceName,
                        profileId.Trim());

            credential
                .RetrievePassword();

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

    public static void SaveSecret(
        string profileId,
        string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            profileId);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            secret);

        var normalizedId =
            profileId.Trim();

        DeleteSecret(
            normalizedId);

        new PasswordVault()
            .Add(
                new PasswordCredential(
                    ResourceName,
                    normalizedId,
                    secret));
    }

    public static void DeleteSecret(
        string profileId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            profileId);

        var normalizedId =
            profileId.Trim();

        try
        {
            var vault =
                new PasswordVault();

            var credential =
                vault.Retrieve(
                    ResourceName,
                    normalizedId);

            vault.Remove(
                credential);
        }
        catch
        {
            // Missing credential is already the desired state.
        }
    }
}
