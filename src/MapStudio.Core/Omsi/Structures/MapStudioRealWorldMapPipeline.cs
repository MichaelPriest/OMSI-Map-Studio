using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Scene;
using MapStudio.Core.Generation.Terrain;
using MapStudio.Core.Omsi.Indexing;
using MapStudio.Core.Omsi.Maps;

namespace MapStudio.Core.Omsi.Structures;

public enum MapStudioRealWorldMapPipelineStage
{
    IndexingAssets,
    DownloadingOpenStreetMap,
    ApplyingElevation,
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
    string SessionBackupDirectory,
    MapStudioTerrainElevationBatchResult? Elevation = null)
{
    public int PlacedElementCount =>
        Roads.PlacedSplineCount +
        Roads.GeneratedJunctionCount +
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
        RunAutoIndexedAsync(
            string omsiRoot,
            string mapDirectory,
            double south,
            double west,
            double north,
            double east,
            MapStudioGeographicAnchor anchor,
            IReadOnlyDictionary<
                string,
                IReadOnlyList<MapStudioSceneEvidence>>?
                streetFurnitureEvidence = null,
            IProgress<MapStudioRealWorldMapPipelineProgress>?
                progress = null,
            IProgress<OmsiAssetIndexProgress>?
                assetIndexProgress = null,
            CancellationToken cancellationToken = default,
            MapStudioGeoreferencedElevationSurface? elevation = null)
    {
        progress?.Report(
            new MapStudioRealWorldMapPipelineProgress(
                MapStudioRealWorldMapPipelineStage
                    .IndexingAssets,
                "Atualizando biblioteca de assets OMSI..."));

        var catalog =
            await new MapStudioRealWorldAssetCatalogLoader()
                .LoadAsync(
                    omsiRoot,
                    assetIndexProgress,
                    cancellationToken)
                .ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        return await RunAsync(
                omsiRoot,
                mapDirectory,
                south,
                west,
                north,
                east,
                anchor,
                catalog.SceneryObjects,
                streetFurnitureEvidence,
                progress,
                cancellationToken,
                elevation)
            .ConfigureAwait(false);
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
            CancellationToken cancellationToken = default,
            MapStudioGeoreferencedElevationSurface? elevation = null)
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

        var generatedAssetTransaction =
            new MapStudioGeneratedAssetTransaction();

        var generatedAssets =
            await generatedAssetTransaction
                .CaptureAsync(
                    root,
                    cancellationToken)
                .ConfigureAwait(false);

        try
        {
            MapStudioTerrainElevationBatchResult?
                elevationResult =
                    null;

            if (elevation is not null)
            {
                progress?.Report(
                    new MapStudioRealWorldMapPipelineProgress(
                        MapStudioRealWorldMapPipelineStage
                            .ApplyingElevation,
                        "Aplicando elevação real ao terreno e ao posicionamento vertical..."));

                elevationResult =
                    await new MapStudioTerrainElevationBatchApplier()
                        .ApplyAsync(
                            root,
                            mapRoot,
                            anchor,
                            elevation,
                            cancellationToken)
                        .ConfigureAwait(false);
            }

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
                        cancellationToken,
                        elevation)
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
                        cancellationToken,
                        elevation)
                    .ConfigureAwait(false);

            progress?.Report(
                new MapStudioRealWorldMapPipelineProgress(
                    MapStudioRealWorldMapPipelineStage
                        .Completed,
                    $"Mapa real concluído: {roads.PlacedSplineCount} spline(s) de via, {roads.GeneratedJunctionCount} junction(s) e {scene.PlacedObjectCount} objeto(s) de cenário."));

            return new MapStudioRealWorldMapPipelineResult(
                download,
                roads,
                scene,
                snapshot.BackupRoot,
                elevationResult);
        }
        catch (Exception original)
        {
            var rollbackErrors =
                new List<Exception>();

            try
            {
                RestoreSnapshot(
                    snapshot);
            }
            catch (Exception rollback)
            {
                rollbackErrors.Add(rollback);
            }

            try
            {
                generatedAssetTransaction
                    .Restore(
                        generatedAssets);
            }
            catch (Exception rollback)
            {
                rollbackErrors.Add(rollback);
            }

            if (rollbackErrors.Count > 0)
            {
                throw new AggregateException(
                    "realWorldMapRollbackFailed",
                    [original, ..rollbackErrors]);
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

            var terrainPath =
                tilePath +
                ".terrain";

            if (File.Exists(terrainPath))
            {
                var terrainRelative =
                    Path.GetRelativePath(
                        mapDirectory,
                        terrainPath);

                var terrainBackupPath =
                    Path.Combine(
                        backupRoot,
                        terrainRelative);

                Directory.CreateDirectory(
                    Path.GetDirectoryName(
                        terrainBackupPath)!);

                File.Copy(
                    terrainPath,
                    terrainBackupPath,
                    overwrite: true);

                files.Add(
                    new SnapshotFile(
                        terrainPath,
                        terrainBackupPath));
            }
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
