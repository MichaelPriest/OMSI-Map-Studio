using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Scene;
using MapStudio.Core.Omsi.Indexing;
using MapStudio.Core.Omsi.Maps;

namespace MapStudio.Core.Omsi.Structures;

public enum MapStudioRealWorldMapPipelineStage
{
    DownloadingOpenStreetMap,
    GeneratingRoads,
    ReconstructingScene,
    Completed
}

public sealed record MapStudioRealWorldMapPipelineProgress(
    MapStudioRealWorldMapPipelineStage Stage,
    string Message);

public sealed record MapStudioRealWorldMapPipelineResult(
    MapStudioOverpassSceneDownloadResult Download,
    MapStudioRoadBatchReconstructionResult Roads,
    MapStudioRealWorldSceneReconstructionResult Scene,
    string SessionBackupDirectory)
{
    public int PlacedElementCount =>
        Roads.PlacedSplineCount +
        Scene.PlacedObjectCount;
}

public sealed class MapStudioRealWorldMapPipeline
{
    private readonly MapStudioOverpassSceneClient
        _sceneClient;

    public MapStudioRealWorldMapPipeline()
        : this(
            new MapStudioOverpassSceneClient())
    {
    }

    public MapStudioRealWorldMapPipeline(
        MapStudioOverpassSceneClient sceneClient)
    {
        ArgumentNullException.ThrowIfNull(
            sceneClient);

        _sceneClient =
            sceneClient;
    }

    public async Task<MapStudioRealWorldMapPipelineResult>
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
            IProgress<MapStudioRealWorldMapPipelineProgress>?
                progress = null,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(omsiRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(mapDirectory);
        ArgumentNullException.ThrowIfNull(assets);

        var root =
            Path.GetFullPath(
                omsiRoot);

        var mapRoot =
            Path.GetFullPath(
                mapDirectory);

        progress?.Report(
            new MapStudioRealWorldMapPipelineProgress(
                MapStudioRealWorldMapPipelineStage
                    .DownloadingOpenStreetMap,
                "Baixando vias e cenário real do OpenStreetMap..."));

        var download =
            await _sceneClient
                .DownloadAsync(
                    south,
                    west,
                    north,
                    east,
                    cancellationToken)
                .ConfigureAwait(false);

        cancellationToken
            .ThrowIfCancellationRequested();

        var snapshot =
            await CreateSnapshotAsync(
                    root,
                    mapRoot,
                    cancellationToken)
                .ConfigureAwait(false);

        try
        {
            progress?.Report(
                new MapStudioRealWorldMapPipelineProgress(
                    MapStudioRealWorldMapPipelineStage
                        .GeneratingRoads,
                    "Gerando vias reais com o Road Kit..."));

            var roads =
                await new MapStudioRoadBatchReconstructionRunner()
                    .RunAsync(
                        root,
                        mapRoot,
                        download.OsmXml,
                        anchor,
                        cancellationToken)
                    .ConfigureAwait(false);

            progress?.Report(
                new MapStudioRealWorldMapPipelineProgress(
                    MapStudioRealWorldMapPipelineStage
                        .ReconstructingScene,
                    "Reconstruindo prédios, vegetação, infraestrutura e mobiliário..."));

            var scene =
                await new MapStudioRealWorldSceneReconstructionRunner()
                    .RunAsync(
                        root,
                        mapRoot,
                        download.OsmXml,
                        anchor,
                        assets,
                        streetFurnitureEvidence,
                        cancellationToken)
                    .ConfigureAwait(false);

            progress?.Report(
                new MapStudioRealWorldMapPipelineProgress(
                    MapStudioRealWorldMapPipelineStage
                        .Completed,
                    $"Mapa real concluído: {roads.PlacedSplineCount} spline(s) de via e {scene.PlacedObjectCount} objeto(s) de cenário."));

            return new MapStudioRealWorldMapPipelineResult(
                download,
                roads,
                scene,
                snapshot.BackupRoot);
        }
        catch (Exception original)
        {
            try
            {
                RestoreSnapshot(
                    snapshot);
            }
            catch (Exception rollback)
            {
                throw new AggregateException(
                    "realWorldMapRollbackFailed",
                    original,
                    rollback);
            }

            throw;
        }
    }

    private static async Task<MapSnapshot>
        CreateSnapshotAsync(
            string omsiRoot,
            string mapDirectory,
            CancellationToken cancellationToken)
    {
        var descriptor =
            await OmsiMapCatalog
                .OpenMapAsync(
                    mapDirectory,
                    cancellationToken)
                .ConfigureAwait(false);

        var backupRoot =
            Path.Combine(
                omsiRoot,
                ".mapstudio",
                "backups",
                "real-world-map-session",
                DateTime.UtcNow
                    .ToString(
                        "yyyyMMdd-HHmmss",
                        System.Globalization
                            .CultureInfo
                            .InvariantCulture) +
                "-" +
                Guid.NewGuid()
                    .ToString("N")[..8]);

        Directory.CreateDirectory(
            backupRoot);

        var files =
            new List<SnapshotFile>(
                descriptor.Tiles.Count);

        foreach (var tile in descriptor.Tiles)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            if (
                !OmsiMapPathResolver
                    .TryResolveTilePath(
                        mapDirectory,
                        tile.RelativeMapPath,
                        out var tilePath) ||
                !File.Exists(tilePath))
            {
                throw new InvalidDataException(
                    "realWorldMapTilePathInvalid");
            }

            var relative =
                Path.GetRelativePath(
                    mapDirectory,
                    tilePath);

            var backupPath =
                Path.Combine(
                    backupRoot,
                    relative);

            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    backupPath)!);

            File.Copy(
                tilePath,
                backupPath,
                overwrite: true);

            files.Add(
                new SnapshotFile(
                    tilePath,
                    backupPath));
        }

        return new MapSnapshot(
            backupRoot,
            files);
    }

    private static void RestoreSnapshot(
        MapSnapshot snapshot)
    {
        foreach (var file in snapshot.Files)
        {
            if (!File.Exists(file.BackupPath))
            {
                throw new InvalidDataException(
                    "realWorldMapSnapshotMissing");
            }

            File.Copy(
                file.BackupPath,
                file.OriginalPath,
                overwrite: true);
        }
    }

    private sealed record MapSnapshot(
        string BackupRoot,
        IReadOnlyList<SnapshotFile> Files);

    private sealed record SnapshotFile(
        string OriginalPath,
        string BackupPath);
}
