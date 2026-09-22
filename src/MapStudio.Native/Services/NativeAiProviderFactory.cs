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
            "anthropic" =>
                CreateAnthropic(
                    normalized,
                    secretOverride),
            "gemini" =>
                CreateGemini(
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

        switch (provider)
        {
            case MapStudioOpenAiCompatibleProvider
                compatible:
                await compatible
                    .TestConnectionAsync(
                        cancellationToken)
                    .ConfigureAwait(false);
                return;

            case MapStudioAnthropicProvider
                anthropic:
                await anthropic
                    .TestConnectionAsync(
                        cancellationToken)
                    .ConfigureAwait(false);
                return;

            case MapStudioGeminiProvider
                gemini:
                await gemini
                    .TestConnectionAsync(
                        cancellationToken)
                    .ConfigureAwait(false);
                return;

            default:
                throw new NotSupportedException(
                    "aiAdapterConnectionProbeUnsupported:" +
                    profile.AdapterId);
        }
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
            "ollama" or
            "anthropic" or
            "gemini";
    }

    private static IMapStudioAiProvider
        CreateAnthropic(
            MapStudioAiConnectionProfile profile,
            string? secretOverride)
    {
        if (
            string.IsNullOrWhiteSpace(
                profile.Model))
        {
            throw new InvalidDataException(
                "aiAnthropicModelRequired");
        }

        var secret =
            ResolveSecret(
                profile,
                secretOverride,
                "aiAnthropicCredentialRequired");

        return new MapStudioAnthropicProvider(
            HttpClient,
            profile.Model,
            secret,
            profile.Endpoint ??
                "https://api.anthropic.com/v1/messages",
            profile.AdapterId,
            profile.DisplayName);
    }

    private static IMapStudioAiProvider
        CreateGemini(
            MapStudioAiConnectionProfile profile,
            string? secretOverride)
    {
        if (
            string.IsNullOrWhiteSpace(
                profile.Model))
        {
            throw new InvalidDataException(
                "aiGeminiModelRequired");
        }

        var secret =
            ResolveSecret(
                profile,
                secretOverride,
                "aiGeminiCredentialRequired");

        return new MapStudioGeminiProvider(
            HttpClient,
            profile.Model,
            secret,
            profile.Endpoint ??
                "https://generativelanguage.googleapis.com/v1beta",
            profile.AdapterId,
            profile.DisplayName);
    }

    private static string ResolveSecret(
        MapStudioAiConnectionProfile profile,
        string? secretOverride,
        string errorCode)
    {
        var secret =
            string.IsNullOrWhiteSpace(
                secretOverride)
                ? NativeAiCredentialStore
                    .TryGetSecret(
                        profile.Id)
                : secretOverride;

        if (
            string.IsNullOrWhiteSpace(
                secret))
        {
            throw new InvalidDataException(
                errorCode);
        }

        return secret.Trim();
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
