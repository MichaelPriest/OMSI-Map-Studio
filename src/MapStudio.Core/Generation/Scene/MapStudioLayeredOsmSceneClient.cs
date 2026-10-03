using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace MapStudio.Core.Generation.Scene;

public enum MapStudioOsmSceneLayer
{
    Roads,
    Buildings,
    Infrastructure,
    Vegetation,
    Furniture
}

public sealed class MapStudioLayeredOsmSceneClient
{
    private const double EarthRadiusMeters =
        6_378_137.0;

    private const int MaximumChunksPerAxis =
        24;

    private const int MaximumConcurrentChunkRequests =
        2;

    private const int MaximumRecoveryDepth =
        2;

    private const int MaximumOverpassAttemptsPerChunk =
        8;

    private const int MaximumLayerRecoveryPasses =
        2;

    // Historical marker kept for the Test 10.121 compatibility gate.
    // v3 intentionally invalidates v2 furniture chunks because the query now
    // includes explicit STOP/GIVE_WAY nodes.
    private const string PreviousCacheSchemaVersion =
        "osm-layers-v2";

    private const string CacheSchemaVersion =
        "osm-layers-v3";

    private static readonly TimeSpan
        EndpointAttemptTimeout =
            TimeSpan.FromSeconds(18);

    private static readonly TimeSpan
        EndpointConnectTimeout =
            TimeSpan.FromSeconds(8);

    private static readonly TimeSpan
        LayerRecoveryBaseDelay =
            TimeSpan.FromMilliseconds(1500);

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

    private static readonly Uri
        DefaultDirectMapEndpoint =
            new(
                "https://api.openstreetmap.org/api/0.6/map");

    private static readonly LayerDefinition[]
        LayerDefinitions =
        [
            new(
                MapStudioOsmSceneLayer.Roads,
                "roads",
                "Vias",
                320.0),
            new(
                MapStudioOsmSceneLayer.Buildings,
                "buildings",
                "Prédios",
                280.0),
            new(
                MapStudioOsmSceneLayer.Infrastructure,
                "infrastructure",
                "Infraestrutura",
                320.0),
            new(
                MapStudioOsmSceneLayer.Vegetation,
                "vegetation",
                "Vegetação",
                360.0),
            new(
                MapStudioOsmSceneLayer.Furniture,
                "furniture",
                "Mobiliário",
                280.0)
        ];

    private readonly HttpClient _httpClient;
    private readonly IReadOnlyList<Uri> _endpoints;
    private readonly Uri? _directMapEndpoint;
    private readonly Action<string>? _diagnostic;

    private readonly ConcurrentDictionary<
        string,
        EndpointHealth>
        _endpointHealth =
            new(
                StringComparer.OrdinalIgnoreCase);

    private int _endpointRotation;

    public MapStudioLayeredOsmSceneClient()
        : this(
            SharedHttpClient,
            DefaultEndpoints,
            DefaultDirectMapEndpoint,
            null)
    {
    }

    public MapStudioLayeredOsmSceneClient(
        Action<string> diagnostic)
        : this(
            SharedHttpClient,
            DefaultEndpoints,
            DefaultDirectMapEndpoint,
            diagnostic)
    {
    }

    public MapStudioLayeredOsmSceneClient(
        HttpClient httpClient,
        IReadOnlyList<Uri> endpoints,
        Uri? directMapEndpoint = null,
        Action<string>? diagnostic = null)
    {
        ArgumentNullException.ThrowIfNull(
            httpClient);

        ArgumentNullException.ThrowIfNull(
            endpoints);

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

        if (
            directMapEndpoint is not null &&
            (
                !directMapEndpoint.IsAbsoluteUri ||
                directMapEndpoint.Scheme !=
                    Uri.UriSchemeHttps
            ))
        {
            throw new ArgumentException(
                "osmDirectMapEndpointInvalid",
                nameof(directMapEndpoint));
        }

        _httpClient =
            httpClient;

        _endpoints =
            endpoints.ToArray();

        _directMapEndpoint =
            directMapEndpoint;

        _diagnostic =
            diagnostic;

        foreach (var endpoint in _endpoints)
        {
            _endpointHealth.TryAdd(
                GetEndpointKey(
                    endpoint),
                new EndpointHealth());
        }
    }

    public async Task<MapStudioOverpassSceneDownloadResult>
        DownloadAsync(
            double south,
            double west,
            double north,
            double east,
            string? cacheDirectory = null,
            CancellationToken cancellationToken = default,
            IProgress<MapStudioOverpassSceneDownloadProgress>?
                progress = null)
    {
        ValidateBounds(
            south,
            west,
            north,
            east);

        var cacheRoot =
            string.IsNullOrWhiteSpace(
                cacheDirectory)
                ? null
                : Path.GetFullPath(
                    cacheDirectory);

        if (cacheRoot is not null)
        {
            Directory.CreateDirectory(
                cacheRoot);
        }

        WriteDiagnostic(
            $"osm layered begin bounds={south:F6},{west:F6},{north:F6},{east:F6} cache={(cacheRoot is null ? "off" : cacheRoot)}");

        using var requestConcurrency =
            new SemaphoreSlim(
                MaximumConcurrentChunkRequests,
                MaximumConcurrentChunkRequests);

        var aggregate =
            new SceneAggregate();

        var successfulChunks =
            0;

        var failedChunks =
            0;

        var requestAttempts =
            0;

        var usedEndpoints =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var layer in LayerDefinitions)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            var primaryChunks =
                CreatePrimaryChunks(
                    south,
                    west,
                    north,
                    east,
                    layer.TargetChunkSpanMeters);

            var completedPrimaryChunks =
                0;

            var reportedSuccessfulLeafChunks =
                0;

            var reportedFailedLeafChunks =
                0;

            var reportedRequestAttempts =
                0;

            progress?.Report(
                new MapStudioOverpassSceneDownloadProgress(
                    0,
                    primaryChunks.Count,
                    0,
                    0,
                    0,
                    $"OpenStreetMap → {layer.DisplayName} → preparando {primaryChunks.Count} bloco(s)..."));

