using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace MapStudio.Core.Generation.Scene;

public sealed record MapStudioOverpassSceneDownloadResult(
    string OsmXml,
    int NodeCount,
    int WayCount,
    int RelationCount,
    int SuccessfulChunkCount,
    int FailedChunkCount,
    int RequestAttemptCount,
    IReadOnlyList<string> UsedEndpoints);

public sealed record MapStudioOverpassSceneDownloadProgress(
    int CompletedPrimaryChunks,
    int TotalPrimaryChunks,
    int SuccessfulLeafChunks,
    int FailedLeafChunks,
    int RequestAttemptCount,
    string Message);

public sealed class MapStudioOverpassSceneClient
{
    private const double EarthRadiusMeters =
        6_378_137.0;

    private const double TargetChunkSpanMeters =
        500.0;

    private const int MaximumChunksPerAxis =
        6;

    private const int MaximumConcurrentChunkRequests =
        2;

    private const int MaximumRecoveryDepth =
        1;

    private static readonly TimeSpan
        EndpointAttemptTimeout =
            TimeSpan.FromSeconds(20);

    private static readonly TimeSpan
        EndpointConnectTimeout =
            TimeSpan.FromSeconds(8);

    private static readonly HttpClient
        SharedHttpClient =
            CreateSharedHttpClient();

    private static readonly Uri[]
        DefaultEndpoints =
        [
            new(
                "https://overpass-api.de/api/interpreter"),
            new(
                "https://maps.mail.ru/osm/tools/overpass/api/interpreter"),
            new(
                "https://overpass.private.coffee/api/interpreter")
        ];

    private readonly HttpClient _httpClient;
    private readonly IReadOnlyList<Uri> _endpoints;
    private readonly Action<string>? _diagnostic;
    private int _endpointRotation;

    public MapStudioOverpassSceneClient()
        : this(
            SharedHttpClient,
            DefaultEndpoints,
            null)
    {
    }

    public MapStudioOverpassSceneClient(
        Action<string> diagnostic)
        : this(
            SharedHttpClient,
            DefaultEndpoints,
            diagnostic)
    {
    }

    public MapStudioOverpassSceneClient(
        HttpClient httpClient,
        IReadOnlyList<Uri> endpoints)
        : this(
            httpClient,
            endpoints,
            null)
    {
    }

    public MapStudioOverpassSceneClient(
        HttpClient httpClient,
        IReadOnlyList<Uri> endpoints,
        Action<string>? diagnostic)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(endpoints);

        if (
            endpoints.Count == 0 ||
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

        _httpClient = httpClient;
        _endpoints = endpoints.ToArray();
        _diagnostic = diagnostic;
    }

