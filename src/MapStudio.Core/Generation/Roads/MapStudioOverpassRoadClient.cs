using System.Globalization;

namespace MapStudio.Core.Generation.Roads;

public sealed record MapStudioOverpassRoadDownloadResult(
    IReadOnlyList<MapStudioGeoRoadTrace> Traces,
    int IgnoredWayCount,
    int MissingNodeReferenceCount,
    int SuccessfulChunkCount,
    int FailedChunkCount,
    int RequestAttemptCount,
    IReadOnlyList<string> UsedEndpoints);

public sealed class MapStudioOverpassRoadClient
{
    private const double EarthRadiusMeters =
        6_378_137.0;

    private const double TargetChunkSpanMeters =
        1_500.0;

    private const int MaximumChunksPerAxis =
        6;

    private static readonly HttpClient
        SharedHttpClient =
            CreateSharedHttpClient();

    private static readonly Uri[]
        DefaultEndpoints =
        [
            new(
                "https://overpass-api.de/api/interpreter"),
            new(
                "https://overpass.private.coffee/api/interpreter")
        ];

    private readonly HttpClient
        _httpClient;

    private readonly IReadOnlyList<Uri>
        _endpoints;

    public MapStudioOverpassRoadClient()
        : this(
            SharedHttpClient,
            DefaultEndpoints)
    {
    }

    public MapStudioOverpassRoadClient(
        HttpClient httpClient,
        IReadOnlyList<Uri> endpoints)
    {
        ArgumentNullException.ThrowIfNull(
            httpClient);

        ArgumentNullException.ThrowIfNull(
            endpoints);

        if (
            endpoints.Count ==
                0 ||
            endpoints.Any(
                endpoint =>
                    endpoint is null ||
                    !endpoint.IsAbsoluteUri ||
                    endpoint.Scheme !=
                        Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                "overpassEndpointsInvalid",
                nameof(endpoints));
        }

        _httpClient =
            httpClient;

        _endpoints =
            endpoints
                .ToArray();
    }

    public async Task<
        MapStudioOverpassRoadDownloadResult>
        DownloadAsync(
            double south,
            double west,
            double north,
            double east,
            CancellationToken cancellationToken =
                default)
    {
        ValidateBounds(
            south,
            west,
            north,
            east);

        var (
            rows,
            columns
        ) =
            GetChunkGrid(
                south,
                west,
                north,
                east);

        var traces =
            new Dictionary<
                string,
                MapStudioGeoRoadTrace>(
                    StringComparer
                        .OrdinalIgnoreCase);

        var usedEndpoints =
            new HashSet<string>(
                StringComparer
                    .OrdinalIgnoreCase);

        var ignoredWays =
            0;

        var missingNodeReferences =
            0;

        var successfulChunks =
            0;

        var failedChunks =
            0;

        var requestAttempts =
            0;

        var failures =
            new List<string>();

        for (
            var row = 0;
            row < rows;
            row++)
        {
            var chunkSouth =
                Interpolate(
                    south,
                    north,
                    row /
                        (double)rows);

            var chunkNorth =
                Interpolate(
                    south,
                    north,
                    (
                        row +
                        1
                    ) /
                    (double)rows);

            for (
                var column = 0;
                column < columns;
                column++)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                var chunkWest =
                    Interpolate(
                        west,
                        east,
                        column /
                            (double)columns);

                var chunkEast =
                    Interpolate(
                        west,
                        east,
                        (
                            column +
                                1
                        ) /
                        (double)columns);

                var fetched =
                    await TryDownloadChunkAsync(
                            chunkSouth,
                            chunkWest,
                            chunkNorth,
                            chunkEast,
                            cancellationToken)
                        .ConfigureAwait(false);

                requestAttempts +=
                    fetched.AttemptCount;

                if (fetched.Result is null)
                {
                    failedChunks++;

                    if (
                        !string.IsNullOrWhiteSpace(
                            fetched.Error))
                    {
                        failures.Add(
                            fetched.Error);
                    }

                    continue;
                }

                successfulChunks++;

                ignoredWays +=
                    fetched.Result
                        .IgnoredWayCount;

                missingNodeReferences +=
                    fetched.Result
                        .MissingNodeReferenceCount;

                if (
                    fetched.Endpoint is
                    not null)
                {
                    usedEndpoints.Add(
                        fetched.Endpoint);
                }

                foreach (
                    var trace in
                        fetched.Result
                            .Traces)
                {
                    traces[
                        trace.Id] =
                        trace;
                }
            }
        }

        if (
            successfulChunks ==
                0)
        {
            throw new HttpRequestException(
                failures.Count ==
                    0
                    ? "Nenhuma consulta Overpass pôde ser concluída."
                    : "Falha em todas as consultas Overpass: " +
                      string.Join(
                          " | ",
                          failures
                              .Distinct(
                                  StringComparer
                                      .OrdinalIgnoreCase)
                              .Take(
                                  4)));
        }

