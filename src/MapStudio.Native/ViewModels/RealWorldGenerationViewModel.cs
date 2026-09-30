using System.ComponentModel;
using System.Runtime.CompilerServices;
using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Omsi.Structures;
using MapStudio.Native.Services;

namespace MapStudio.Native.ViewModels;

public sealed class RealWorldGenerationViewModel :
    INotifyPropertyChanged
{
    private readonly NativeRealWorldMapGenerationController
        _controller;

    private NativeRealWorldGenerationState
        _state =
            NativeRealWorldGenerationState.Idle;

    private MapStudioRealWorldMapPipelineStage?
        _stage;

    private string
        _statusText =
            "Pronto para gerar mapa real.";

    private int
        _indexedFiles;

    private int
        _indexedCandidates;

    private int
        _elevationSamplesCompleted;

    private int
        _elevationSamplesTotal;

    private NativeRealWorldGenerationSummary?
        _summary;

    public RealWorldGenerationViewModel()
        : this(
            new NativeRealWorldMapGenerationController())
    {
    }

    public RealWorldGenerationViewModel(
        NativeRealWorldMapGenerationController controller)
    {
        ArgumentNullException.ThrowIfNull(
            controller);

        _controller =
            controller;
    }

    public event PropertyChangedEventHandler?
        PropertyChanged;

    public NativeRealWorldGenerationState State
    {
        get => _state;
        private set
        {
            if (_state == value)
            {
                return;
            }

            _state =
                value;

            OnPropertyChanged();
            OnPropertyChanged(
                nameof(IsBusy));
            OnPropertyChanged(
                nameof(CanStart));
            OnPropertyChanged(
                nameof(CanCancel));
        }
    }

    public MapStudioRealWorldMapPipelineStage?
        Stage
    {
        get => _stage;
        private set
        {
            if (
                SetField(
                    ref _stage,
                    value))
            {
                OnPropertyChanged(
                    nameof(OverallProgress));

                OnPropertyChanged(
                    nameof(OverallProgressText));
            }
        }
    }

    public double OverallProgress =>
        Stage switch
        {
            MapStudioRealWorldMapPipelineStage.IndexingAssets =>
                0.10,
            MapStudioRealWorldMapPipelineStage.DownloadingElevation =>
                0.25,
            MapStudioRealWorldMapPipelineStage.DownloadingOpenStreetMap =>
                0.40,
            MapStudioRealWorldMapPipelineStage.ApplyingElevation =>
                0.55,
            MapStudioRealWorldMapPipelineStage.GeneratingRoads =>
                0.70,
            MapStudioRealWorldMapPipelineStage.ReconstructingScene =>
                0.88,
            MapStudioRealWorldMapPipelineStage.Completed =>
                1.0,
            _ =>
                0.0
        };

    public string OverallProgressText =>
        Stage switch
        {
            MapStudioRealWorldMapPipelineStage.IndexingAssets =>
                "1/6 · assets OMSI",
            MapStudioRealWorldMapPipelineStage.DownloadingElevation =>
                "2/6 · elevação",
            MapStudioRealWorldMapPipelineStage.DownloadingOpenStreetMap =>
                "3/6 · OpenStreetMap",
            MapStudioRealWorldMapPipelineStage.ApplyingElevation =>
                "4/6 · terreno",
            MapStudioRealWorldMapPipelineStage.GeneratingRoads =>
                "5/6 · vias e estruturas",
            MapStudioRealWorldMapPipelineStage.ReconstructingScene =>
                "6/6 · cenário",
            MapStudioRealWorldMapPipelineStage.Completed =>
                "concluído",
            _ =>
                string.Empty
        };

    public string StatusText
    {
        get => _statusText;
        private set =>
            SetField(
                ref _statusText,
                value);
    }

    public int IndexedFiles
    {
        get => _indexedFiles;
        private set =>
            SetField(
                ref _indexedFiles,
                value);
    }

    public int IndexedCandidates
    {
        get => _indexedCandidates;
        private set =>
            SetField(
                ref _indexedCandidates,
                value);
    }

    public int ElevationSamplesCompleted
    {
        get => _elevationSamplesCompleted;
        private set
        {
            if (
                SetField(
                    ref _elevationSamplesCompleted,
                    value))
            {
                OnPropertyChanged(
                    nameof(ElevationProgress));
                OnPropertyChanged(
                    nameof(ElevationProgressText));
            }
        }
    }

    public int ElevationSamplesTotal
    {
        get => _elevationSamplesTotal;
        private set
        {
            if (
                SetField(
                    ref _elevationSamplesTotal,
                    value))
            {
                OnPropertyChanged(
                    nameof(ElevationProgress));
                OnPropertyChanged(
                    nameof(ElevationProgressText));
            }
        }
    }

    public double ElevationProgress =>
        ElevationSamplesTotal <= 0
            ? 0
            : Math.Clamp(
                (double)ElevationSamplesCompleted /
                ElevationSamplesTotal,
                0,
                1);

    public string ElevationProgressText =>
        ElevationSamplesTotal <= 0
            ? string.Empty
            : $"{ElevationSamplesCompleted}/{ElevationSamplesTotal}";

    public NativeRealWorldGenerationSummary?
        Summary
    {
        get => _summary;
        private set
        {
            if (
                SetField(
                    ref _summary,
                    value))
            {
                OnPropertyChanged(
                    nameof(SummaryText));
            }
        }
    }

    public string SummaryText =>
        Summary is null
            ? string.Empty
            : $"{Summary.TotalPlacedElements} elemento(s) · " +
              $"{Summary.RoadSplines} via(s) · " +
              $"{Summary.Junctions} junction(s) · " +
              $"{Summary.RoadStructures} estrutura(s) viária(s) · " +
              $"{Summary.SceneObjects} objeto(s) · " +
              $"{Summary.ReviewItems} revisão(ões) · " +
              $"{Summary.MissingAssets} asset(s) ausente(s) · " +
              $"elevação {FormatElevationMode(Summary.ElevationMode)}";

    private static string FormatElevationMode(
        NativeRealWorldElevationMode mode) =>
        mode switch
        {
            NativeRealWorldElevationMode.Google =>
                "Google",
            NativeRealWorldElevationMode.OpenMeteo =>
                "Open-Meteo/Copernicus",
            _ =>
                "sem DEM"
        };

    public bool IsBusy =>
        State is
            NativeRealWorldGenerationState.Running or
            NativeRealWorldGenerationState.Cancelling;

    public bool CanStart =>
        !IsBusy;

    public bool CanCancel =>
        State ==
        NativeRealWorldGenerationState.Running;

    public async Task<NativeRealWorldGenerationSummary>
        StartAsync(
            string omsiRoot,
            string mapDirectory,
            double south,
            double west,
            double north,
            double east,
            MapStudioGeographicAnchor anchor,
            CancellationToken cancellationToken = default,
            bool useSavedGoogleElevation = true,
            bool useSavedOpenMeteoElevation = false,
            bool useBuildingVisualEvidence = false,
            bool useOpenAerialMapEvidence = false)
    {
        if (IsBusy)
        {
            throw new InvalidOperationException(
                "realWorldGenerationAlreadyRunning");
        }

        Summary =
            null;

        IndexedFiles =
            0;

        IndexedCandidates =
            0;

        ElevationSamplesCompleted =
            0;

        ElevationSamplesTotal =
            0;

        State =
            NativeRealWorldGenerationState.Running;

        StatusText =
            "Preparando geração automática do mapa real...";

        var progress =
            new Progress<NativeRealWorldGenerationProgress>(
                ApplyProgress);

        try
        {
            var summary =
                await _controller
                    .RunBestAvailableAsync(
                        omsiRoot,
                        mapDirectory,
                        south,
                        west,
                        north,
                        east,
                        anchor,
                        progress,
                        cancellationToken,
                        useSavedGoogleElevation,
                        useSavedOpenMeteoElevation,
                        useBuildingVisualEvidence,
                        useOpenAerialMapEvidence);

            Summary =
                summary;

            State =
                NativeRealWorldGenerationState.Completed;

            StatusText =
                SummaryText;

            return summary;
        }
        catch (OperationCanceledException)
        {
            State =
                NativeRealWorldGenerationState.Cancelled;

            StatusText =
                "Geração do mapa real cancelada.";

            throw;
        }
        catch (Exception exception)
        {
            State =
                NativeRealWorldGenerationState.Failed;

            StatusText =
                "Falha ao gerar mapa real: " +
                exception.Message +
                ". O rollback foi solicitado pelo pipeline.";

            throw;
        }
    }

    public bool Cancel()
    {
        var cancelled =
            _controller.Cancel();

        if (cancelled)
        {
            State =
                NativeRealWorldGenerationState.Cancelling;

            StatusText =
                "Cancelando geração e restaurando a sessão...";
        }

        return cancelled;
    }

    public void Reset()
    {
        _controller.Reset();

        State =
            NativeRealWorldGenerationState.Idle;

        Stage =
            null;

        StatusText =
            "Pronto para gerar mapa real.";

        IndexedFiles =
            0;

        IndexedCandidates =
            0;

        ElevationSamplesCompleted =
            0;

        ElevationSamplesTotal =
            0;

        Summary =
            null;
    }

    private void ApplyProgress(
        NativeRealWorldGenerationProgress progress)
    {
        State =
            progress.State;

        Stage =
            progress.Stage;

        StatusText =
            progress.Message;

        if (
            progress.IndexedFiles > 0 ||
            progress.IndexedCandidates > 0)
        {
            IndexedFiles =
                progress.IndexedFiles;

            IndexedCandidates =
                progress.IndexedCandidates;
        }

        if (
            progress.ElevationSamplesTotal > 0)
        {
            ElevationSamplesCompleted =
                progress.ElevationSamplesCompleted;

            ElevationSamplesTotal =
                progress.ElevationSamplesTotal;
        }
    }

    private bool SetField<T>(
        ref T field,
        T value,
        [CallerMemberName]
        string? propertyName = null)
    {
        if (
            EqualityComparer<T>
                .Default
                .Equals(
                    field,
                    value))
        {
            return false;
        }

        field =
            value;

        OnPropertyChanged(
            propertyName);

        return true;
    }

    private void OnPropertyChanged(
        [CallerMemberName]
        string? propertyName = null) =>
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(
                propertyName));
}