    public async Task<MapStudioOverpassSceneDownloadResult>
        DownloadAsync(
            double south,
            double west,
            double north,
            double east,
            CancellationToken cancellationToken = default,
            IProgress<MapStudioOverpassSceneDownloadProgress>?
                progress = null)
    {
        ValidateBounds(
            south,
            west,
            north,
            east);

        WriteDiagnostic(
            $"scene download begin bounds={south:F6},{west:F6},{north:F6},{east:F6}");

        return await DownloadCoreAsync(
                south,
                west,
                north,
                east,
                progress,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<MapStudioOverpassSceneDownloadResult>
        DownloadCoreAsync(
            double south,
            double west,
            double north,
            double east,
            IProgress<MapStudioOverpassSceneDownloadProgress>?
                progress,
            CancellationToken cancellationToken)
    {
        var (
            rows,
            columns
        ) =
            GetChunkGrid(
                south,
                west,
                north,
                east);

        var primaryChunks =
            new List<Bounds>(
                checked(
                    rows *
                    columns));

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                primaryChunks.Add(
                    new Bounds(
                        Lerp(
                            south,
                            north,
                            row /
                                (double)rows),
                        Lerp(
                            west,
                            east,
                            column /
                                (double)columns),
                        Lerp(
                            south,
                            north,
                            (row + 1) /
                                (double)rows),
                        Lerp(
                            west,
                            east,
                            (column + 1) /
                                (double)columns)));
            }
        }

        progress?.Report(
            new MapStudioOverpassSceneDownloadProgress(
                0,
                primaryChunks.Count,
                0,
                0,
                0,
                $"OpenStreetMap: preparando {primaryChunks.Count} bloco(s) de download..."));

        using var requestConcurrency =
            new SemaphoreSlim(
                MaximumConcurrentChunkRequests,
                MaximumConcurrentChunkRequests);

        var completedPrimaryChunks =
            0;

        var reportedSuccessfulLeafChunks =
            0;

        var reportedFailedLeafChunks =
            0;

        var reportedRequestAttempts =
            0;

        var chunkTasks =
            primaryChunks
                .Select(
                    async (
                        bounds,
                        index) =>
                    {
                        cancellationToken
                            .ThrowIfCancellationRequested();

                        var result =
                            await DownloadChunkWithRecoveryAsync(
                                    bounds,
                                    depth: 0,
                                    requestConcurrency,
                                    unavailableEndpoints:
                                        Array.Empty<string>(),
                                    onAttempt:
                                        (
                                            endpoint,
                                            endpointAttempt,
                                            endpointTotal) =>
                                        {
                                            var liveAttempts =
                                                System.Threading.Interlocked
                                                    .Increment(
                                                        ref reportedRequestAttempts);

                                            progress?.Report(
                                                new MapStudioOverpassSceneDownloadProgress(
                                                    System.Threading.Volatile
                                                        .Read(
                                                            ref completedPrimaryChunks),
                                                    primaryChunks.Count,
                                                    System.Threading.Volatile
                                                        .Read(
                                                            ref reportedSuccessfulLeafChunks),
                                                    System.Threading.Volatile
                                                        .Read(
                                                            ref reportedFailedLeafChunks),
                                                    liveAttempts,
                                                    $"OpenStreetMap: bloco {index + 1}/{primaryChunks.Count} · tentando {endpoint.Host} ({endpointAttempt}/{endpointTotal})..."));
                                        },
                                    cancellationToken)
                                .ConfigureAwait(false);

                        var completed =
                            System.Threading.Interlocked
                                .Increment(
                                    ref completedPrimaryChunks);

                        var successful =
                            System.Threading.Interlocked
                                .Add(
                                    ref reportedSuccessfulLeafChunks,
                                    result.SuccessfulChunkCount);

                        var failed =
                            System.Threading.Interlocked
                                .Add(
                                    ref reportedFailedLeafChunks,
                                    result.FailedChunkCount);

                        var attempts =
                            System.Threading.Volatile
                                .Read(
                                    ref reportedRequestAttempts);

                        progress?.Report(
                            new MapStudioOverpassSceneDownloadProgress(
                                completed,
                                primaryChunks.Count,
                                successful,
                                failed,
                                attempts,
                                $"OpenStreetMap: bloco {completed}/{primaryChunks.Count} concluído · {attempts} tentativa(s)."));

                        return result;
                    })
                .ToArray();

        var chunkResults =
            await Task
                .WhenAll(
                    chunkTasks)
                .ConfigureAwait(false);

        var aggregate =
            new SceneAggregate();

        var failures =
            new List<string>();

        var successfulChunks = 0;
        var failedChunks = 0;
        var requestAttempts = 0;

        var usedEndpoints =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var result in chunkResults)
        {
            successfulChunks +=
                result.SuccessfulChunkCount;

            failedChunks +=
                result.FailedChunkCount;

            requestAttempts +=
                result.RequestAttemptCount;

            foreach (var endpoint in result.UsedEndpoints)
            {
                usedEndpoints.Add(endpoint);
            }

            foreach (var document in result.Documents)
            {
                aggregate.Add(document);
            }

            failures.AddRange(
                result.Failures);
        }

