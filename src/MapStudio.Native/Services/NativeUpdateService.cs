using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MapStudio.Native.Services;

internal sealed record NativeUpdateInfo(
    string Version,
    string Notes,
    string? MinimumVersion,
    bool Mandatory,
    DateTimeOffset PublishedAt,
    string? Sha256,
    string DownloadEndpoint);

internal sealed record NativeUpdateCheckResult(
    string CurrentVersion,
    string Channel,
    NativeUpdateInfo? Update,
    bool IsUpdateAvailable,
    Uri? DownloadUri);

internal sealed class NativeUpdateService
{
    private static readonly HttpClient Http =
        new()
        {
            Timeout =
                TimeSpan.FromSeconds(
                    12)
        };

    private static readonly JsonSerializerOptions JsonOptions =
        new()
        {
            PropertyNameCaseInsensitive =
                true
        };

    public static string CurrentVersion
    {
        get
        {
            var assembly =
                Assembly.GetEntryAssembly() ??
                typeof(NativeUpdateService)
                    .Assembly;

            var informational =
                assembly
                    .GetCustomAttribute<
                        AssemblyInformationalVersionAttribute>()
                    ?.InformationalVersion;

            if (
                !string.IsNullOrWhiteSpace(
                    informational))
            {
                var plus =
                    informational.IndexOf(
                        '+');

                return plus >= 0
                    ? informational[..plus]
                    : informational;
            }

            return
                assembly
                    .GetName()
                    .Version
                    ?.ToString() ??
                "0.0.0";
        }
    }

    public async Task<NativeUpdateCheckResult?>
        CheckAsync(
            Uri apiBaseUri,
            string channel,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentNullException.ThrowIfNull(
            apiBaseUri);

        channel =
            string.Equals(
                channel,
                "stable",
                StringComparison.OrdinalIgnoreCase)
                ? "stable"
                : "alpha";

        var endpoint =
            new Uri(
                apiBaseUri,
                $"/api/updates/latest?channel={Uri.EscapeDataString(channel)}");

        using var response =
            await Http
                .GetAsync(
                    endpoint,
                    cancellationToken)
                .ConfigureAwait(
                    false);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream =
            await response.Content
                .ReadAsStreamAsync(
                    cancellationToken)
                .ConfigureAwait(
                    false);

        var envelope =
            await JsonSerializer
                .DeserializeAsync<
                    UpdateEnvelope>(
                    stream,
                    JsonOptions,
                    cancellationToken)
                .ConfigureAwait(
                    false);

        if (
            envelope?.Update is null ||
            string.IsNullOrWhiteSpace(
                envelope.Update.Version))
        {
            return new NativeUpdateCheckResult(
                CurrentVersion,
                channel,
                null,
                IsUpdateAvailable:
                    false,
                DownloadUri:
                    null);
        }

        var update =
            new NativeUpdateInfo(
                envelope.Update.Version,
                envelope.Update.Notes ??
                    string.Empty,
                envelope.Update.MinimumVersion,
                envelope.Update.Mandatory,
                envelope.Update.PublishedAt,
                envelope.Update.Sha256,
                envelope.Update.DownloadEndpoint ??
                    $"/api/download/latest?channel={channel}");

        var available =
            NativeVersionComparer
                .Compare(
                    update.Version,
                    CurrentVersion) >
            0;

        var downloadUri =
            available
                ? new Uri(
                    apiBaseUri,
                    update.DownloadEndpoint)
                : null;

        return new NativeUpdateCheckResult(
            CurrentVersion,
            channel,
            update,
            available,
            downloadUri);
    }

    private sealed record UpdateEnvelope(
        string? Channel,
        UpdateDto? Update);

    private sealed record UpdateDto(
        string Version,
        string? Notes,
        string? MinimumVersion,
        bool Mandatory,
        DateTimeOffset PublishedAt,
        string? Sha256,
        string? DownloadEndpoint);
}

