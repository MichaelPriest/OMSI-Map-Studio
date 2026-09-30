using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Terrain;
using MapStudio.Core.Omsi.Indexing;
using MapStudio.Core.Omsi.Structures;

namespace MapStudio.Native.Services;

public enum NativeRealWorldGenerationState
{
    Idle,
    Running,
    Cancelling,
    Completed,
    Failed,
    Cancelled
}

public sealed record NativeRealWorldGenerationProgress(
    NativeRealWorldGenerationState State,
    MapStudioRealWorldMapPipelineStage? Stage,
    string Message,
    int IndexedFiles = 0,
    int IndexedCandidates = 0,
    int ElevationSamplesCompleted = 0,
    int ElevationSamplesTotal = 0);

public sealed record NativeRealWorldGenerationSummary(
    NativeRealWorldElevationMode ElevationMode,
    int RoadSplines,
    int Junctions,
    int SceneObjects,
    int ReviewItems,
    int TotalPlacedElements,
    string SessionBackupDirectory,
    int RoadStructures = 0,
    int MissingAssets = 0);

public sealed class NativeRealWorldMapGenerationController :
    IDisposable
{
    private readonly NativeRealWorldMapGenerationService
        _service;

    private readonly object
        _gate =
            new();

    private CancellationTokenSource?
        _activeCancellation;

    private NativeRealWorldGenerationState
        _state =
            NativeRealWorldGenerationState.Idle;

    public NativeRealWorldMapGenerationController()
        : this(
            new NativeRealWorldMapGenerationService())
    {
    }

    public NativeRealWorldMapGenerationController(
        NativeRealWorldMapGenerationService service)
    {
        ArgumentNullException.ThrowIfNull(service);

        _service =
            service;
    }

    public NativeRealWorldGenerationState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    public bool IsRunning =>
        State is
            NativeRealWorldGenerationState.Running or
            NativeRealWorldGenerationState.Cancelling;

    public async Task<NativeRealWorldGenerationSummary>
        RunBestAvailableAsync(
            string omsiRoot,
            string mapDirectory,
            double south,
            double west,
            double north,
            double east,
            MapStudioGeographicAnchor anchor,
            IProgress<NativeRealWorldGenerationProgress>?
                progress = null,
            CancellationToken cancellationToken = default,
            bool useSavedGoogleElevation = true,
            bool useSavedOpenMeteoElevation = false)
    {
        CancellationTokenSource linked;

        lock (_gate)
        {
            if (
                _state is
                    NativeRealWorldGenerationState.Running or
                    NativeRealWorldGenerationState.Cancelling)
            {
                throw new InvalidOperationException(
                    "realWorldGenerationAlreadyRunning");
            }

            _activeCancellation =
                CancellationTokenSource
                    .CreateLinkedTokenSource(
                        cancellationToken);

            linked =
                _activeCancellation;

            _state =
                NativeRealWorldGenerationState.Running;
        }

        Report(
            progress,
            NativeRealWorldGenerationState.Running,
            stage: null,
            "Preparando geração automática do mapa real...");

        var pipelineProgress =
            new Progress<MapStudioRealWorldMapPipelineProgress>(
                update =>
                    Report(
                        progress,
                        State,
                        update.Stage,
                        update.Message));

        var assetIndexProgress =
            new Progress<OmsiAssetIndexProgress>(
                update =>
                    Report(
                        progress,
                        State,
                        MapStudioRealWorldMapPipelineStage
                            .IndexingAssets,
                        update.RelativePath is { Length: > 0 }
                            ? $"Indexando assets OMSI: {update.RelativePath}"
                            : "Indexando biblioteca de assets OMSI...",
                        update.ExaminedFiles,
                        update.CandidateFiles));

        var elevationProgress =
            new Progress<MapStudioElevationDownloadProgress>(
                update =>
                    Report(
                        progress,
                        State,
                        MapStudioRealWorldMapPipelineStage
                            .DownloadingElevation,
                        $"Baixando elevação real: {update.CompletedSamples}/{update.TotalSamples}",
                        elevationSamplesCompleted:
                            update.CompletedSamples,
                        elevationSamplesTotal:
                            update.TotalSamples));

        try
        {
            var result =
                await _service
                    .RunBestAvailableAsync(
                        omsiRoot,
                        mapDirectory,
                        south,
                        west,
                        north,
                        east,
                        anchor,
                        progress:
                            pipelineProgress,
                        assetIndexProgress:
                            assetIndexProgress,
                        elevationProgress:
                            elevationProgress,
                        cancellationToken:
                            linked.Token,
                        useSavedGoogleElevation:
                            useSavedGoogleElevation,
                        useSavedOpenMeteoElevation:
                            useSavedOpenMeteoElevation)
                    .ConfigureAwait(false);

            var summary =
                new NativeRealWorldGenerationSummary(
                    result.ElevationMode,
                    result.Pipeline
                        .Roads
                        .PlacedSplineCount,
                    result.Pipeline
                        .Roads
                        .GeneratedJunctionCount,
                    result.Pipeline
                        .Scene
                        .PlacedObjectCount,
                    result.Pipeline
                        .Scene
                        .ReviewCount,
                    result.Pipeline
                        .PlacedElementCount,
                    result.Pipeline
                        .SessionBackupDirectory,
                    result.Pipeline
                        .Roads
                        .GeneratedStructureCount,
                    result.Pipeline
                        .Scene
                        .Vegetation
                        .MissingAssetVegetationIds
                        .Count +
                    result.Pipeline
                        .Scene
                        .StreetFurniture
                        .MissingAssetFeatureIds
                        .Count);

            SetState(
                NativeRealWorldGenerationState.Completed);

            Report(
                progress,
                NativeRealWorldGenerationState.Completed,
                MapStudioRealWorldMapPipelineStage.Completed,
                $"Mapa real concluído: {summary.RoadSplines} via(s), {summary.Junctions} junction(s), {summary.RoadStructures} estrutura(s) viária(s), {summary.SceneObjects} objeto(s) de cenário.");

            return summary;
        }
        catch (OperationCanceledException)
            when (linked.IsCancellationRequested)
        {
            SetState(
                NativeRealWorldGenerationState.Cancelled);

            Report(
                progress,
                NativeRealWorldGenerationState.Cancelled,
                stage: null,
                "Geração do mapa real cancelada.");

            throw;
        }
        catch
        {
            SetState(
                NativeRealWorldGenerationState.Failed);

            Report(
                progress,
                NativeRealWorldGenerationState.Failed,
                stage: null,
                "Falha ao gerar o mapa real. O pipeline executou o rollback da sessão.");

            throw;
        }
        finally
        {
            lock (_gate)
            {
                if (
                    ReferenceEquals(
                        _activeCancellation,
                        linked))
                {
                    _activeCancellation =
                        null;
                }
            }

            linked.Dispose();
        }
    }

    public bool Cancel()
    {
        CancellationTokenSource? cancellation;

        lock (_gate)
        {
            cancellation =
                _activeCancellation;

            if (
                cancellation is null ||
                cancellation.IsCancellationRequested)
            {
                return false;
            }

            _state =
                NativeRealWorldGenerationState.Cancelling;
        }

        cancellation.Cancel();

        return true;
    }

    public void Reset()
    {
        lock (_gate)
        {
            if (
                _state is
                    NativeRealWorldGenerationState.Running or
                    NativeRealWorldGenerationState.Cancelling)
            {
                throw new InvalidOperationException(
                    "realWorldGenerationStillRunning");
            }

            _state =
                NativeRealWorldGenerationState.Idle;
        }
    }

    public void Dispose()
    {
        CancellationTokenSource? cancellation;

        lock (_gate)
        {
            cancellation =
                _activeCancellation;

            _activeCancellation =
                null;
        }

        cancellation?.Cancel();
        cancellation?.Dispose();
    }

    private void SetState(
        NativeRealWorldGenerationState state)
    {
        lock (_gate)
        {
            _state =
                state;
        }
    }

    private static void Report(
        IProgress<NativeRealWorldGenerationProgress>? progress,
        NativeRealWorldGenerationState state,
        MapStudioRealWorldMapPipelineStage? stage,
        string message,
        int indexedFiles = 0,
        int indexedCandidates = 0,
        int elevationSamplesCompleted = 0,
        int elevationSamplesTotal = 0)
    {
        progress?.Report(
            new NativeRealWorldGenerationProgress(
                state,
                stage,
                message,
                indexedFiles,
                indexedCandidates,
                elevationSamplesCompleted,
                elevationSamplesTotal));
    }
}
