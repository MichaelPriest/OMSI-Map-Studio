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

    private const int MaximumRecoveryDepth =
        1;

    private static readonly TimeSpan
        EndpointAttemptTimeout =
            TimeSpan.FromSeconds(
                18);

    private static readonly TimeSpan
        MaximumDownloadDuration =
            TimeSpan.FromMinutes(
                2);

    private static readonly HttpClient
        SharedHttpClient =
            CreateSharedHttpClient();

    private static readonly Uri[]
        DefaultEndpoints =
        [
            new(
                "https://overpass-api.de/api/interpreter"),
            new(
                "https://overpass.kumi.systems/api/interpreter"),
            new(
                "https://overpass.private.coffee/api/interpreter")
        ];

    private readonly HttpClient
        _httpClient;

    private readonly IReadOnlyList<Uri>
        _endpoints;

    private readonly Action<string>?
        _diagnostic;

    private int _endpointRotation;

    public MapStudioOverpassRoadClient()
        : this(
            SharedHttpClient,
            DefaultEndpoints,
            null)
    {
    }

    public MapStudioOverpassRoadClient(
        Action<string> diagnostic)
        : this(
            SharedHttpClient,
            DefaultEndpoints,
            diagnostic)
    {
    }

    public MapStudioOverpassRoadClient(
        HttpClient httpClient,
        IReadOnlyList<Uri> endpoints)
        : this(
            httpClient,
            endpoints,
            null)
    {
    }

    public MapStudioOverpassRoadClient(
        HttpClient httpClient,
        IReadOnlyList<Uri> endpoints,
        Action<string>? diagnostic)
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
            endpoints.ToArray();

        _diagnostic =
            diagnostic;
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
        using var budgetCancellation =
            CancellationTokenSource
                .CreateLinkedTokenSource(
                    cancellationToken);

        budgetCancellation.CancelAfter(
            MaximumDownloadDuration);

        WriteDiagnostic(
            $"download begin bounds={south:F6},{west:F6},{north:F6},{east:F6} maxSeconds={MaximumDownloadDuration.TotalSeconds:0} recoveryDepth={MaximumRecoveryDepth}");

        try
        {
            var result =
                await DownloadCoreAsync(
                        south,
                        west,
                        north,
                        east,
                        budgetCancellation.Token)
                    .ConfigureAwait(false);

            WriteDiagnostic(
                $"download complete traces={result.Traces.Count} chunksOk={result.SuccessfulChunkCount} attempts={result.RequestAttemptCount}");

            return result;
        }
        catch (OperationCanceledException)
            when (
                !cancellationToken
                    .IsCancellationRequested &&
                budgetCancellation
                    .IsCancellationRequested)
        {
            WriteDiagnostic(
                $"download budget-exceeded maxSeconds={MaximumDownloadDuration.TotalSeconds:0}");

            throw new HttpRequestException(
                $"Importação OpenStreetMap excedeu o limite de {MaximumDownloadDuration.TotalSeconds:0} segundos. " +
                "A geração foi interrompida para evitar ficar presa em retries dos servidores Overpass.");
        }
    }

    private async Task<
        MapStudioOverpassRoadDownloadResult>
        DownloadCoreAsync(
            double south,
            double west,
            double north,
            double east,
            CancellationToken cancellationToken)
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

        var aggregate =
            new RecoveryAccumulator();

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

                WriteDiagnostic(
                    $"chunk start row={row + 1}/{rows} column={column + 1}/{columns} bounds={chunkSouth:F6},{chunkWest:F6},{chunkNorth:F6},{chunkEast:F6}");

                var recovered =
                    await DownloadChunkWithRecoveryAsync(
                            new Bounds(
                                chunkSouth,
                                chunkWest,
                                chunkNorth,
                                chunkEast),
                            0,
                            cancellationToken)
                        .ConfigureAwait(false);

                aggregate.Add(
                    recovered);

                WriteDiagnostic(
                    $"chunk complete row={row + 1}/{rows} column={column + 1}/{columns} ok={recovered.SuccessfulChunkCount} failed={recovered.FailedChunkCount} attempts={recovered.RequestAttemptCount}");
            }
        }

        if (
            aggregate.FailedChunkCount >
                0)
        {
            throw new HttpRequestException(
                "Importação OpenStreetMap incompleta: " +
                $"{aggregate.FailedChunkCount} subárea(s) falharam mesmo após subdivisão e fallback. " +
                "A geração foi cancelada para não criar uma malha parcial." +
                (
                    aggregate.Failures.Count ==
                        0
                        ? string.Empty
                        : " " +
                          string.Join(
                              " | ",
                              aggregate.Failures
                                  .Distinct(
                                      StringComparer
                                          .OrdinalIgnoreCase)
                                  .Take(
                                      6))
                ));
        }

        if (
            aggregate.SuccessfulChunkCount ==
                0)
        {
            throw new HttpRequestException(
                "Nenhuma consulta Overpass pôde ser concluída.");
        }

        return new MapStudioOverpassRoadDownloadResult(
            aggregate.Traces.Values
                .OrderBy(
                    trace =>
                        trace.Id,
                    StringComparer
                        .OrdinalIgnoreCase)
                .ToArray(),
            aggregate.IgnoredWayCount,
            aggregate.MissingNodeReferenceCount,
            aggregate.SuccessfulChunkCount,
            0,
            aggregate.RequestAttemptCount,
            aggregate.UsedEndpoints
                .OrderBy(
                    endpoint =>
                        endpoint,
                    StringComparer
                        .OrdinalIgnoreCase)
                .ToArray());
    }

    private async Task<RecoveryResult>
        DownloadChunkWithRecoveryAsync(
            Bounds bounds,
            int depth,
            CancellationToken cancellationToken)
    {
        cancellationToken
            .ThrowIfCancellationRequested();

        WriteDiagnostic(
            $"recovery attempt depth={depth} bounds={bounds.South:F6},{bounds.West:F6},{bounds.North:F6},{bounds.East:F6}");

        var fetched =
            await TryDownloadChunkAsync(
                    bounds.South,
                    bounds.West,
                    bounds.North,
                    bounds.East,
                    cancellationToken)
                .ConfigureAwait(false);

        if (fetched.Result is not null)
        {
            return RecoveryResult
                .FromSuccess(
                    fetched);
        }

        if (
            depth >=
            MaximumRecoveryDepth)
        {
            WriteDiagnostic(
                $"recovery exhausted depth={depth} error={fetched.Error}");

            return RecoveryResult
                .FromFailure(
                    fetched);
        }

        WriteDiagnostic(
            $"recovery subdivide depth={depth} nextDepth={depth + 1} error={fetched.Error}");

        var middleLatitude =
            (
                bounds.South +
                bounds.North
            ) *
            0.5;

        var middleLongitude =
            (
                bounds.West +
                bounds.East
            ) *
            0.5;

        var result =
            new RecoveryAccumulator
            {
                RequestAttemptCount =
                    fetched.AttemptCount
            };

        foreach (
            var child in
                new[]
                {
                    new Bounds(
                        bounds.South,
                        bounds.West,
                        middleLatitude,
                        middleLongitude),
                    new Bounds(
                        bounds.South,
                        middleLongitude,
                        middleLatitude,
                        bounds.East),
                    new Bounds(
                        middleLatitude,
                        bounds.West,
                        bounds.North,
                        middleLongitude),
                    new Bounds(
                        middleLatitude,
                        middleLongitude,
                        bounds.North,
                        bounds.East)
                })
        {
            result.Add(
                await DownloadChunkWithRecoveryAsync(
                        child,
                        depth +
                            1,
                        cancellationToken)
                    .ConfigureAwait(false));
        }

        if (
            result.FailedChunkCount >
                0 &&
            !string.IsNullOrWhiteSpace(
                fetched.Error))
        {
            result.Failures.Add(
                fetched.Error);
        }

        return result.ToResult();
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

        var endpointStart =
            Math.Abs(
                System.Threading.Interlocked
                    .Increment(
                        ref _endpointRotation) -
                1) %
            _endpoints.Count;

        for (
            var endpointOffset = 0;
            endpointOffset <
                _endpoints.Count;
            endpointOffset++)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            var endpoint =
                _endpoints[
                    (
                        endpointStart +
                        endpointOffset
                    ) %
                    _endpoints.Count];

            attemptCount++;

            using var attemptCancellation =
                CancellationTokenSource
                    .CreateLinkedTokenSource(
                        cancellationToken);

            attemptCancellation.CancelAfter(
                EndpointAttemptTimeout);

            WriteDiagnostic(
                $"endpoint attempt={attemptCount}/{_endpoints.Count} host={endpoint.Host} timeoutSeconds={EndpointAttemptTimeout.TotalSeconds:0}");

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
                            attemptCancellation.Token)
                        .ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    var error =
                        $"{endpoint.Host}: HTTP {(int)response.StatusCode}";

                    errors.Add(
                        error);

                    WriteDiagnostic(
                        $"endpoint failed {error}");

                    continue;
                }

                var xml =
                    await response.Content
                        .ReadAsStringAsync(
                            attemptCancellation.Token)
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
                    var imported =
                        new MapStudioOsmRoadImporter()
                            .Parse(
                                xml);

                    WriteDiagnostic(
                        $"endpoint success host={endpoint.Host} traces={imported.Traces.Count} ignoredWays={imported.IgnoredWayCount} missingNodes={imported.MissingNodeReferenceCount}");

                    return new ChunkFetchResult(
                        imported,
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

                var error =
                    exception is
                        TaskCanceledException &&
                    attemptCancellation
                        .IsCancellationRequested
                        ? $"{endpoint.Host}: timeout após {EndpointAttemptTimeout.TotalSeconds:0}s"
                        : $"{endpoint.Host}: {exception.Message}";

                errors.Add(
                    error);

                WriteDiagnostic(
                    $"endpoint failed {error}");
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
            $"[out:xml][timeout:16];way[\"highway\"]({south:G17},{west:G17},{north:G17},{east:G17});out body;>;out skel qt;");
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

        return
            (
                Math.Clamp(
                    (int)Math.Ceiling(
                        heightMeters /
                        TargetChunkSpanMeters),
                    1,
                    MaximumChunksPerAxis),
                Math.Clamp(
                    (int)Math.Ceiling(
                        widthMeters /
                        TargetChunkSpanMeters),
                    1,
                    MaximumChunksPerAxis)
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

    private void WriteDiagnostic(
        string message)
    {
        try
        {
            _diagnostic?.Invoke(
                message);
        }
        catch
        {
        }
    }

    private static HttpClient
        CreateSharedHttpClient()
    {
        var client =
            new HttpClient
            {
                Timeout =
                    System.Threading.Timeout
                        .InfiniteTimeSpan
            };

        client.DefaultRequestHeaders
            .UserAgent
            .ParseAdd(
                "OMSI-Map-Studio/0.2 (+https://github.com/MichaelPriest/OMSI-Map-Studio)");

        return client;
    }

    private sealed record Bounds(
        double South,
        double West,
        double North,
        double East);

    private sealed record ChunkFetchResult(
        MapStudioOsmRoadImportResult?
            Result,
        string? Endpoint,
        int AttemptCount,
        string? Error);

    private sealed record RecoveryResult(
        IReadOnlyList<MapStudioGeoRoadTrace>
            Traces,
        int IgnoredWayCount,
        int MissingNodeReferenceCount,
        int SuccessfulChunkCount,
        int FailedChunkCount,
        int RequestAttemptCount,
        IReadOnlyList<string> UsedEndpoints,
        IReadOnlyList<string> Failures)
    {
        public static RecoveryResult
            FromSuccess(
                ChunkFetchResult fetched) =>
            new(
                fetched.Result!.Traces,
                fetched.Result.IgnoredWayCount,
                fetched.Result.MissingNodeReferenceCount,
                1,
                0,
                fetched.AttemptCount,
                fetched.Endpoint is null
                    ? Array.Empty<string>()
                    : [fetched.Endpoint],
                Array.Empty<string>());

        public static RecoveryResult
            FromFailure(
                ChunkFetchResult fetched) =>
            new(
                Array.Empty<
                    MapStudioGeoRoadTrace>(),
                0,
                0,
                0,
                1,
                fetched.AttemptCount,
                Array.Empty<string>(),
                string.IsNullOrWhiteSpace(
                    fetched.Error)
                    ? Array.Empty<string>()
                    : [fetched.Error]);
    }

    private sealed class RecoveryAccumulator
    {
        public Dictionary<
            string,
            MapStudioGeoRoadTrace>
            Traces { get; } =
            new(
                StringComparer
                    .OrdinalIgnoreCase);

        public HashSet<string>
            UsedEndpoints { get; } =
            new(
                StringComparer
                    .OrdinalIgnoreCase);

        public List<string>
            Failures { get; } =
            [];

        public int IgnoredWayCount
        {
            get;
            set;
        }

        public int MissingNodeReferenceCount
        {
            get;
            set;
        }

        public int SuccessfulChunkCount
        {
            get;
            set;
        }

        public int FailedChunkCount
        {
            get;
            set;
        }

        public int RequestAttemptCount
        {
            get;
            set;
        }

        public void Add(
            RecoveryResult value)
        {
            foreach (
                var trace in
                    value.Traces)
            {
                Traces[
                    trace.Id] =
                    trace;
            }

            foreach (
                var endpoint in
                    value.UsedEndpoints)
            {
                UsedEndpoints.Add(
                    endpoint);
            }

            Failures.AddRange(
                value.Failures);

            IgnoredWayCount +=
                value.IgnoredWayCount;

            MissingNodeReferenceCount +=
                value.MissingNodeReferenceCount;

            SuccessfulChunkCount +=
                value.SuccessfulChunkCount;

            FailedChunkCount +=
                value.FailedChunkCount;

            RequestAttemptCount +=
                value.RequestAttemptCount;
        }

        public RecoveryResult ToResult() =>
            new(
                Traces.Values.ToArray(),
                IgnoredWayCount,
                MissingNodeReferenceCount,
                SuccessfulChunkCount,
                FailedChunkCount,
                RequestAttemptCount,
                UsedEndpoints.ToArray(),
                Failures.ToArray());
    }
}