internal static class NativeCommerceEndpoint
{
    public static Uri? ResolveApiBaseUri()
    {
        var configured =
            Environment
                .GetEnvironmentVariable(
                    "OMSI_MAP_STUDIO_API_BASE_URL");

        if (
            string.IsNullOrWhiteSpace(
                configured) ||
            !Uri.TryCreate(
                configured,
                UriKind.Absolute,
                out var uri) ||
            (
                uri.Scheme != Uri.UriSchemeHttps &&
                uri.Scheme != Uri.UriSchemeHttp
            ))
        {
            return null;
        }

        return uri;
    }

    public static string ResolveUpdateChannel()
    {
        var channel =
            Environment
                .GetEnvironmentVariable(
                    "OMSI_MAP_STUDIO_UPDATE_CHANNEL");

        return string.Equals(
            channel,
            "stable",
            StringComparison.OrdinalIgnoreCase)
            ? "stable"
            : "alpha";
    }
}

internal static class NativeVersionComparer
{
    public static int Compare(
        string left,
        string right)
    {
        var a =
            Parse(
                left);

        var b =
            Parse(
                right);

        for (
            var index = 0;
            index < 3;
            index++)
        {
            var result =
                a.Core[index]
                    .CompareTo(
                        b.Core[index]);

            if (result != 0)
            {
                return result;
            }
        }

        if (
            a.PreRelease.Count == 0 &&
            b.PreRelease.Count == 0)
        {
            return 0;
        }

        if (a.PreRelease.Count == 0)
        {
            return 1;
        }

        if (b.PreRelease.Count == 0)
        {
            return -1;
        }

        var count =
            Math.Max(
                a.PreRelease.Count,
                b.PreRelease.Count);

        for (
            var index = 0;
            index < count;
            index++)
        {
            if (index >= a.PreRelease.Count)
            {
                return -1;
            }

            if (index >= b.PreRelease.Count)
            {
                return 1;
            }

            var result =
                CompareIdentifier(
                    a.PreRelease[index],
                    b.PreRelease[index]);

            if (result != 0)
            {
                return result;
            }
        }

        return 0;
    }

    private static ParsedVersion Parse(
        string value)
    {
        value =
            value
                .Trim()
                .TrimStart(
                    'v',
                    'V');

        var plusIndex =
            value.IndexOf(
                '+');

        if (plusIndex >= 0)
        {
            value =
                value[..plusIndex];
        }

        var dashIndex =
            value.IndexOf(
                '-');

        var coreText =
            dashIndex >= 0
                ? value[..dashIndex]
                : value;

        var preText =
            dashIndex >= 0
                ? value[(dashIndex + 1)..]
                : string.Empty;

        var coreParts =
            coreText
                .Split(
                    '.',
                    StringSplitOptions
                        .RemoveEmptyEntries);

        var core =
            new int[3];

        for (
            var index = 0;
            index < core.Length;
            index++)
        {
            if (
                index <
                    coreParts.Length &&
                int.TryParse(
                    coreParts[index],
                    out var parsed))
            {
                core[index] =
                    parsed;
            }
        }

        var preRelease =
            preText
                .Split(
                    ['.', '-'],
                    StringSplitOptions
                        .RemoveEmptyEntries)
                .ToArray();

        return new ParsedVersion(
            core,
            preRelease);
    }

    private static int CompareIdentifier(
        string left,
        string right)
    {
        var leftNumber =
            int.TryParse(
                left,
                out var leftValue);

        var rightNumber =
            int.TryParse(
                right,
                out var rightValue);

        if (
            leftNumber &&
            rightNumber)
        {
            return leftValue
                .CompareTo(
                    rightValue);
        }

        if (leftNumber)
        {
            return -1;
        }

        if (rightNumber)
        {
            return 1;
        }

        return string.Compare(
            left,
            right,
            StringComparison
                .OrdinalIgnoreCase);
    }

    private sealed record ParsedVersion(
        int[] Core,
        IReadOnlyList<string> PreRelease);
}