            var tasks =
                primaryChunks
                    .Select(
                        async (
                            bounds,
                            index) =>
                        {
                            var primaryNumber =
                                index + 1;

                            var result =
                                await DownloadChunkWithRecoveryAsync(
                                        layer,
                                        bounds,
                                        primaryNumber,
                                        primaryChunks.Count,
                                        primaryNumber
                                            .ToString(
                                                CultureInfo
                                                    .InvariantCulture),
                                        depth: 0,
                                        cacheRoot,
                                        requestConcurrency,
                                        attempt =>
                                        {
                                            var attempts =
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
                                                    attempts,
                                                    $"OpenStreetMap → {layer.DisplayName} → bloco {attempt.ChunkLabel}/{primaryChunks.Count} → servidor {attempt.Endpoint.Host} → tentativa {attempt.Attempt}/{attempt.TotalAttempts}"));
                                        },
                                        status =>
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
                                                    System.Threading.Volatile
                                                        .Read(
                                                            ref reportedRequestAttempts),
                                                    status)),
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
                                    $"OpenStreetMap → {layer.DisplayName} → bloco {primaryNumber}/{primaryChunks.Count} concluído"));

                            return result;
                        })
                    .ToArray();

            var layerResults =
                await Task
                    .WhenAll(
                        tasks)
                    .ConfigureAwait(false);

            for (
                var recoveryPass = 1;
                recoveryPass <=
                    MaximumLayerRecoveryPasses &&
                layerResults.Any(
                    result =>
                        result.FailedChunkCount >
                            0);
                recoveryPass++)
            {
                var failedPrimaryCount =
                    layerResults.Count(
                        result =>
                            result.FailedChunkCount >
                                0);

                var delay =
                    TimeSpan.FromMilliseconds(
                        LayerRecoveryBaseDelay
                            .TotalMilliseconds *
                        recoveryPass *
                        recoveryPass);

                progress?.Report(
                    new MapStudioOverpassSceneDownloadProgress(
                        primaryChunks.Count,
                        primaryChunks.Count,
                        System.Threading.Volatile
                            .Read(
                                ref reportedSuccessfulLeafChunks),
                        System.Threading.Volatile
                            .Read(
                                ref reportedFailedLeafChunks),
                        System.Threading.Volatile
                            .Read(
                                ref reportedRequestAttempts),
                        $"OpenStreetMap → {layer.DisplayName} → recuperação {recoveryPass}/{MaximumLayerRecoveryPasses}: repetindo apenas {failedPrimaryCount} bloco(s) incompleto(s) em {delay.TotalSeconds:0.0}s; cache parcial será reutilizado"));

                await Task
                    .Delay(
                        delay,
                        cancellationToken)
                    .ConfigureAwait(false);

                var retryTasks =
                    layerResults
                        .Select(
                            (
                                previous,
                                index) =>
                            {
                                if (
                                    previous
                                        .FailedChunkCount ==
                                    0)
                                {
                                    return Task
                                        .FromResult(
                                            previous);
                                }

                                var primaryNumber =
                                    index +
                                    1;

                                return DownloadChunkWithRecoveryAsync(
                                    layer,
                                    primaryChunks[index],
                                    primaryNumber,
                                    primaryChunks.Count,
                                    primaryNumber
                                        .ToString(
                                            CultureInfo
                                                .InvariantCulture),
                                    depth:
                                        0,
                                    cacheRoot,
                                    requestConcurrency,
                                    attempt =>
                                    {
                                        var attempts =
                                            System.Threading.Interlocked
                                                .Increment(
                                                    ref reportedRequestAttempts);

                                        progress?.Report(
                                            new MapStudioOverpassSceneDownloadProgress(
                                                primaryChunks.Count,
                                                primaryChunks.Count,
                                                System.Threading.Volatile
                                                    .Read(
                                                        ref reportedSuccessfulLeafChunks),
                                                System.Threading.Volatile
                                                    .Read(
                                                        ref reportedFailedLeafChunks),
                                                attempts,
                                                $"OpenStreetMap → {layer.DisplayName} → recuperação {recoveryPass}/{MaximumLayerRecoveryPasses} → bloco {attempt.ChunkLabel}/{primaryChunks.Count} → servidor {attempt.Endpoint.Host} → tentativa {attempt.Attempt}/{attempt.TotalAttempts}"));
                                    },
                                    status =>
                                        progress?.Report(
                                            new MapStudioOverpassSceneDownloadProgress(
                                                primaryChunks.Count,
                                                primaryChunks.Count,
                                                System.Threading.Volatile
                                                    .Read(
                                                        ref reportedSuccessfulLeafChunks),
                                                System.Threading.Volatile
                                                    .Read(
                                                        ref reportedFailedLeafChunks),
                                                System.Threading.Volatile
                                                    .Read(
                                                        ref reportedRequestAttempts),
                                                status)),
                                    cancellationToken);
                            })
                        .ToArray();

                layerResults =
                    await Task
                        .WhenAll(
                            retryTasks)
                        .ConfigureAwait(false);

                reportedSuccessfulLeafChunks =
                    layerResults.Sum(
                        result =>
                            result.SuccessfulChunkCount);

                reportedFailedLeafChunks =
                    layerResults.Sum(
                        result =>
                            result.FailedChunkCount);
            }

            var layerFailures =
                new List<string>();

            var layerCacheHits =
                0;

            var layerNetworkChunks =
                0;

            foreach (var result in layerResults)
            {
                successfulChunks +=
                    result.SuccessfulChunkCount;

                failedChunks +=
                    result.FailedChunkCount;

                layerCacheHits +=
                    result.CacheHitCount;

                layerNetworkChunks +=
                    result.NetworkSuccessCount;

                foreach (
                    var endpoint
                    in result.UsedEndpoints)
                {
                    usedEndpoints.Add(
                        endpoint);
                }

                foreach (
                    var document
                    in result.Documents)
                {
                    aggregate.Add(
                        document);
                }

                layerFailures.AddRange(
                    result.Failures);
            }

            requestAttempts +=
                System.Threading.Volatile
                    .Read(
                        ref reportedRequestAttempts);

            if (
                layerResults.Sum(
                    result =>
                        result.FailedChunkCount) >
                0)
            {
                throw new HttpRequestException(
                    $"OpenStreetMap → {layer.DisplayName}: download incompleto. " +
                    string.Join(
                        " / ",
                        layerFailures
                            .Where(
                                failure =>
                                    !string.IsNullOrWhiteSpace(
                                        failure))
                            .Distinct(
                                StringComparer.OrdinalIgnoreCase)
                            .Take(8)));
            }

            progress?.Report(
                new MapStudioOverpassSceneDownloadProgress(
                    primaryChunks.Count,
                    primaryChunks.Count,
                    layerResults.Sum(
                        result =>
                            result.SuccessfulChunkCount),
                    0,
                    System.Threading.Volatile
                        .Read(
                            ref reportedRequestAttempts),
                    $"OpenStreetMap → {layer.DisplayName} → {primaryChunks.Count}/{primaryChunks.Count} blocos validados · cache {layerCacheHits} · rede {layerNetworkChunks}"));
        }

        if (successfulChunks == 0)
        {
            throw new HttpRequestException(
                "Nenhuma camada OpenStreetMap pôde ser concluída.");
        }

        var xml =
            aggregate.BuildXml();

        WriteDiagnostic(
            $"osm layered complete nodes={aggregate.NodeCount} ways={aggregate.WayCount} relations={aggregate.RelationCount} chunks={successfulChunks} attempts={requestAttempts}");

        progress?.Report(
            new MapStudioOverpassSceneDownloadProgress(
                1,
                1,
                successfulChunks,
                failedChunks,
                requestAttempts,
                "OpenStreetMap → todas as camadas foram baixadas e validadas; iniciando staging transacional."));

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
            LayerDefinition layer,
            Bounds bounds,
            int primaryNumber,
            int primaryTotal,
            string chunkLabel,
            int depth,
            string? cacheRoot,
            SemaphoreSlim requestConcurrency,
            Action<AttemptUpdate> onAttempt,
            Action<string> onStatus,
            CancellationToken cancellationToken)
    {
        cancellationToken
            .ThrowIfCancellationRequested();

        var cached =
            await TryReadCacheAsync(
                    layer,
                    bounds,
                    cacheRoot,
                    cancellationToken)
                .ConfigureAwait(false);

        if (cached is not null)
        {
            WriteDiagnostic(
                $"osm layer={layer.Slug} chunk={chunkLabel}/{primaryTotal} source=cache");

            onStatus(
                $"OpenStreetMap → {layer.DisplayName} → bloco {chunkLabel}/{primaryTotal} → cache local validado");

            return new RecoveryResult(
                [cached],
                1,
                0,
                0,
                Array.Empty<string>(),
                Array.Empty<string>(),
                1,
                0);
        }

        var fetched =
            await TryDownloadOverpassAsync(
                    layer,
                    bounds,
                    chunkLabel,
                    primaryTotal,
                    requestConcurrency,
                    onAttempt,
                    onStatus,
                    cancellationToken)
                .ConfigureAwait(false);

        if (fetched.Document is not null)
        {
            await WriteCacheAsync(
                    layer,
                    bounds,
                    cacheRoot,
                    fetched.Document,
                    cancellationToken)
                .ConfigureAwait(false);

            return new RecoveryResult(
                [fetched.Document],
                1,
                0,
                fetched.RequestAttemptCount,
                fetched.Endpoint is null
                    ? Array.Empty<string>()
                    : [fetched.Endpoint],
                Array.Empty<string>(),
                0,
                1);
        }

        var directFallback =
            await TryDownloadDirectMapAsync(
                    layer,
                    bounds,
                    chunkLabel,
                    primaryTotal,
                    requestConcurrency,
                    onAttempt,
                    onStatus,
                    cancellationToken)
                .ConfigureAwait(false);

        if (directFallback.Document is not null)
        {
            await WriteCacheAsync(
                    layer,
                    bounds,
                    cacheRoot,
                    directFallback.Document,
                    cancellationToken)
                .ConfigureAwait(false);

            return new RecoveryResult(
                [directFallback.Document],
                1,
                0,
                fetched.RequestAttemptCount +
                    directFallback.RequestAttemptCount,
                directFallback.Endpoint is null
                    ? Array.Empty<string>()
                    : [directFallback.Endpoint],
                Array.Empty<string>(),
                0,
                1);
        }

        var canRecover =
            fetched.CanRecoverBySubdivision ||
            directFallback.CanRecoverBySubdivision;

        var combinedFailure =
            string.Join(
                " / ",
                new[]
                {
                    fetched.Error,
                    directFallback.Error
                }
                    .Where(
                        error =>
                            !string.IsNullOrWhiteSpace(
                                error)));

        if (
            !canRecover ||
            depth >= MaximumRecoveryDepth)
        {
            var failure =
                $"camada {layer.DisplayName}, bloco {chunkLabel}/{primaryTotal}: " +
                (
                    string.IsNullOrWhiteSpace(
                        combinedFailure)
                        ? "nenhuma fonte saudável permaneceu"
                        : combinedFailure
                );

            WriteDiagnostic(
                $"osm layer={layer.Slug} chunk={chunkLabel}/{primaryTotal} failed depth={depth} error={failure}");

            return new RecoveryResult(
                Array.Empty<XDocument>(),
                0,
                1,
                fetched.RequestAttemptCount +
                    directFallback.RequestAttemptCount,
                Array.Empty<string>(),
                [failure],
                0,
                0);
        }

        onStatus(
            $"OpenStreetMap → {layer.DisplayName} → bloco {chunkLabel}/{primaryTotal} → subdividindo para recuperação");

        var children =
            Subdivide(
                bounds);

        var childResults =
            await Task
                .WhenAll(
                    children
                        .Select(
                            (
                                child,
                                index) =>
                                DownloadChunkWithRecoveryAsync(
                                    layer,
                                    child,
                                    primaryNumber,
                                    primaryTotal,
                                    $"{chunkLabel}.{index + 1}",
                                    depth + 1,
                                    cacheRoot,
                                    requestConcurrency,
                                    onAttempt,
                                    onStatus,
                                    cancellationToken)))
                .ConfigureAwait(false);

        var documents =
            new List<XDocument>();

        var successfulChunks =
            0;

        var failedChunks =
            0;

        var requestAttempts =
            fetched.RequestAttemptCount +
            directFallback.RequestAttemptCount;

        var cacheHits =
            0;

        var networkSuccesses =
            0;

        var endpoints =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        var failures =
            new List<string>();

        foreach (var result in childResults)
        {
            documents.AddRange(
                result.Documents);

            successfulChunks +=
                result.SuccessfulChunkCount;

            failedChunks +=
                result.FailedChunkCount;

            requestAttempts +=
                result.RequestAttemptCount;

            cacheHits +=
                result.CacheHitCount;

            networkSuccesses +=
                result.NetworkSuccessCount;

            foreach (
                var endpoint
                in result.UsedEndpoints)
            {
                endpoints.Add(
                    endpoint);
            }

            failures.AddRange(
                result.Failures);
        }

        return new RecoveryResult(
            documents,
            successfulChunks,
            failedChunks,
            requestAttempts,
            endpoints.ToArray(),
            failures,
            cacheHits,
            networkSuccesses);
    }

    private async Task<ChunkFetchResult>
        TryDownloadOverpassAsync(
            LayerDefinition layer,
            Bounds bounds,
            string chunkLabel,
            int primaryTotal,
            SemaphoreSlim requestConcurrency,
            Action<AttemptUpdate> onAttempt,
            Action<string> onStatus,
            CancellationToken cancellationToken)
    {
        var query =
            BuildQuery(
                layer,
                bounds);

        var attemptedEndpoints =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        var errors =
            new List<string>();

        var canRecover =
            false;

        var requestAttempts =
            0;

        var maximumAttempts =
            Math.Min(
                MaximumOverpassAttemptsPerChunk,
                _endpoints.Count);

        for (
            var attempt = 1;
            attempt <= maximumAttempts;
            attempt++)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            var endpoint =
                SelectHealthyEndpoint(
                    attemptedEndpoints);

            if (endpoint is null)
            {
                break;
            }

            var endpointKey =
                GetEndpointKey(
                    endpoint);

            attemptedEndpoints.Add(
                endpointKey);

            requestAttempts++;

            onAttempt(
                new AttemptUpdate(
                    endpoint,
                    chunkLabel,
                    attempt,
                    maximumAttempts));

            var response =
                await SendOverpassAttemptAsync(
                        endpoint,
                        query,
                        requestConcurrency,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (response.Document is not null)
            {
                MarkEndpointSuccess(
                    endpointKey);

                WriteDiagnostic(
                    $"osm layer={layer.Slug} chunk={chunkLabel}/{primaryTotal} endpoint={endpoint.Host} attempt={attempt} status=200 elapsedMs={response.Elapsed.TotalMilliseconds:0} bytes={response.ByteCount}");

                onStatus(
                    $"OpenStreetMap → {layer.DisplayName} → bloco {chunkLabel}/{primaryTotal} → {endpoint.Host} → {FormatBytes(response.ByteCount)} em {response.Elapsed.TotalSeconds:0.0}s");

                return new ChunkFetchResult(
                    response.Document,
                    endpointKey,
                    requestAttempts,
                    null,
                    canRecover,
                    Array.Empty<string>());
            }

            canRecover |=
                response.CanRecoverBySubdivision;

            if (
                !string.IsNullOrWhiteSpace(
                    response.Error))
            {
                errors.Add(
                    response.Error);
            }

            if (response.StatusCode == 429)
            {
                var retryAfter =
                    response.RetryAfter ??
                    TimeSpan.FromSeconds(30);

                MarkEndpointRateLimited(
                    endpointKey,
                    retryAfter);

                WriteDiagnostic(
                    $"osm layer={layer.Slug} chunk={chunkLabel}/{primaryTotal} endpoint={endpoint.Host} status=429 elapsedMs={response.Elapsed.TotalMilliseconds:0} retryAfterSeconds={retryAfter.TotalSeconds:0}");

                onStatus(
                    $"OpenStreetMap → {layer.DisplayName} → bloco {chunkLabel}/{primaryTotal} → {endpoint.Host} respondeu 429 · Retry-After {retryAfter.TotalSeconds:0}s · servidor em cooldown");
            }
            else if (response.TransportFailure)
            {
                MarkEndpointTransportFailure(
                    endpointKey);

                WriteDiagnostic(
                    $"osm layer={layer.Slug} chunk={chunkLabel}/{primaryTotal} endpoint={endpoint.Host} transport-failure elapsedMs={response.Elapsed.TotalMilliseconds:0} error={response.Error}");
            }
            else
            {
                MarkEndpointFailure(
                    endpointKey);

                WriteDiagnostic(
                    $"osm layer={layer.Slug} chunk={chunkLabel}/{primaryTotal} endpoint={endpoint.Host} status={response.StatusCode?.ToString(CultureInfo.InvariantCulture) ?? "error"} elapsedMs={response.Elapsed.TotalMilliseconds:0} bytes={response.ByteCount} error={response.Error}");
            }

            if (
                attempt <
                    maximumAttempts &&
                response.StatusCode is
                    408 or
                    500 or
                    502 or
                    503 or
                    504)
            {
                var backoff =
                    TimeSpan.FromMilliseconds(
                        350 *
                        attempt *
                        attempt);

                onStatus(
                    $"OpenStreetMap → {layer.DisplayName} → bloco {chunkLabel}/{primaryTotal} → backoff {backoff.TotalMilliseconds:0} ms antes de trocar de servidor");

                await Task
                    .Delay(
                        backoff,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return new ChunkFetchResult(
            null,
            null,
            requestAttempts,
            errors.Count == 0
                ? "Nenhum endpoint Overpass saudável estava disponível."
                : string.Join(
                    " / ",
                    errors),
            canRecover,
            Array.Empty<string>());
    }

    private async Task<ChunkFetchResult>
        TryDownloadDirectMapAsync(
            LayerDefinition layer,
            Bounds bounds,
            string chunkLabel,
            int primaryTotal,
            SemaphoreSlim requestConcurrency,
            Action<AttemptUpdate> onAttempt,
            Action<string> onStatus,
            CancellationToken cancellationToken)
    {
        if (_directMapEndpoint is null)
        {
            return new ChunkFetchResult(
                null,
                null,
                0,
                null,
                false,
                Array.Empty<string>());
        }

        var endpointKey =
            GetEndpointKey(
                _directMapEndpoint);

        var directHealth =
            _endpointHealth
                .GetOrAdd(
                    endpointKey,
                    _ =>
                        new EndpointHealth())
                .Snapshot();

        if (
            directHealth.CooldownUntil >
                DateTimeOffset.UtcNow)
        {
            var remaining =
                directHealth.CooldownUntil -
                DateTimeOffset.UtcNow;

            var message =
                $"{_directMapEndpoint.Host}: fallback OSM API em cooldown por mais {Math.Max(1, remaining.TotalSeconds):0}s";

            onStatus(
                $"OpenStreetMap → {layer.DisplayName} → bloco {chunkLabel}/{primaryTotal} → {message}");

            return new ChunkFetchResult(
                null,
                null,
                0,
                message,
                false,
                Array.Empty<string>());
        }

        onAttempt(
            new AttemptUpdate(
                _directMapEndpoint,
                chunkLabel,
                1,
                1));

        var result =
            await SendDirectMapAttemptAsync(
                    bounds,
                    requestConcurrency,
                    cancellationToken)
                .ConfigureAwait(false);

        if (result.Document is not null)
        {
            MarkEndpointSuccess(
                endpointKey);

            WriteDiagnostic(
                $"osm layer={layer.Slug} chunk={chunkLabel}/{primaryTotal} endpoint={_directMapEndpoint.Host} direct-map status=200 elapsedMs={result.Elapsed.TotalMilliseconds:0} bytes={result.ByteCount}");

            onStatus(
                $"OpenStreetMap → {layer.DisplayName} → bloco {chunkLabel}/{primaryTotal} → fallback {_directMapEndpoint.Host} → {FormatBytes(result.ByteCount)} em {result.Elapsed.TotalSeconds:0.0}s");

            return new ChunkFetchResult(
                result.Document,
                endpointKey,
                1,
                null,
                false,
                Array.Empty<string>());
        }

        if (result.StatusCode == 429)
        {
            var retryAfter =
                result.RetryAfter ??
                TimeSpan.FromSeconds(30);

            MarkEndpointRateLimited(
                endpointKey,
                retryAfter);

            onStatus(
                $"OpenStreetMap → {layer.DisplayName} → bloco {chunkLabel}/{primaryTotal} → fallback {_directMapEndpoint.Host} respondeu 429 · Retry-After {retryAfter.TotalSeconds:0}s · servidor em cooldown");
        }
        else if (result.TransportFailure)
        {
            MarkEndpointTransportFailure(
                endpointKey);
        }
        else
        {
            MarkEndpointFailure(
                endpointKey);
        }

        WriteDiagnostic(
            $"osm layer={layer.Slug} chunk={chunkLabel}/{primaryTotal} endpoint={_directMapEndpoint.Host} direct-map status={result.StatusCode?.ToString(CultureInfo.InvariantCulture) ?? "error"} elapsedMs={result.Elapsed.TotalMilliseconds:0} error={result.Error}");

        onStatus(
            $"OpenStreetMap → {layer.DisplayName} → bloco {chunkLabel}/{primaryTotal} → fallback {_directMapEndpoint.Host} falhou: {result.Error}");

        return new ChunkFetchResult(
            null,
            null,
            1,
            result.Error,
            result.CanRecoverBySubdivision,
            Array.Empty<string>());
    }

    private async Task<NetworkAttemptResult>
        SendOverpassAttemptAsync(
            Uri endpoint,
            string query,
            SemaphoreSlim requestConcurrency,
            CancellationToken cancellationToken)
    {
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

        var stopwatch =
            Stopwatch.StartNew();

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
                stopwatch.Stop();

                var statusCode =
                    (int)response.StatusCode;

                return new NetworkAttemptResult(
                    null,
                    statusCode,
                    $"{endpoint.Host}: HTTP {statusCode}",
                    IsRecoverableStatus(
                        statusCode),
                    0,
                    stopwatch.Elapsed,
                    statusCode == 429
                        ? GetRetryAfter(
                            response)
                        : null,
                    false);
            }

            var bytes =
                await response.Content
                    .ReadAsByteArrayAsync(
                        attemptCancellation.Token)
                    .ConfigureAwait(false);

            stopwatch.Stop();

            if (bytes.Length == 0)
            {
                return new NetworkAttemptResult(
                    null,
                    (int)response.StatusCode,
                    $"{endpoint.Host}: resposta vazia",
                    false,
                    0,
                    stopwatch.Elapsed,
                    null,
                    false);
            }

            try
            {
                return new NetworkAttemptResult(
                    ParseOsm(
                        bytes),
                    (int)response.StatusCode,
                    null,
                    false,
                    bytes.LongLength,
                    stopwatch.Elapsed,
                    null,
                    false);
            }
            catch (
                Exception exception)
                when (
                    exception is
                        InvalidDataException or
                        XmlException)
            {
                return new NetworkAttemptResult(
                    null,
                    (int)response.StatusCode,
                    $"{endpoint.Host}: XML inválido ({exception.Message})",
                    false,
                    bytes.LongLength,
                    stopwatch.Elapsed,
                    null,
                    false);
            }
        }
        catch (
            Exception exception)
            when (
                exception is
                    HttpRequestException or
                    TaskCanceledException)
        {
            stopwatch.Stop();

            if (
                cancellationToken
                    .IsCancellationRequested)
            {
                throw;
            }

            return new NetworkAttemptResult(
                null,
                null,
                exception is TaskCanceledException &&
                attemptCancellation
                    .IsCancellationRequested
                    ? $"{endpoint.Host}: timeout após {EndpointAttemptTimeout.TotalSeconds:0}s"
                    : $"{endpoint.Host}: {exception.Message}",
                true,
                0,
                stopwatch.Elapsed,
                null,
                true);
        }
        finally
        {
            requestConcurrency
                .Release();
        }
    }

    private async Task<NetworkAttemptResult>
        SendDirectMapAttemptAsync(
            Bounds bounds,
            SemaphoreSlim requestConcurrency,
            CancellationToken cancellationToken)
    {
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

        var stopwatch =
            Stopwatch.StartNew();

        try
        {
            var invariant =
                CultureInfo.InvariantCulture;

            var bbox =
                string.Create(
                    invariant,
                    $"{bounds.West:G17},{bounds.South:G17},{bounds.East:G17},{bounds.North:G17}");

            var separator =
                string.IsNullOrWhiteSpace(
                    _directMapEndpoint!.Query)
                    ? "?"
                    : "&";

            var requestUri =
                new Uri(
                    _directMapEndpoint +
                    separator +
                    "bbox=" +
                    Uri.EscapeDataString(
                        bbox));

            using var request =
                new HttpRequestMessage(
                    HttpMethod.Get,
                    requestUri);

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
                stopwatch.Stop();

                var statusCode =
                    (int)response.StatusCode;

                return new NetworkAttemptResult(
                    null,
                    statusCode,
                    $"{_directMapEndpoint.Host}: HTTP {statusCode} no fallback OSM API",
                    IsRecoverableStatus(
                        statusCode),
                    0,
                    stopwatch.Elapsed,
                    statusCode == 429
                        ? GetRetryAfter(
                            response)
                        : null,
                    false);
            }

            var bytes =
                await response.Content
                    .ReadAsByteArrayAsync(
                        attemptCancellation.Token)
                    .ConfigureAwait(false);

            stopwatch.Stop();

            if (bytes.Length == 0)
            {
                return new NetworkAttemptResult(
                    null,
                    (int)response.StatusCode,
                    $"{_directMapEndpoint.Host}: resposta vazia no fallback OSM API",
                    false,
                    0,
                    stopwatch.Elapsed,
                    null,
                    false);
            }

            try
            {
                return new NetworkAttemptResult(
                    ParseOsm(
                        bytes),
                    (int)response.StatusCode,
                    null,
                    false,
                    bytes.LongLength,
                    stopwatch.Elapsed,
                    null,
                    false);
            }
            catch (
                Exception exception)
                when (
                    exception is
                        InvalidDataException or
                        XmlException)
            {
                return new NetworkAttemptResult(
                    null,
                    (int)response.StatusCode,
                    $"{_directMapEndpoint.Host}: XML inválido no fallback ({exception.Message})",
                    false,
                    bytes.LongLength,
                    stopwatch.Elapsed,
                    null,
                    false);
            }
        }
        catch (
            Exception exception)
            when (
                exception is
                    HttpRequestException or
                    TaskCanceledException)
        {
            stopwatch.Stop();

            if (
                cancellationToken
                    .IsCancellationRequested)
            {
                throw;
            }

            return new NetworkAttemptResult(
                null,
                null,
                exception is TaskCanceledException &&
                attemptCancellation
                    .IsCancellationRequested
                    ? $"{_directMapEndpoint!.Host}: timeout após {EndpointAttemptTimeout.TotalSeconds:0}s no fallback OSM API"
                    : $"{_directMapEndpoint!.Host}: {exception.Message}",
                true,
                0,
                stopwatch.Elapsed,
                null,
                true);
        }
        finally
        {
            requestConcurrency
                .Release();
        }
    }

    private Uri? SelectHealthyEndpoint(
        IReadOnlySet<string> attemptedEndpoints)
    {
        var now =
            DateTimeOffset.UtcNow;

        var start =
            Math.Abs(
                System.Threading.Interlocked
                    .Increment(
                        ref _endpointRotation) -
                1) %
            _endpoints.Count;

        return _endpoints
            .Select(
                (
                    endpoint,
                    index) =>
                {
                    var endpointKey =
                        GetEndpointKey(
                            endpoint);

                    var health =
                        _endpointHealth
                            .GetOrAdd(
                                endpointKey,
                                _ =>
                                    new EndpointHealth())
                            .Snapshot();

                    return new
                    {
                        Endpoint = endpoint,
                        Key = endpointKey,
                        health.Score,
                        health.CooldownUntil,
                        RotationDistance =
                            (
                                index -
                                start +
                                _endpoints.Count
                            ) %
                            _endpoints.Count
                    };
                })
            .Where(
                candidate =>
                    !attemptedEndpoints
                        .Contains(
                            candidate.Key) &&
                    candidate.CooldownUntil <=
                        now)
            .OrderByDescending(
                candidate =>
                    candidate.Score)
            .ThenBy(
                candidate =>
                    candidate.RotationDistance)
            .Select(
                candidate =>
                    candidate.Endpoint)
            .FirstOrDefault();
    }

    private void MarkEndpointSuccess(
        string endpointKey) =>
        _endpointHealth
            .GetOrAdd(
                endpointKey,
                _ =>
                    new EndpointHealth())
            .MarkSuccess();

    private void MarkEndpointFailure(
        string endpointKey) =>
        _endpointHealth
            .GetOrAdd(
                endpointKey,
                _ =>
                    new EndpointHealth())
            .MarkFailure();

    private void MarkEndpointTransportFailure(
        string endpointKey) =>
        _endpointHealth
            .GetOrAdd(
                endpointKey,
                _ =>
                    new EndpointHealth())
            .MarkTransportFailure();

    private void MarkEndpointRateLimited(
        string endpointKey,
        TimeSpan retryAfter) =>
        _endpointHealth
            .GetOrAdd(
                endpointKey,
                _ =>
                    new EndpointHealth())
            .MarkRateLimited(
                retryAfter);

    private static TimeSpan GetRetryAfter(
        HttpResponseMessage response)
    {
        var retry =
            response.Headers.RetryAfter;

        if (retry?.Delta is
            {
            } delta)
        {
            return delta <=
                TimeSpan.Zero
                ? TimeSpan.FromSeconds(1)
                : delta;
        }

        if (retry?.Date is
            {
            } retryDate)
        {
            var remaining =
                retryDate -
                DateTimeOffset.UtcNow;

            return remaining <=
                TimeSpan.Zero
                ? TimeSpan.FromSeconds(1)
                : remaining;
        }

        return TimeSpan.FromSeconds(30);
    }

    private static bool IsRecoverableStatus(
        int statusCode) =>
        statusCode is
            400 or
            408 or
            413 or
            429 or
            500 or
            502 or
            503 or
            504 or
            509;

    private static async Task<XDocument?>
        TryReadCacheAsync(
            LayerDefinition layer,
            Bounds bounds,
            string? cacheRoot,
            CancellationToken cancellationToken)
    {
        if (cacheRoot is null)
        {
            return null;
        }

        var path =
            GetCachePath(
                cacheRoot,
                layer,
                bounds);

        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var bytes =
                await File
                    .ReadAllBytesAsync(
                        path,
                        cancellationToken)
                    .ConfigureAwait(false);

            return ParseOsm(
                bytes);
        }
        catch (
            Exception exception)
            when (
                exception is
                    IOException or
                    InvalidDataException or
                    XmlException)
        {
            try
            {
                File.Delete(
                    path);
            }
            catch
            {
            }

            return null;
        }
    }

    private static async Task WriteCacheAsync(
        LayerDefinition layer,
        Bounds bounds,
        string? cacheRoot,
        XDocument document,
        CancellationToken cancellationToken)
    {
        if (cacheRoot is null)
        {
            return;
        }

        var path =
            GetCachePath(
                cacheRoot,
                layer,
                bounds);

        var directory =
            Path.GetDirectoryName(
                path)!;

        Directory.CreateDirectory(
            directory);

        var temporary =
            path +
            ".tmp-" +
            Guid.NewGuid()
                .ToString("N");

        try
        {
            var xml =
                document.ToString(
                    SaveOptions
                        .DisableFormatting);

            await File
                .WriteAllTextAsync(
                    temporary,
                    xml,
                    new UTF8Encoding(
                        encoderShouldEmitUTF8Identifier:
                            false),
                    cancellationToken)
                .ConfigureAwait(false);

            File.Move(
                temporary,
                path,
                overwrite:
                    true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                try
                {
                    File.Delete(
                        temporary);
                }
                catch
                {
                }
            }
        }
    }

    private static string GetCachePath(
        string cacheRoot,
        LayerDefinition layer,
        Bounds bounds)
    {
        var query =
            BuildQuery(
                layer,
                bounds);

        var keyMaterial =
            CacheSchemaVersion +
            "\n" +
            layer.Slug +
            "\n" +
            query;

        var hash =
            Convert
                .ToHexString(
                    SHA256.HashData(
                        Encoding.UTF8
                            .GetBytes(
                                keyMaterial)))
                .ToLowerInvariant();

        return Path.Combine(
            cacheRoot,
            layer.Slug,
            hash +
            ".osm.xml");
    }

    private static string BuildQuery(
        LayerDefinition layer,
        Bounds bounds)
    {
        var invariant =
            CultureInfo.InvariantCulture;

        var bbox =
            string.Create(
                invariant,
                $"{bounds.South:G17},{bounds.West:G17},{bounds.North:G17},{bounds.East:G17}");

        var selectors =
            layer.Layer switch
            {
                MapStudioOsmSceneLayer.Roads =>
                    $"way[\"highway\"~\"^(motorway|motorway_link|trunk|trunk_link|primary|primary_link|secondary|secondary_link|tertiary|tertiary_link|unclassified|residential|living_street|service|road|busway|bus_guideway)$\"][\"area\"!=\"yes\"]({bbox});",

                MapStudioOsmSceneLayer.Buildings =>
                    $"way[\"building\"]({bbox});" +
                    $"relation[\"building\"]({bbox});",

                MapStudioOsmSceneLayer.Infrastructure =>
                    $"node[\"power\"~\"^(pole|tower)$\"]({bbox});" +
                    $"way[\"barrier\"~\"^(wall|fence|guard_rail)$\"]({bbox});" +
                    $"way[\"highway\"=\"footway\"][\"footway\"=\"sidewalk\"]({bbox});" +
                    $"way[\"highway\"=\"service\"][\"service\"=\"driveway\"]({bbox});" +
                    $"way[\"amenity\"=\"parking\"]({bbox});" +
                    $"relation[\"amenity\"=\"parking\"]({bbox});",

                MapStudioOsmSceneLayer.Vegetation =>
                    $"node[\"natural\"~\"^(tree|shrub)$\"]({bbox});" +
                    $"way[\"natural\"=\"tree_row\"]({bbox});" +
                    $"way[\"barrier\"=\"hedge\"]({bbox});" +
                    $"way[\"landuse\"=\"forest\"]({bbox});" +
                    $"way[\"natural\"~\"^(wood|scrub)$\"]({bbox});" +
                    $"relation[\"landuse\"=\"forest\"]({bbox});" +
                    $"relation[\"natural\"~\"^(wood|scrub)$\"]({bbox});",

                MapStudioOsmSceneLayer.Furniture =>
                    $"node[\"highway\"=\"street_lamp\"]({bbox});" +
                    $"node[\"highway\"=\"traffic_signals\"]({bbox});" +
                    $"node[\"highway\"=\"crossing\"]({bbox});" +
                    $"node[\"crossing\"]({bbox});" +
                    $"node[\"traffic_sign\"]({bbox});" +
                    $"node[\"highway\"=\"traffic_sign\"]({bbox});" +
                    $"node[\"highway\"=\"stop\"]({bbox});" +
                    $"node[\"highway\"=\"give_way\"]({bbox});" +
                    $"node[\"highway\"=\"bus_stop\"]({bbox});" +
                    $"node[\"public_transport\"=\"platform\"]({bbox});" +
                    $"node[\"amenity\"=\"shelter\"]({bbox});" +
                    $"node[\"shelter\"=\"yes\"]({bbox});" +
                    $"node[\"amenity\"=\"bench\"]({bbox});" +
                    $"node[\"amenity\"=\"waste_basket\"]({bbox});" +
                    $"node[\"barrier\"=\"bollard\"]({bbox});" +
                    $"node[\"emergency\"=\"fire_hydrant\"]({bbox});",

                _ =>
                    throw new ArgumentOutOfRangeException(
                        nameof(layer))
            };

        var needsDependencyExpansion =
            layer.Layer is not
                MapStudioOsmSceneLayer.Furniture;

        return
            "[out:xml][timeout:18];(" +
            selectors +
            ");out body" +
            (
                needsDependencyExpansion
                    ? ";>;out skel qt;"
                    : " qt;"
            );
    }

    private static IReadOnlyList<Bounds>
        CreatePrimaryChunks(
            double south,
            double west,
            double north,
            double east,
            double targetChunkSpanMeters)
    {
        var (
            rows,
            columns
        ) =
            GetChunkGrid(
                south,
                west,
                north,
                east,
                targetChunkSpanMeters);

        var chunks =
            new List<Bounds>(
                checked(
                    rows *
                    columns));

        for (var row = 0; row < rows; row++)
        {
            for (
                var column = 0;
                column < columns;
                column++)
            {
                chunks.Add(
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

        return chunks;
    }

    private static Bounds[] Subdivide(
        Bounds bounds)
    {
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

        return
        [
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
        ];
    }

    private static (
        int Rows,
        int Columns
    ) GetChunkGrid(
        double south,
        double west,
        double north,
        double east,
        double targetChunkSpanMeters)
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
                Math.Cos(
                    latitudeRadians));

        var rows =
            Math.Clamp(
                (int)Math.Ceiling(
                    heightMeters /
                    targetChunkSpanMeters),
                1,
                MaximumChunksPerAxis);

        var columns =
            Math.Clamp(
                (int)Math.Ceiling(
                    widthMeters /
                    targetChunkSpanMeters),
                1,
                MaximumChunksPerAxis);

        return (
            rows,
            columns
        );
    }

    private static XDocument ParseOsm(
        byte[] bytes)
    {
        using var stream =
            new MemoryStream(
                bytes,
                writable:
                    false);

        using var reader =
            XmlReader.Create(
                stream,
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

    private static string GetEndpointKey(
        Uri endpoint) =>
        endpoint.GetLeftPart(
            UriPartial.Authority);

    private static string FormatBytes(
        long bytes)
    {
        if (bytes <
            1024)
        {
            return $"{bytes} B";
        }

        if (bytes <
            1024L *
            1024L)
        {
            return
                $"{bytes / 1024.0:0.0} KB";
        }

        return
            $"{bytes / (1024.0 * 1024.0):0.0} MB";
    }

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
            south <
                -90 ||
            north >
                90 ||
            west <
                -180 ||
            east >
                180 ||
            south >=
                north ||
            west >=
                east)
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
                "OMSI-Map-Studio/layered-osm");

        client.DefaultRequestHeaders
            .Accept
            .ParseAdd(
                "application/xml,text/xml;q=0.9,*/*;q=0.1");

        return client;
    }

    private void WriteDiagnostic(
        string message) =>
        _diagnostic?.Invoke(
            message);

    private sealed record LayerDefinition(
        MapStudioOsmSceneLayer Layer,
        string Slug,
        string DisplayName,
        double TargetChunkSpanMeters);

    private sealed record Bounds(
        double South,
        double West,
        double North,
        double East);

    private sealed record AttemptUpdate(
        Uri Endpoint,
        string ChunkLabel,
        int Attempt,
        int TotalAttempts);

    private sealed record NetworkAttemptResult(
        XDocument? Document,
        int? StatusCode,
        string? Error,
        bool CanRecoverBySubdivision,
        long ByteCount,
        TimeSpan Elapsed,
        TimeSpan? RetryAfter,
        bool TransportFailure);

    private sealed record ChunkFetchResult(
        XDocument? Document,
        string? Endpoint,
        int RequestAttemptCount,
        string? Error,
        bool CanRecoverBySubdivision,
        IReadOnlyList<string> Unused);

    private sealed record RecoveryResult(
        IReadOnlyList<XDocument> Documents,
        int SuccessfulChunkCount,
        int FailedChunkCount,
        int RequestAttemptCount,
        IReadOnlyList<string> UsedEndpoints,
        IReadOnlyList<string> Failures,
        int CacheHitCount,
        int NetworkSuccessCount);

    private sealed class EndpointHealth
    {
        private readonly object _gate =
            new();

        private int _score =
            100;

        private DateTimeOffset
            _cooldownUntil =
                DateTimeOffset.MinValue;

        public (
            int Score,
            DateTimeOffset CooldownUntil
        ) Snapshot()
        {
            lock (_gate)
            {
                return (
                    _score,
                    _cooldownUntil
                );
            }
        }

        public void MarkSuccess()
        {
            lock (_gate)
            {
                _score =
                    Math.Min(
                        100,
                        _score +
                        8);

                _cooldownUntil =
                    DateTimeOffset.MinValue;
            }
        }

        public void MarkFailure()
        {
            lock (_gate)
            {
                _score =
                    Math.Max(
                        0,
                        _score -
                        12);
            }
        }

        public void MarkTransportFailure()
        {
            lock (_gate)
            {
                _score =
                    Math.Max(
                        0,
                        _score -
                        20);

                // A timeout on a parent bbox must not quarantine the server
                // from the smaller recovery children. Smaller Overpass
                // queries often succeed immediately after the parent timed
                // out. Only explicit 429 responses impose a hard cooldown.
                _cooldownUntil =
                    DateTimeOffset.MinValue;
            }
        }

        public void MarkRateLimited(
            TimeSpan retryAfter)
        {
            lock (_gate)
            {
                _score =
                    Math.Max(
                        0,
                        _score -
                        35);

                _cooldownUntil =
                    DateTimeOffset.UtcNow +
                    (
                        retryAfter <=
                            TimeSpan.Zero
                            ? TimeSpan.FromSeconds(1)
                            : retryAfter
                    );
            }
        }
    }

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
                document.Root;

            if (root is null)
            {
                return;
            }

            foreach (
                var element
                in root.Elements())
            {
                var id =
                    element.Attribute(
                        "id")
                        ?.Value;

                if (string.IsNullOrWhiteSpace(
                        id))
                {
                    continue;
                }

                var clone =
                    new XElement(
                        element);

                switch (
                    element.Name
                        .LocalName)
                {
                    case "node":
                        _nodes[id] =
                            clone;
                        break;

                    case "way":
                        _ways[id] =
                            clone;
                        break;

                    case "relation":
                        _relations[id] =
                            clone;
                        break;
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

            foreach (
                var node
                in _nodes
                    .OrderBy(
                        pair =>
                            SortKey(
                                pair.Key)))
            {
                root.Add(
                    new XElement(
                        node.Value));
            }

            foreach (
                var way
                in _ways
                    .OrderBy(
                        pair =>
                            SortKey(
                                pair.Key)))
            {
                root.Add(
                    new XElement(
                        way.Value));
            }

            foreach (
                var relation
                in _relations
                    .OrderBy(
                        pair =>
                            SortKey(
                                pair.Key)))
            {
                root.Add(
                    new XElement(
                        relation.Value));
            }

            return new XDocument(
                    new XDeclaration(
                        "1.0",
                        "utf-8",
                        "yes"),
                    root)
                .ToString(
                    SaveOptions
                        .DisableFormatting);
        }

        private static (
            long Numeric,
            string Text
        ) SortKey(
            string id) =>
            long.TryParse(
                id,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var numeric)
                ? (
                    numeric,
                    string.Empty
                )
                : (
                    long.MaxValue,
                    id
                );
    }
}
