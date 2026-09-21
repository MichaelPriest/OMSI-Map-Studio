using MapStudio.Core.AI;

namespace MapStudio.Native.Services;

public static class NativeAiProviderFactory
{
    private static readonly HttpClient HttpClient =
        new()
        {
            Timeout =
                TimeSpan.FromSeconds(
                    90)
        };

    public static IMapStudioAiProvider Create(
        MapStudioAiConnectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(
            profile);

        var normalized =
            profile.Normalize();

        var adapter =
            normalized.AdapterId
                .Trim()
                .ToLowerInvariant();

        return adapter switch
        {
            "openai-compatible" or
            "lmstudio" or
            "ollama" =>
                CreateOpenAiCompatible(
                    normalized),
            _ =>
                throw new NotSupportedException(
                    "aiAdapterNotImplemented:" +
                    normalized.AdapterId)
        };
    }

    public static bool IsImplemented(
        MapStudioAiConnectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(
            profile);

        var adapter =
            profile.AdapterId
                .Trim()
                .ToLowerInvariant();

        return adapter is
            "openai-compatible" or
            "lmstudio" or
            "ollama";
    }

    private static IMapStudioAiProvider
        CreateOpenAiCompatible(
            MapStudioAiConnectionProfile profile)
    {
        if (
            string.IsNullOrWhiteSpace(
                profile.Endpoint) ||
            string.IsNullOrWhiteSpace(
                profile.Model))
        {
            throw new InvalidDataException(
                "aiCompatibleEndpointAndModelRequired");
        }

        var secret =
            NativeAiCredentialStore
                .TryGetSecret(
                    profile.Id);

        return new MapStudioOpenAiCompatibleProvider(
            HttpClient,
            profile.Endpoint,
            profile.Model,
            secret,
            profile.AdapterId,
            profile.DisplayName,
            profile.IsLocal);
    }
}
