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
        MapStudioAiConnectionProfile profile,
        string? secretOverride = null)
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
                    normalized,
                    secretOverride),
            _ =>
                throw new NotSupportedException(
                    "aiAdapterNotImplemented:" +
                    normalized.AdapterId)
        };
    }

    public static async Task TestConnectionAsync(
        MapStudioAiConnectionProfile profile,
        string? secretOverride = null,
        CancellationToken cancellationToken =
            default)
    {
        var provider =
            Create(
                profile,
                secretOverride);

        if (
            provider is
                MapStudioOpenAiCompatibleProvider
                    compatible)
        {
            await compatible
                .TestConnectionAsync(
                    cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        throw new NotSupportedException(
            "aiAdapterConnectionProbeUnsupported:" +
            profile.AdapterId);
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
            MapStudioAiConnectionProfile profile,
            string? secretOverride)
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
            string.IsNullOrWhiteSpace(
                secretOverride)
                ? NativeAiCredentialStore
                    .TryGetSecret(
                        profile.Id)
                : secretOverride;

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
