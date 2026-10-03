using System.Globalization;
using System.Text.Json;

namespace MapStudio.Native.Services;

public sealed record NativeMapSearchResult(
    string DisplayName,
    double Latitude,
    double Longitude,
    string? Type);

public static class NativeOpenStreetMapGeocoder
{
    private static readonly HttpClient Client =
        CreateClient();

    private static readonly SemaphoreSlim Gate =
        new(
            1,
            1);

    private static DateTimeOffset _lastRequestUtc =
        DateTimeOffset.MinValue;

    public static async Task<IReadOnlyList<NativeMapSearchResult>>
        SearchAsync(
            string query,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            query);

        var normalized =
            query
                .Trim();

        if (normalized.Length > 200)
        {
            normalized =
                normalized[..200];
        }

        await Gate
            .WaitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var elapsed =
                DateTimeOffset.UtcNow -
                _lastRequestUtc;

            if (
                elapsed <
                TimeSpan.FromSeconds(
                    1))
            {
                await Task
                    .Delay(
                        TimeSpan.FromSeconds(
                            1) -
                        elapsed,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            var url =
                "https://nominatim.openstreetmap.org/search" +
                "?format=jsonv2" +
                "&limit=5" +
                "&addressdetails=0" +
                "&accept-language=pt-BR,en" +
                "&q=" +
                Uri.EscapeDataString(
                    normalized);

            using var response =
                await Client
                    .GetAsync(
                        url,
                        cancellationToken)
                    .ConfigureAwait(false);

            _lastRequestUtc =
                DateTimeOffset.UtcNow;

            response
                .EnsureSuccessStatusCode();

            await using var stream =
                await response.Content
                    .ReadAsStreamAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

            using var document =
                await JsonDocument
                    .ParseAsync(
                        stream,
                        cancellationToken:
                            cancellationToken)
                    .ConfigureAwait(false);

            if (
                document.RootElement.ValueKind !=
                    JsonValueKind.Array)
            {
                return
                    Array.Empty<NativeMapSearchResult>();
            }

            var results =
                new List<NativeMapSearchResult>();

            foreach (
                var item in
                    document.RootElement
                        .EnumerateArray())
            {
                if (
                    !item.TryGetProperty(
                        "display_name",
                        out var displayNameElement) ||
                    !item.TryGetProperty(
                        "lat",
                        out var latitudeElement) ||
                    !item.TryGetProperty(
                        "lon",
                        out var longitudeElement))
                {
                    continue;
                }

                var displayName =
                    displayNameElement
                        .GetString();

                var latitudeText =
                    latitudeElement
                        .GetString();

                var longitudeText =
                    longitudeElement
                        .GetString();

                if (
                    string.IsNullOrWhiteSpace(
                        displayName) ||
                    !double.TryParse(
                        latitudeText,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var latitude) ||
                    !double.TryParse(
                        longitudeText,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var longitude) ||
                    !double.IsFinite(
                        latitude) ||
                    !double.IsFinite(
                        longitude))
                {
                    continue;
                }

                var type =
                    item.TryGetProperty(
                        "type",
                        out var typeElement)
                        ? typeElement.GetString()
                        : null;

                results.Add(
                    new NativeMapSearchResult(
                        displayName,
                        latitude,
                        longitude,
                        type));
            }

            return
                results;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static HttpClient CreateClient()
    {
        var client =
            new HttpClient
            {
                Timeout =
                    TimeSpan.FromSeconds(
                        20)
            };

        client.DefaultRequestHeaders
            .UserAgent
            .ParseAdd(
                "OMSI-Map-Studio/0.2 (+https://github.com/MichaelPriest/OMSI-Map-Studio)");

        return
            client;
    }
}
