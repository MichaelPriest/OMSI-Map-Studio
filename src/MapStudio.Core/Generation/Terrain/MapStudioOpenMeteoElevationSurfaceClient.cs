using System.Globalization;
using System.Text.Json;

namespace MapStudio.Core.Generation.Terrain;

public sealed class MapStudioOpenMeteoElevationSurfaceClient
{
    private const int BatchSize = 100;

    private readonly HttpClient _httpClient;

    public MapStudioOpenMeteoElevationSurfaceClient()
        : this(
            new HttpClient
            {
                Timeout =
                    TimeSpan.FromSeconds(
                        30)
            })
    {
    }

    public MapStudioOpenMeteoElevationSurfaceClient(
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
                "invalidOpenMeteoElevationRequest");
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
                (double)row /
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
                    (double)column /
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

            var batch =
                coordinates
                    .Skip(offset)
                    .Take(
                        Math.Min(
                            BatchSize,
                            coordinates.Count -
                            offset))
                    .ToArray();

            var latitudes =
                string.Join(
                    ",",
                    batch.Select(
                        coordinate =>
                            coordinate.Latitude
                                .ToString(
                                    "G17",
                                    CultureInfo.InvariantCulture)));

            var longitudes =
                string.Join(
                    ",",
                    batch.Select(
                        coordinate =>
                            coordinate.Longitude
                                .ToString(
                                    "G17",
                                    CultureInfo.InvariantCulture)));

            var uri =
                "https://customer-api.open-meteo.com/v1/elevation" +
                "?latitude=" +
                Uri.EscapeDataString(
                    latitudes) +
                "&longitude=" +
                Uri.EscapeDataString(
                    longitudes) +
                "&apikey=" +
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
                    $"openMeteoElevationHttp:{(int)response.StatusCode}");
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

            if (
                !root.TryGetProperty(
                    "elevation",
                    out var values) ||
                values.ValueKind !=
                    JsonValueKind.Array ||
                values.GetArrayLength() !=
                    batch.Length)
            {
                throw new InvalidDataException(
                    "openMeteoElevationGridError");
            }

            foreach (
                var elevation in
                    values.EnumerateArray())
            {
                if (
                    !elevation.TryGetDouble(
                        out var value) ||
                    !double.IsFinite(value))
                {
                    throw new InvalidDataException(
                        "openMeteoElevationGridError");
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
                "openMeteoElevationGridError");
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
                "open-meteo-copernicus"),
            south,
            west,
            north,
            east,
            minimum);
    }
}
