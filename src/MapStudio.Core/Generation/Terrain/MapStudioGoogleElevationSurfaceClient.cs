using System.Globalization;
using System.Text.Json;

namespace MapStudio.Core.Generation.Terrain;

public sealed record MapStudioElevationDownloadProgress(
    int CompletedSamples,
    int TotalSamples);

public sealed class MapStudioGoogleElevationSurfaceClient
{
    private const int BatchSize = 64;

    private readonly HttpClient _httpClient;

    public MapStudioGoogleElevationSurfaceClient()
        : this(
            new HttpClient
            {
                Timeout =
                    TimeSpan.FromSeconds(
                        30)
            })
    {
    }

    public MapStudioGoogleElevationSurfaceClient(
        HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(
            httpClient);

        _httpClient =
            httpClient;
    }

    public async Task<MapStudioGeoreferencedElevationSurface>
        DownloadAsync(
            string apiKey,
            double south,
            double west,
            double north,
            double east,
            int rows = 17,
            int columns = 17,
            IProgress<MapStudioElevationDownloadProgress>?
                progress = null,
            CancellationToken cancellationToken = default)
    {
        if (
            string.IsNullOrWhiteSpace(apiKey) ||
            !double.IsFinite(south) ||
            !double.IsFinite(west) ||
            !double.IsFinite(north) ||
            !double.IsFinite(east) ||
            south is < -90 or > 90 ||
            north is < -90 or > 90 ||
            west is < -180 or > 180 ||
            east is < -180 or > 180 ||
            south >= north ||
            west >= east ||
            rows is < 2 or > 65 ||
            columns is < 2 or > 65)
        {
            throw new InvalidDataException(
                "invalidGoogleElevationRequest");
        }

        var totalSamples =
            checked(
                rows *
                columns);

        var coordinates =
            new List<(
                double Latitude,
                double Longitude)>(
                    totalSamples);

        for (
            var row = 0;
            row < rows;
            row++)
        {
            var rowFraction =
                rows == 1
                    ? 0
                    : (double)row /
                      (
                          rows -
                          1
                      );

            var latitude =
                north +
                (
                    south -
                    north
                ) *
                rowFraction;

            for (
                var column = 0;
                column < columns;
                column++)
            {
                var columnFraction =
                    columns == 1
                        ? 0
                        : (double)column /
                          (
                              columns -
                              1
                          );

                var longitude =
                    west +
                    (
                        east -
                        west
                    ) *
                    columnFraction;

                coordinates.Add(
                    (
                        latitude,
                        longitude
                    ));
            }
        }

        var elevations =
            new List<double>(
                totalSamples);

        for (
            var offset = 0;
            offset < coordinates.Count;
            offset += BatchSize)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            var count =
                Math.Min(
                    BatchSize,
                    coordinates.Count -
                    offset);

            var locations =
                coordinates
                    .Skip(offset)
                    .Take(count)
                    .Select(
                        coordinate =>
                            string.Create(
                                CultureInfo.InvariantCulture,
                                $"{coordinate.Latitude:G17},{coordinate.Longitude:G17}"))
                    .ToArray();

            var uri =
                "https://maps.googleapis.com/maps/api/elevation/json" +
                "?locations=" +
                Uri.EscapeDataString(
                    string.Join(
                        "|",
                        locations)) +
                "&key=" +
                Uri.EscapeDataString(
                    apiKey.Trim());

            using var response =
                await _httpClient
                    .GetAsync(
                        uri,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"googleElevationHttp:{(int)response.StatusCode}");
            }

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

            var root =
                document.RootElement;

            var statusText =
                root.TryGetProperty(
                    "status",
                    out var status)
                    ? status.GetString()
                    : null;

            if (
                !string.Equals(
                    statusText,
                    "OK",
                    StringComparison.OrdinalIgnoreCase) ||
                !root.TryGetProperty(
                    "results",
                    out var results) ||
                results.ValueKind !=
                    JsonValueKind.Array ||
                results.GetArrayLength() !=
                    count)
            {
                throw new InvalidDataException(
                    string.IsNullOrWhiteSpace(statusText)
                        ? "googleElevationGridError"
                        : "googleElevation:" +
                          statusText);
            }

            foreach (
                var result in
                    results.EnumerateArray())
            {
                if (
                    !result.TryGetProperty(
                        "elevation",
                        out var elevation) ||
                    !elevation.TryGetDouble(
                        out var value) ||
                    !double.IsFinite(value))
                {
                    throw new InvalidDataException(
                        "googleElevationGridError");
                }

                elevations.Add(
                    value);
            }

            progress?.Report(
                new MapStudioElevationDownloadProgress(
                    elevations.Count,
                    totalSamples));
        }

        if (
            elevations.Count !=
            totalSamples)
        {
            throw new InvalidDataException(
                "googleElevationGridError");
        }

        var minimum =
            elevations.Min();

        var maximum =
            elevations.Max();

        return new MapStudioGeoreferencedElevationSurface(
            new MapStudioElevationGrid(
                rows,
                columns,
                elevations,
                minimum,
                maximum,
                "google-elevation"),
            south,
            west,
            north,
            east,
            minimum);
    }
}