        if (failedChunks > 0)
        {
            throw new HttpRequestException(
                "Importação de cenário OpenStreetMap ficou incompleta após recuperação limitada. " +
                string.Join(
                    " / ",
                    failures
                        .Where(
                            failure =>
                                !string.IsNullOrWhiteSpace(
                                    failure))
                        .Distinct(
                            StringComparer.OrdinalIgnoreCase)
                        .Take(6)));
        }

        if (successfulChunks == 0)
        {
            throw new HttpRequestException(
                "Nenhuma consulta de cenário Overpass pôde ser concluída.");
        }

        var xml =
            aggregate.BuildXml();

        WriteDiagnostic(
            $"scene download complete nodes={aggregate.NodeCount} ways={aggregate.WayCount} relations={aggregate.RelationCount} chunks={successfulChunks} attempts={requestAttempts}");

        return new MapStudioOverpassSceneDownloadResult(
            xml,
            aggregate.NodeCount,
            aggregate.WayCount,
            aggregate.RelationCount,
            successfulChunks,
            failedChunks,
            requestAttempts,
            usedEndpoints
                .OrderBy(
                    endpoint =>
                        endpoint,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    private async Task<RecoveryResult>
        DownloadChunkWithRecoveryAsync(
            Bounds bounds,
            int depth,
            SemaphoreSlim requestConcurrency,
            IReadOnlyCollection<string> unavailableEndpoints,
            Action<Uri, int, int>? onAttempt,
            CancellationToken cancellationToken)
    {
        WriteDiagnostic(
            $"scene recovery attempt depth={depth} bounds={bounds.South:F6},{bounds.West:F6},{bounds.North:F6},{bounds.East:F6}");

        var fetched =
            await TryDownloadChunkAsync(
                    bounds,
                    requestConcurrency,
                    unavailableEndpoints,
                    onAttempt,
                    cancellationToken)
                .ConfigureAwait(false);

        if (fetched.Document is not null)
        {
            return new RecoveryResult(
                [fetched.Document],
                1,
                0,
                fetched.RequestAttemptCount,
                fetched.Endpoint is null
                    ? Array.Empty<string>()
                    : [fetched.Endpoint],
                Array.Empty<string>());
        }

        if (
            !fetched.CanRecoverBySubdivision ||
            depth >= MaximumRecoveryDepth)

        {
            WriteDiagnostic(
                $"scene recovery exhausted depth={depth}");

            return new RecoveryResult(
                Array.Empty<XDocument>(),
                0,
                1,
                fetched.RequestAttemptCount,
                Array.Empty<string>(),
                string.IsNullOrWhiteSpace(
                    fetched.Error)
                    ? Array.Empty<string>()
                    : [fetched.Error]);
        }

        WriteDiagnostic(
            $"scene recovery subdivide depth={depth}");

        var middleLatitude =
            (
                bounds.South +
                bounds.North
            ) /
            2.0;

        var middleLongitude =
            (
                bounds.West +
                bounds.East
            ) /
            2.0;

        var children =
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
            };

        var documents =
            new List<XDocument>();

        var successfulChunks =
            0;

        var failedChunks =
            0;

        var requestAttempts =
            fetched.RequestAttemptCount;

        var endpoints =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        var failures =
            new List<string>();

        if (
            !string.IsNullOrWhiteSpace(
                fetched.Error))
        {
            failures.Add(
                fetched.Error);
        }

        var childResults =
            await Task
                .WhenAll(
                    children
                        .Select(
                            child =>
                                DownloadChunkWithRecoveryAsync(
                                    child,
                                    depth + 1,
                                    requestConcurrency,
                                    fetched.UnavailableEndpoints,
                                    onAttempt,
                                    cancellationToken)))
                .ConfigureAwait(false);

        foreach (var childResult in childResults)
        {
            documents.AddRange(
                childResult.Documents);

            successfulChunks +=
                childResult.SuccessfulChunkCount;

            failedChunks +=
                childResult.FailedChunkCount;

            requestAttempts +=
                childResult.RequestAttemptCount;

            foreach (var endpoint in childResult.UsedEndpoints)
            {
                endpoints.Add(endpoint);
            }

            failures.AddRange(
                childResult.Failures);
        }

        return new RecoveryResult(
            documents,
            successfulChunks,
            failedChunks,
            requestAttempts,
            endpoints.ToArray(),
            failures);
    }

    private async Task<ChunkFetchResult>
        TryDownloadChunkAsync(
            Bounds bounds,
            SemaphoreSlim requestConcurrency,
            IReadOnlyCollection<string> inheritedUnavailableEndpoints,
            Action<Uri, int, int>? onAttempt,
            CancellationToken cancellationToken)
    {
        var query =
            BuildQuery(bounds);

        var errors =
            new List<string>();

        var unavailableEndpoints =
            new HashSet<string>(
                inheritedUnavailableEndpoints,
                StringComparer.OrdinalIgnoreCase);

        var canRecoverBySubdivision =
            false;

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
            endpointOffset < _endpoints.Count;
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

            var endpointKey =
                endpoint.GetLeftPart(
                    UriPartial.Authority);

            if (
                unavailableEndpoints
                    .Contains(
                        endpointKey))
            {
                continue;
            }

            attemptCount++;

            onAttempt?.Invoke(
                endpoint,
                attemptCount,
                Math.Max(
                    1,
                    _endpoints.Count -
                        unavailableEndpoints.Count));

            await requestConcurrency
                .WaitAsync(
                    cancellationToken)
                .ConfigureAwait(false);

            using var attemptCancellation =
                CancellationTokenSource
                    .CreateLinkedTokenSource(
                        cancellationToken);

            attemptCancellation.CancelAfter(
                EndpointAttemptTimeout);

            try
            {
                using var content =
                    new FormUrlEncodedContent(
                        new Dictionary<string, string>
                        {
                            ["data"] = query
                        });

                using var request =
                    new HttpRequestMessage(
                        HttpMethod.Post,
                        endpoint)
                    {
                        Content =
                            content
                    };

                using var response =
                    await _httpClient
                        .SendAsync(
                            request,
                            HttpCompletionOption
                                .ResponseHeadersRead,
                            attemptCancellation.Token)
                        .ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    var statusCode =
                        (int)response.StatusCode;

                    if (
                        statusCode is
                            408 or
                            429 or
                            500 or
                            502 or
                            503 or
                            504)
                    {
                        canRecoverBySubdivision =
                            true;
                    }

                    errors.Add(
                        $"{endpoint.Host}: HTTP {statusCode}");

                    continue;
                }

                var xml =
                    await response.Content
                        .ReadAsStringAsync(
                            attemptCancellation.Token)
                        .ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(xml))
                {
                    errors.Add(
                        $"{endpoint.Host}: resposta vazia");

                    continue;
                }

                try
                {
                    var document =
                        ParseOsm(xml);

                    return new ChunkFetchResult(
                        document,
                        endpointKey,
                        attemptCount,
                        null,
                        canRecoverBySubdivision,
                        unavailableEndpoints.ToArray());
                }
                catch (
                    Exception exception)
                    when (
                        exception is
                            InvalidDataException or
                            XmlException)
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
                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }

