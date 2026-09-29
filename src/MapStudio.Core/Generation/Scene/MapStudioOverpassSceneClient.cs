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

public sealed class MapStudioOverpassSceneClient
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
            TimeSpan.FromSeconds(20);

    private static readonly TimeSpan
        MaximumDownloadDuration =
            TimeSpan.FromMinutes(2);

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
            CancellationToken cancellationToken = default)
    {
        ValidateBounds(
            south,
            west,
            north,
            east);

        using var budgetCancellation =
            CancellationTokenSource
                .CreateLinkedTokenSource(
                    cancellationToken);

        budgetCancellation.CancelAfter(
            MaximumDownloadDuration);

        WriteDiagnostic(
            $"scene download begin bounds={south:F6},{west:F6},{north:F6},{east:F6}");

        try
        {
            return await DownloadCoreAsync(
                    south,
                    west,
                    north,
                    east,
                    budgetCancellation.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (
                !cancellationToken.IsCancellationRequested &&
                budgetCancellation.IsCancellationRequested)
        {
            throw new HttpRequestException(
                $"Importação de cenário OpenStreetMap excedeu o limite de {MaximumDownloadDuration.TotalSeconds:0} segundos.");
        }
    }

    private async Task<MapStudioOverpassSceneDownloadResult>
        DownloadCoreAsync(
            double south,
            double west,
            double north,
            double east,
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

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                var chunkSouth =
                    Lerp(
                        south,
                        north,
                        row /
                            (double)rows);

                var chunkNorth =
                    Lerp(
                        south,
                        north,
                        (row + 1) /
                            (double)rows);

                var chunkWest =
                    Lerp(
                        west,
                        east,
                        column /
                            (double)columns);

                var chunkEast =
                    Lerp(
                        west,
                        east,
                        (column + 1) /
                            (double)columns);

                var result =
                    await DownloadChunkWithRecoveryAsync(
                            new Bounds(
                                chunkSouth,
                                chunkWest,
                                chunkNorth,
                                chunkEast),
                            depth: 0,
                            cancellationToken)
                        .ConfigureAwait(false);

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
            CancellationToken cancellationToken)
    {
        WriteDiagnostic(
            $"scene recovery attempt depth={depth} bounds={bounds.South:F6},{bounds.West:F6},{bounds.North:F6},{bounds.East:F6}");

        var fetched =
            await TryDownloadChunkAsync(
                    bounds,
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

        if (depth >= MaximumRecoveryDepth)
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

        foreach (var child in children)
        {
            var childResult =
                await DownloadChunkWithRecoveryAsync(
                        child,
                        depth + 1,
                        cancellationToken)
                    .ConfigureAwait(false);

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
            CancellationToken cancellationToken)
    {
        var query =
            BuildQuery(bounds);

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

            attemptCount++;

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

                using var response =
                    await _httpClient
                        .PostAsync(
                            endpoint,
                            content,
                            attemptCancellation.Token)
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
                        endpoint
                            .GetLeftPart(
                                UriPartial.Authority),
                        attemptCount,
                        null);
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

                errors.Add(
                    exception is TaskCanceledException &&
                    attemptCancellation.IsCancellationRequested
                        ? $"{endpoint.Host}: timeout após {EndpointAttemptTimeout.TotalSeconds:0}s"
                        : $"{endpoint.Host}: {exception.Message}");
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
                "OMSI-Map-Studio/real-world-scene");

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
        string? Error);

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

                target?[
                    id
                ] =
                    new XElement(
                        element);
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
