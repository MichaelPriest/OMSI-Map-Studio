using System.ComponentModel;
using MapStudio.Core.Generation.Roads;
using MapStudio.Native.Services;
using MapStudio.Native.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MapStudio.Native.Controls;

public sealed partial class RealWorldGenerationPanel :
    UserControl,
    IDisposable
{
    public RealWorldGenerationViewModel
        ViewModel { get; }

    public RealWorldGenerationPanel()
        : this(
            new RealWorldGenerationViewModel())
    {
    }

    public RealWorldGenerationPanel(
        RealWorldGenerationViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(
            viewModel);

        ViewModel =
            viewModel;

        InitializeComponent();

        ViewModel.PropertyChanged +=
            OnViewModelPropertyChanged;

        Unloaded +=
            OnUnloaded;

        Refresh();
    }

    public Task<NativeRealWorldGenerationSummary>
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
            bool useOpenAerialMapEvidence = false) =>
        ViewModel.StartAsync(
            omsiRoot,
            mapDirectory,
            south,
            west,
            north,
            east,
            anchor,
            cancellationToken,
            useSavedGoogleElevation,
            useSavedOpenMeteoElevation,
            useBuildingVisualEvidence,
            useOpenAerialMapEvidence);

    public bool Cancel() =>
        ViewModel.Cancel();

    public void Reset() =>
        ViewModel.Reset();

    public void Dispose()
    {
        ViewModel.PropertyChanged -=
            OnViewModelPropertyChanged;

        Unloaded -=
            OnUnloaded;
    }

    private void OnCancelClick(
        object sender,
        RoutedEventArgs e)
    {
        Cancel();
    }

    private void OnUnloaded(
        object sender,
        RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -=
            OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (
            DispatcherQueue.HasThreadAccess)
        {
            Refresh();
            return;
        }

        DispatcherQueue.TryEnqueue(
            Refresh);
    }

    private void Refresh()
    {
        StatusText.Text =
            ViewModel.StatusText;

        StageText.Text =
            ViewModel.Stage?.ToString() ??
            ViewModel.State.ToString();

        CancelButton.IsEnabled =
            ViewModel.CanCancel;

        OverallProgressPanel.Visibility =
            ViewModel.State ==
                NativeRealWorldGenerationState.Idle
                ? Visibility.Collapsed
                : Visibility.Visible;

        OverallProgressText.Text =
            ViewModel.OverallProgressText;

        OverallProgressBar.Value =
            ViewModel.OverallProgress *
            100.0;

        AssetIndexPanel.Visibility =
            ViewModel.IndexedFiles > 0 ||
            ViewModel.IndexedCandidates > 0
                ? Visibility.Visible
                : Visibility.Collapsed;

        AssetIndexText.Text =
            $"{ViewModel.IndexedFiles} arquivo(s) · " +
            $"{ViewModel.IndexedCandidates} candidato(s)";

        ElevationPanel.Visibility =
            ViewModel.ElevationSamplesTotal > 0
                ? Visibility.Visible
                : Visibility.Collapsed;

        ElevationText.Text =
            ViewModel.ElevationProgressText;

        ElevationProgressBar.Value =
            ViewModel.ElevationProgress *
            100.0;

        SummaryPanel.Visibility =
            ViewModel.Summary is null
                ? Visibility.Collapsed
                : Visibility.Visible;

        SummaryText.Text =
            ViewModel.SummaryText;

        BackupText.Text =
            ViewModel.Summary is null
                ? string.Empty
                : "Backup da sessão: " +
                  ViewModel.Summary
                      .SessionBackupDirectory;
    }
}