        return new MapStudioOverpassRoadDownloadResult(
            traces.Values
                .OrderBy(
                    trace =>
                        trace.Id,
                    StringComparer
                        .OrdinalIgnoreCase)
                .ToArray(),
            ignoredWays,
            missingNodeReferences,
            successfulChunks,
            failedChunks,
            requestAttempts,
            usedEndpoints
                .OrderBy(
                    endpoint =>
                        endpoint,
                    StringComparer
                        .OrdinalIgnoreCase)
                .ToArray());
    }

    private async Task<ChunkFetchResult>
        TryDownloadChunkAsync(
            double south,
            double west,
            double north,
            double east,
            CancellationToken cancellationToken)
    {
        var query =
            BuildQuery(
                south,
                west,
                north,
                east);

        var errors =
            new List<string>();

        var attemptCount =
            0;

        foreach (
            var endpoint in
                _endpoints)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            attemptCount++;

            try
            {
                using var content =
                    new FormUrlEncodedContent(
                        new Dictionary<
                            string,
                            string>
                        {
                            ["data"] =
                                query
                        });

                using var response =
                    await _httpClient
                        .PostAsync(
                            endpoint,
                            content,
                            cancellationToken)
                        .ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    errors.Add(
                        $"{endpoint.Host}: HTTP {(int)response.StatusCode}");

                    continue;
                }

                var xml =
                    await response.Content
                        .ReadAsStringAsync(
                            cancellationToken)
                        .ConfigureAwait(false);

                if (
                    string.IsNullOrWhiteSpace(
                        xml))
                {
                    errors.Add(
                        $"{endpoint.Host}: resposta vazia");

                    continue;
                }

                try
                {
                    return new ChunkFetchResult(
                        new MapStudioOsmRoadImporter()
                            .Parse(
                                xml),
                        endpoint
                            .GetLeftPart(
                                UriPartial
                                    .Authority),
                        attemptCount,
                        null);
                }
                catch (
                    Exception exception)
                    when (
                        exception is
                            InvalidDataException or
                            System.Xml
                                .XmlException)
                {
                    errors.Add(
                        $"{endpoint.Host}: XML inválido ({exception.Message})");
                }
            }
            catch (
                Exception exception)
                when (
                    exception is
                        HttpRequestException or
                        TaskCanceledException)
            {
                if (
                    cancellationToken
                        .IsCancellationRequested)
                {
                    throw;
                }

                errors.Add(
                    $"{endpoint.Host}: {exception.Message}");
            }
        }

        return new ChunkFetchResult(
            null,
            null,
            attemptCount,
            string.Join(
                " / ",
                errors));
    }

    private static string BuildQuery(
        double south,
        double west,
        double north,
        double east)
    {
        var invariant =
            CultureInfo.InvariantCulture;

        return string.Create(
            invariant,
            $"[out:xml][timeout:40];way[\"highway\"]({south:G17},{west:G17},{north:G17},{east:G17});out body;>;out skel qt;");
    }

    private static (
        int Rows,
        int Columns
    ) GetChunkGrid(
        double south,
        double west,
        double north,
        double east)
    {
        var centerLatitude =
            (
                south +
                north
            ) *
            0.5;

        var latitudeRadians =
            centerLatitude *
            Math.PI /
            180.0;

        var heightMeters =
            (
                north -
                south
            ) *
            Math.PI /
            180.0 *
            EarthRadiusMeters;

        var widthMeters =
            (
                east -
                west
            ) *
            Math.PI /
            180.0 *
            EarthRadiusMeters *
            Math.Max(
                0.01,
                Math.Abs(
                    Math.Cos(
                        latitudeRadians)));

        var rows =
            Math.Clamp(
                (int)Math.Ceiling(
                    heightMeters /
                    TargetChunkSpanMeters),
                1,
                MaximumChunksPerAxis);

        var columns =
            Math.Clamp(
                (int)Math.Ceiling(
                    widthMeters /
                    TargetChunkSpanMeters),
                1,
                MaximumChunksPerAxis);

        return
            (
                rows,
                columns
            );
    }

    private static double Interpolate(
        double start,
        double end,
        double t) =>
        start +
        (
            end -
            start
        ) *
        t;

    private static void ValidateBounds(
        double south,
        double west,
        double north,
        double east)
    {
        if (
            !double.IsFinite(
                south) ||
            !double.IsFinite(
                west) ||
            !double.IsFinite(
                north) ||
            !double.IsFinite(
                east) ||
            south is
                < -90 or > 90 ||
            north is
                < -90 or > 90 ||
            west is
                < -180 or > 180 ||
            east is
                < -180 or > 180 ||
            north <=
                south ||
            east <=
                west)
        {
            throw new ArgumentOutOfRangeException(
                nameof(south),
                "overpassBoundsInvalid");
        }
    }

    private static HttpClient
        CreateSharedHttpClient()
    {
        var client =
            new HttpClient
            {
                Timeout =
                    TimeSpan.FromSeconds(
                        50)
            };

        client.DefaultRequestHeaders
            .UserAgent
            .ParseAdd(
                "OMSI-Map-Studio/0.2 (+https://github.com/MichaelPriest/OMSI-Map-Studio)");

        return client;
    }

    private sealed record ChunkFetchResult(
        MapStudioOsmRoadImportResult?
            Result,
        string? Endpoint,
        int AttemptCount,
        string? Error);
}