                unavailableEndpoints.Add(
                    endpointKey);

                errors.Add(
                    exception is TaskCanceledException &&
                    attemptCancellation.IsCancellationRequested
                        ? $"{endpoint.Host}: timeout após {EndpointAttemptTimeout.TotalSeconds:0}s"
                        : $"{endpoint.Host}: {exception.Message}");
            }
            finally
            {
                requestConcurrency
                    .Release();
            }
        }

        return new ChunkFetchResult(
            null,
            null,
            attemptCount,
            errors.Count == 0
                ? "Nenhum endpoint Overpass saudável permaneceu para este bloco."
                : string.Join(
                    " / ",
                    errors),
            canRecoverBySubdivision,
            unavailableEndpoints.ToArray());
    }

    private static string BuildQuery(
        Bounds bounds)
    {
        var invariant =
            CultureInfo.InvariantCulture;

        var bbox =
            string.Create(
                invariant,
                $"{bounds.South:G17},{bounds.West:G17},{bounds.North:G17},{bounds.East:G17}");

        return
            "[out:xml][timeout:20];(" +
            $"way[\"highway\"]({bbox});" +
            $"way[\"building\"]({bbox});" +
            $"relation[\"building\"]({bbox});" +
            $"node[\"natural\"~\"^(tree|shrub)$\"]({bbox});" +
            $"way[\"natural\"=\"tree_row\"]({bbox});" +
            $"way[\"barrier\"=\"hedge\"]({bbox});" +
            $"way[\"landuse\"=\"forest\"]({bbox});" +
            $"way[\"natural\"~\"^(wood|scrub)$\"]({bbox});" +
            $"relation[\"landuse\"=\"forest\"]({bbox});" +
            $"relation[\"natural\"~\"^(wood|scrub)$\"]({bbox});" +
            $"node[\"highway\"=\"street_lamp\"]({bbox});" +
            $"node[\"power\"~\"^(pole|tower)$\"]({bbox});" +
            $"node[\"traffic_sign\"]({bbox});" +
            $"node[\"highway\"=\"traffic_sign\"]({bbox});" +
            $"node[\"highway\"=\"bus_stop\"]({bbox});" +
            $"node[\"public_transport\"=\"platform\"]({bbox});" +
            $"node[\"amenity\"=\"shelter\"]({bbox});" +
            $"node[\"shelter\"=\"yes\"]({bbox});" +
            $"node[\"amenity\"=\"bench\"]({bbox});" +
            $"node[\"amenity\"=\"waste_basket\"]({bbox});" +
            $"node[\"barrier\"=\"bollard\"]({bbox});" +
            $"node[\"emergency\"=\"fire_hydrant\"]({bbox});" +
            $"way[\"barrier\"~\"^(wall|fence|guard_rail)$\"]({bbox});" +
            $"way[\"highway\"=\"footway\"][\"footway\"=\"sidewalk\"]({bbox});" +
            $"way[\"highway\"=\"service\"][\"service\"=\"driveway\"]({bbox});" +
            $"way[\"amenity\"=\"parking\"]({bbox});" +
            $"relation[\"amenity\"=\"parking\"]({bbox});" +
            ");out body;>;out skel qt;";
    }

    private static XDocument ParseOsm(
        string xml)
    {
        using var textReader =
            new StringReader(xml);

        using var reader =
            XmlReader.Create(
                textReader,
                new XmlReaderSettings
                {
                    DtdProcessing =
                        DtdProcessing.Prohibit,
                    XmlResolver =
                        null,
                    MaxCharactersInDocument =
                        192L *
                        1024 *
                        1024
                });

        var document =
            XDocument.Load(
                reader,
                LoadOptions.None);

        if (
            document.Root is null ||
            !string.Equals(
                document.Root.Name.LocalName,
                "osm",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "osmRootInvalid");
        }

        return document;
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
                0.05,
                Math.Cos(latitudeRadians));

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

        return (
            rows,
            columns
        );
    }

    private static void ValidateBounds(
        double south,
        double west,
        double north,
        double east)
    {
        if (
            !double.IsFinite(south) ||
            !double.IsFinite(west) ||
            !double.IsFinite(north) ||
            !double.IsFinite(east) ||
            south < -90 ||
            north > 90 ||
            west < -180 ||
            east > 180 ||
            south >= north ||
            west >= east)
        {
            throw new ArgumentException(
                "overpassBoundsInvalid");
        }
    }

    private static double Lerp(
        double start,
        double end,
        double amount) =>
        start +
        (
            end -
            start
        ) *
        amount;

    private static HttpClient CreateSharedHttpClient()
    {
        var handler =
            new SocketsHttpHandler
            {
                ConnectTimeout =
                    EndpointConnectTimeout,
                MaxConnectionsPerServer =
                    MaximumConcurrentChunkRequests,
                PooledConnectionLifetime =
                    TimeSpan.FromMinutes(5),
                PooledConnectionIdleTimeout =
                    TimeSpan.FromMinutes(2),
                AutomaticDecompression =
                    System.Net.DecompressionMethods.GZip |
                    System.Net.DecompressionMethods.Deflate |
                    System.Net.DecompressionMethods.Brotli
            };

        var client =
            new HttpClient(
                handler,
                disposeHandler:
                    true)
            {
                Timeout =
                    System.Threading.Timeout
                        .InfiniteTimeSpan
            };

        client.DefaultRequestHeaders
            .UserAgent
            .ParseAdd(
                "OMSI-Map-Studio/real-world-scene");

        client.DefaultRequestHeaders
            .Accept
            .ParseAdd(
                "application/xml,text/xml;q=0.9,*/*;q=0.1");

        return client;
    }

    private void WriteDiagnostic(
        string message) =>
        _diagnostic?.Invoke(message);

    private sealed record Bounds(
        double South,
        double West,
        double North,
        double East);

    private sealed record ChunkFetchResult(
        XDocument? Document,
        string? Endpoint,
        int RequestAttemptCount,
        string? Error,
        bool CanRecoverBySubdivision,
        IReadOnlyList<string> UnavailableEndpoints);

    private sealed record RecoveryResult(
        IReadOnlyList<XDocument> Documents,
        int SuccessfulChunkCount,
        int FailedChunkCount,
        int RequestAttemptCount,
        IReadOnlyList<string> UsedEndpoints,
        IReadOnlyList<string> Failures);

    private sealed class SceneAggregate
    {
        private readonly Dictionary<
            string,
            XElement>
            _nodes =
                new(
                    StringComparer.Ordinal);

        private readonly Dictionary<
            string,
            XElement>
            _ways =
                new(
                    StringComparer.Ordinal);

        private readonly Dictionary<
            string,
            XElement>
            _relations =
                new(
                    StringComparer.Ordinal);

        public int NodeCount =>
            _nodes.Count;

        public int WayCount =>
            _ways.Count;

        public int RelationCount =>
            _relations.Count;

        public void Add(
            XDocument document)
        {
            var root =
                document.Root!;

            foreach (var element in root.Elements())
            {
                var id =
                    element.Attribute("id")?.Value;

                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                var target =
                    element.Name.LocalName switch
                    {
                        "node" =>
                            _nodes,
                        "way" =>
                            _ways,
                        "relation" =>
                            _relations,
                        _ =>
                            null
                    };

                if (target is not null)
                {
                    target[id] =
                        new XElement(
                            element);
                }
            }
        }

        public string BuildXml()
        {
            var root =
                new XElement(
                    "osm",
                    new XAttribute(
                        "version",
                        "0.6"),
                    new XAttribute(
                        "generator",
                        "OMSI Map Studio"));

            AddElements(
                root,
                _nodes);

            AddElements(
                root,
                _ways);

            AddElements(
                root,
                _relations);

            return new XDocument(root)
                .ToString(
                    SaveOptions.DisableFormatting);
        }

        private static void AddElements(
            XElement root,
            IReadOnlyDictionary<
                string,
                XElement> source)
        {
            foreach (
                var pair in
                    source.OrderBy(
                        pair =>
                            ParseSortKey(
                                pair.Key)))
            {
                root.Add(
                    new XElement(
                        pair.Value));
            }
        }

        private static (
            long Numeric,
            string Text
        ) ParseSortKey(
            string value) =>
            long.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var numeric)
                ? (
                    numeric,
                    string.Empty
                )
                : (
                    long.MaxValue,
                    value
                );
    }
}
