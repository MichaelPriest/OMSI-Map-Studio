using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Scene;
using MapStudio.Core.Omsi.Indexing;

namespace MapStudio.Core.Omsi.Structures;

public enum MapStudioRealWorldScenePipelineStage
{
    DownloadingOpenStreetMap,
    ReconstructingScene,
    Completed
}

public sealed record MapStudioRealWorldScenePipelineProgress(
    MapStudioRealWorldScenePipelineStage Stage,
    string Message);

public sealed record MapStudioRealWorldScenePipelineResult(
    MapStudioOverpassSceneDownloadResult Download,
    MapStudioRealWorldSceneReconstructionResult Reconstruction);

public sealed class MapStudioRealWorldScenePipeline
{
    private readonly MapStudioOverpassSceneClient
        _sceneClient;

    private readonly MapStudioLayeredOsmSceneClient?
        _layeredSceneClient;

    public MapStudioRealWorldScenePipeline()
        : this(
            new MapStudioOverpassSceneClient())
    {
        _layeredSceneClient =
            new MapStudioLayeredOsmSceneClient();
    }

    public MapStudioRealWorldScenePipeline(
        MapStudioOverpassSceneClient sceneClient)
    {
        ArgumentNullException.ThrowIfNull(
            sceneClient);

        _sceneClient =
            sceneClient;
    }

    public async Task<MapStudioRealWorldScenePipelineResult>
        RunAsync(
            string omsiRoot,
            string mapDirectory,
            double south,
            double west,
            double north,
            double east,
            MapStudioGeographicAnchor anchor,
            IReadOnlyList<OmsiAssetIndexEntry> assets,
            IReadOnlyDictionary<
                string,
                IReadOnlyList<MapStudioSceneEvidence>>?
                streetFurnitureEvidence = null,
            IProgress<MapStudioRealWorldScenePipelineProgress>?
                progress = null,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            omsiRoot);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            mapDirectory);

        ArgumentNullException.ThrowIfNull(
            assets);

        progress?.Report(
            new MapStudioRealWorldScenePipelineProgress(
                MapStudioRealWorldScenePipelineStage
                    .DownloadingOpenStreetMap,
                "Baixando vias e cenário real do OpenStreetMap..."));

        var osmProgress =
            progress is null
                ? null
                : new Progress<
                    MapStudioOverpassSceneDownloadProgress>(
                        update =>
                            progress.Report(
                                new MapStudioRealWorldScenePipelineProgress(
                                    MapStudioRealWorldScenePipelineStage
                                        .DownloadingOpenStreetMap,
                                    update.Message)));

        var download =
            _layeredSceneClient is null
                ? await _sceneClient
                    .DownloadAsync(
                        south,
                        west,
                        north,
                        east,
                        cancellationToken,
                        osmProgress)
                    .ConfigureAwait(false)
                : await _layeredSceneClient
                    .DownloadAsync(
                        south,
                        west,
                        north,
                        east,
                        Path.Combine(
                            Path.GetFullPath(
                                omsiRoot),
                            ".mapstudio",
                            "cache",
                            "osm"),
                        cancellationToken,
                        osmProgress)
                    .ConfigureAwait(false);

        cancellationToken
            .ThrowIfCancellationRequested();

        progress?.Report(
            new MapStudioRealWorldScenePipelineProgress(
                MapStudioRealWorldScenePipelineStage
                    .ReconstructingScene,
                "Reconstruindo prédios, vegetação, infraestrutura e mobiliário..."));

        var reconstruction =
            await new MapStudioRealWorldSceneReconstructionRunner()
                .RunAsync(
                    omsiRoot,
                    mapDirectory,
                    download.OsmXml,
                    anchor,
                    assets,
                    streetFurnitureEvidence,
                    cancellationToken)
                .ConfigureAwait(false);

        progress?.Report(
            new MapStudioRealWorldScenePipelineProgress(
                MapStudioRealWorldScenePipelineStage
                    .Completed,
                $"Reconstrução concluída: {reconstruction.PlacedObjectCount} objeto(s), {reconstruction.ReviewCount} item(ns) em revisão."));

        return new MapStudioRealWorldScenePipelineResult(
            download,
            reconstruction);
    }
}
