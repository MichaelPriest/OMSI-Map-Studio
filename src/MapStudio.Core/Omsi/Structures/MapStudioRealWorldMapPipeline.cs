using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Scene;
using MapStudio.Core.Generation.Terrain;
using MapStudio.Core.Omsi.Indexing;
using MapStudio.Core.Omsi.Maps;

namespace MapStudio.Core.Omsi.Structures;

public enum MapStudioRealWorldMapPipelineStage
{
    IndexingAssets,
    DownloadingElevation,
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
        Roads.InstalledOmsiJunctionCount +
        Roads.GeneratedStructureCount +
        Roads.JunctionAccessoryCount +
        Scene.PlacedObjectCount;
}

public sealed class MapStudioRealWorldMapPipeline
{
    private readonly MapStudioOverpassSceneClient
        _sceneClient;

    private readonly MapStudioLayeredOsmSceneClient?
        _layeredSceneClient;

    private readonly MapStudioGoogleElevationSurfaceClient
        _elevationClient;

    private readonly MapStudioOpenMeteoElevationSurfaceClient
        _openMeteoElevationClient;

    public MapStudioRealWorldMapPipeline()
        : this(
            new MapStudioOverpassSceneClient(),
            new MapStudioGoogleElevationSurfaceClient(),
            new MapStudioOpenMeteoElevationSurfaceClient())
    {
        _layeredSceneClient =
            new MapStudioLayeredOsmSceneClient();
    }

    public MapStudioRealWorldMapPipeline(
        MapStudioOverpassSceneClient sceneClient)
        : this(
            sceneClient,
            new MapStudioGoogleElevationSurfaceClient(),
            new MapStudioOpenMeteoElevationSurfaceClient())
    {
    }

    public MapStudioRealWorldMapPipeline(
        MapStudioOverpassSceneClient sceneClient,
        MapStudioGoogleElevationSurfaceClient elevationClient)
        : this(
            sceneClient,
            elevationClient,
            new MapStudioOpenMeteoElevationSurfaceClient())
    {
    }

    public MapStudioRealWorldMapPipeline(
        MapStudioOverpassSceneClient sceneClient,
        MapStudioGoogleElevationSurfaceClient elevationClient,
        MapStudioOpenMeteoElevationSurfaceClient openMeteoElevationClient)
    {
        ArgumentNullException.ThrowIfNull(
            sceneClient);

        ArgumentNullException.ThrowIfNull(
            elevationClient);

        ArgumentNullException.ThrowIfNull(
            openMeteoElevationClient);

        _sceneClient =
            sceneClient;

        _elevationClient =
            elevationClient;

        _openMeteoElevationClient =
            openMeteoElevationClient;
    }

    public async Task<MapStudioRealWorldMapPipelineResult>
        RunAutoIndexedWithGoogleElevationAsync(
            string omsiRoot,
            string mapDirectory,
            double south,
            double west,
            double north,
            double east,
            MapStudioGeographicAnchor anchor,
            string googleElevationApiKey,
            int elevationRows = 17,
            int elevationColumns = 17,
            IReadOnlyDictionary<
                string,
                IReadOnlyList<MapStudioSceneEvidence>>?
                streetFurnitureEvidence = null,
            IProgress<MapStudioRealWorldMapPipelineProgress>?
                progress = null,
            IProgress<OmsiAssetIndexProgress>?
                assetIndexProgress = null,
            IProgress<MapStudioElevationDownloadProgress>?
                elevationProgress = null,
            CancellationToken cancellationToken = default,
            IMapStudioBuildingVisualEvidenceProvider?
                buildingVisualEvidenceProvider = null)
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

        progress?.Report(
            new MapStudioRealWorldMapPipelineProgress(
                MapStudioRealWorldMapPipelineStage
                    .IndexingAssets,
                $"Biblioteca OMSI pronta · {catalog.ConstructionAssets.RoadSplines.Count} spline(s) de rua · " +
                $"{catalog.ConstructionAssets.JunctionObjects.Count} cruzamento(s) · " +
                $"{catalog.ConstructionAssets.TrafficSignalObjects.Count} semáforo(s) · " +
                $"{catalog.ConstructionAssets.CrosswalkAssets.Count} faixa(s)/travessia(s) reconhecida(s)."));

        cancellationToken
            .ThrowIfCancellationRequested();

        var elevationCoverage =
            await ResolveElevationCoverageAsync(
                    mapDirectory,
                    anchor,
                    south,
                    west,
                    north,
                    east,
                    cancellationToken)
                .ConfigureAwait(false);

        progress?.Report(
            new MapStudioRealWorldMapPipelineProgress(
                MapStudioRealWorldMapPipelineStage
                    .DownloadingElevation,
                "Baixando grade de elevação real para toda a área física dos tiles..."));

        var elevation =
            await _elevationClient
                .DownloadAsync(
                    googleElevationApiKey,
                    elevationCoverage.South,
                    elevationCoverage.West,
                    elevationCoverage.North,
                    elevationCoverage.East,
                    elevationRows,
                    elevationColumns,
                    elevationProgress,
                    cancellationToken)
                .ConfigureAwait(false);

        cancellationToken
            .ThrowIfCancellationRequested();

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
                elevation,
                buildingVisualEvidenceProvider,
                catalog.ConstructionAssets
                    .RoadSplines,
                catalog.ConstructionAssets
                    .JunctionObjects,
                catalog.ConstructionAssets
                    .TrafficSignalObjects,
                catalog.ConstructionAssets
                    .CrosswalkAssets)
            .ConfigureAwait(false);
    }

    public async Task<MapStudioRealWorldMapPipelineResult>
        RunAutoIndexedWithOpenMeteoElevationAsync(
            string omsiRoot,
            string mapDirectory,
            double south,
            double west,
            double north,
            double east,
            MapStudioGeographicAnchor anchor,
            string? openMeteoApiKey,
            int elevationRows = 17,
            int elevationColumns = 17,
            IReadOnlyDictionary<
                string,
                IReadOnlyList<MapStudioSceneEvidence>>?
                streetFurnitureEvidence = null,
            IProgress<MapStudioRealWorldMapPipelineProgress>?
                progress = null,
            IProgress<OmsiAssetIndexProgress>?
                assetIndexProgress = null,
            IProgress<MapStudioElevationDownloadProgress>?
                elevationProgress = null,
            CancellationToken cancellationToken = default,
            IMapStudioBuildingVisualEvidenceProvider?
                buildingVisualEvidenceProvider = null)
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

        progress?.Report(
            new MapStudioRealWorldMapPipelineProgress(
                MapStudioRealWorldMapPipelineStage
                    .IndexingAssets,
                $"Biblioteca OMSI pronta · {catalog.ConstructionAssets.RoadSplines.Count} spline(s) de rua · " +
                $"{catalog.ConstructionAssets.JunctionObjects.Count} cruzamento(s) · " +
                $"{catalog.ConstructionAssets.TrafficSignalObjects.Count} semáforo(s) · " +
                $"{catalog.ConstructionAssets.CrosswalkAssets.Count} faixa(s)/travessia(s) reconhecida(s)."));

        cancellationToken
            .ThrowIfCancellationRequested();

        var elevationCoverage =
            await ResolveElevationCoverageAsync(
                    mapDirectory,
                    anchor,
                    south,
                    west,
                    north,
                    east,
                    cancellationToken)
                .ConfigureAwait(false);

        progress?.Report(
            new MapStudioRealWorldMapPipelineProgress(
                MapStudioRealWorldMapPipelineStage
                    .DownloadingElevation,
                string.IsNullOrWhiteSpace(
                    openMeteoApiKey)
                    ? "Baixando grade Open-Meteo/Copernicus gratuita para todos os tiles..."
                    : "Baixando grade Open-Meteo/Copernicus customer para todos os tiles..."));

        var elevation =
            await _openMeteoElevationClient
                .DownloadAsync(
                    openMeteoApiKey,
                    elevationCoverage.South,
                    elevationCoverage.West,
                    elevationCoverage.North,
                    elevationCoverage.East,
                    elevationRows,
                    elevationColumns,
                    elevationProgress,
                    cancellationToken)
                .ConfigureAwait(false);

        cancellationToken
            .ThrowIfCancellationRequested();

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
                elevation,
                buildingVisualEvidenceProvider,
                catalog.ConstructionAssets
                    .RoadSplines,
                catalog.ConstructionAssets
                    .JunctionObjects,
                catalog.ConstructionAssets
                    .TrafficSignalObjects,
                catalog.ConstructionAssets
                    .CrosswalkAssets)
            .ConfigureAwait(false);
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
            MapStudioGeoreferencedElevationSurface? elevation = null,
            IMapStudioBuildingVisualEvidenceProvider?
                buildingVisualEvidenceProvider = null)
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

        progress?.Report(
            new MapStudioRealWorldMapPipelineProgress(
                MapStudioRealWorldMapPipelineStage
                    .IndexingAssets,
                $"Biblioteca OMSI pronta · {catalog.ConstructionAssets.RoadSplines.Count} spline(s) de rua · " +
                $"{catalog.ConstructionAssets.JunctionObjects.Count} cruzamento(s) · " +
                $"{catalog.ConstructionAssets.TrafficSignalObjects.Count} semáforo(s) · " +
                $"{catalog.ConstructionAssets.CrosswalkAssets.Count} faixa(s)/travessia(s) reconhecida(s)."));

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
                elevation,
                buildingVisualEvidenceProvider,
                catalog.ConstructionAssets
                    .RoadSplines,
                catalog.ConstructionAssets
                    .JunctionObjects,
                catalog.ConstructionAssets
                    .TrafficSignalObjects,
                catalog.ConstructionAssets
                    .CrosswalkAssets)
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
            MapStudioGeoreferencedElevationSurface? elevation = null,
            IMapStudioBuildingVisualEvidenceProvider?
                buildingVisualEvidenceProvider = null,
            IReadOnlyList<OmsiAssetIndexEntry>?
                roadSplineAssets = null,
            IReadOnlyList<OmsiAssetIndexEntry>?
                junctionObjectAssets = null,
            IReadOnlyList<OmsiAssetIndexEntry>?
                trafficSignalObjectAssets = null,
            IReadOnlyList<OmsiAssetIndexEntry>?
                crosswalkAssets = null)
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

        var osmCoverage =
            await ResolveElevationCoverageAsync(
                    mapRoot,
                    anchor,
                    south,
                    west,
                    north,
                    east,
                    cancellationToken)
                .ConfigureAwait(false);

        progress?.Report(
            new MapStudioRealWorldMapPipelineProgress(
                MapStudioRealWorldMapPipelineStage
                    .DownloadingOpenStreetMap,
                $"Baixando OpenStreetMap para toda a cobertura física dos tiles · " +
                $"{osmCoverage.South:F6},{osmCoverage.West:F6} → {osmCoverage.North:F6},{osmCoverage.East:F6}..."));

        var osmProgress =
            progress is null
                ? null
                : new Progress<
                    MapStudioOverpassSceneDownloadProgress>(
                        update =>
                            progress.Report(
                                new MapStudioRealWorldMapPipelineProgress(
                                    MapStudioRealWorldMapPipelineStage
                                        .DownloadingOpenStreetMap,
                                    update.Message)));

        var osmCacheRoot =
            Path.Combine(
                root,
                ".mapstudio",
                "cache",
                "osm");

        var download =
            _layeredSceneClient is null
                ? await _sceneClient
                    .DownloadAsync(
                        osmCoverage.South,
                        osmCoverage.West,
                        osmCoverage.North,
                        osmCoverage.East,
                        cancellationToken,
                        osmProgress)
                    .ConfigureAwait(false)
                : await _layeredSceneClient
                    .DownloadAsync(
                        osmCoverage.South,
                        osmCoverage.West,
                        osmCoverage.North,
                        osmCoverage.East,
                        osmCacheRoot,
                        cancellationToken,
                        osmProgress)
                    .ConfigureAwait(false);

        cancellationToken
            .ThrowIfCancellationRequested();

        progress?.Report(
            new MapStudioRealWorldMapPipelineProgress(
                MapStudioRealWorldMapPipelineStage
                    .DownloadingOpenStreetMap,
                $"OpenStreetMap recebido: {download.NodeCount} nó(s), {download.WayCount} via(s)/polígono(s), {download.RelationCount} relação(ões) · {download.SuccessfulChunkCount} bloco(s)."));

        if (
            download.NodeCount == 0 &&
            download.WayCount == 0 &&
            download.RelationCount == 0)
        {
            throw new InvalidDataException(
                "OpenStreetMap não retornou nenhum elemento para a cobertura física dos tiles. A reconstrução não será marcada como concluída.");
        }

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
                    roadSplineAssets is { Count: > 0 }
                        ? "Gerando vias reais com splines OMSI compatíveis · Road Kit como fallback..."
                        : "Gerando vias reais com o Road Kit..."));

            var roads =
                await new MapStudioRoadBatchReconstructionRunner()
                    .RunAsync(
                        root,
                        mapRoot,
                        download.OsmXml,
                        anchor,
                        cancellationToken,
                        elevation,
                        roadSplineAssets,
                        junctionObjectAssets,
                        trafficSignalObjectAssets,
                        crosswalkAssets)
                    .ConfigureAwait(false);

            progress?.Report(
                new MapStudioRealWorldMapPipelineProgress(
                    MapStudioRealWorldMapPipelineStage
                        .GeneratingRoads,
                    $"Vias montadas · {roads.InstalledOmsiSplineCount} trecho(s) com SLI instalada do OMSI · " +
                    $"{roads.RoadKitFallbackSplineCount} fallback(s) do Road Kit · " +
                    $"{roads.InstalledOmsiJunctionCount} cruzamento(s) stock OMSI · " +
                    $"{roads.GeneratedJunctionCount} cruzamento(s) gerado(s) · " +
                    $"{roads.InstalledTrafficSignalCount} semáforo(s) stock · " +
                    $"{roads.InstalledCrosswalkCount} travessia(s) stock."));

            progress?.Report(
                new MapStudioRealWorldMapPipelineProgress(
                    MapStudioRealWorldMapPipelineStage
                        .ReconstructingScene,
                    "Reconstruindo prédios, vegetação, infraestrutura e mobiliário..."));

            IProgress<MapStudioBuildingVisualEvidenceProgress>?
                buildingVisualEvidenceProgress =
                    buildingVisualEvidenceProvider is null
                        ? null
                        : new Progress<MapStudioBuildingVisualEvidenceProgress>(
                            update =>
                                progress?.Report(
                                    new MapStudioRealWorldMapPipelineProgress(
                                        MapStudioRealWorldMapPipelineStage
                                            .ReconstructingScene,
                                        update.Message)));

            var excludedStreetFurnitureFeatureIds =
                roads.ConsumedStreetFurnitureFeatureIds is
                    { Count: > 0 } consumedFeatureIds
                    ? consumedFeatureIds
                        .ToHashSet(
                            StringComparer.Ordinal)
                    : null;

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
                        elevation,
                        buildingVisualEvidenceProvider,
                        buildingVisualEvidenceProgress,
                        excludedStreetFurnitureFeatureIds)
                    .ConfigureAwait(false);

            var result =
                new MapStudioRealWorldMapPipelineResult(
                    download,
                    roads,
                    scene,
                    snapshot.BackupRoot,
                    elevationResult);

            if (result.PlacedElementCount <= 0)
            {
                throw new InvalidDataException(
                    $"OpenStreetMap retornou dados ({download.NodeCount} nó(s), {download.WayCount} via(s)/polígono(s), {download.RelationCount} relação(ões)), " +
                    "mas nenhuma via, junction, estrutura ou objeto pôde ser colocado dentro dos tiles criados. " +
                    $"Vias ignoradas={roads.IgnoredWayCount}, segmentos fora do mapa={roads.SkippedOutsideMapSegmentCount}, referências de nó ausentes={roads.MissingNodeReferenceCount}, itens para revisão={scene.ReviewCount}. " +
                    "A sessão foi considerada incompleta e será restaurada.");
            }

            progress?.Report(
                new MapStudioRealWorldMapPipelineProgress(
                    MapStudioRealWorldMapPipelineStage
                        .Completed,
                    $"Mapa real concluído: {roads.PlacedSplineCount} spline(s) de via, {roads.JunctionPlacements.Count} junction(s), {roads.JunctionAccessoryCount} controle(s) viário(s) stock, {roads.GeneratedStructureCount} estrutura(s) viária(s) e {scene.PlacedObjectCount} objeto(s) de cenário."));

            return result;
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

    private static async Task<MapStudioElevationCoverageBounds>
        ResolveElevationCoverageAsync(
            string mapDirectory,
            MapStudioGeographicAnchor anchor,
            double selectedSouth,
            double selectedWest,
            double selectedNorth,
            double selectedEast,
            CancellationToken cancellationToken)
    {
        var descriptor =
            await OmsiMapCatalog
                .OpenMapAsync(
                    mapDirectory,
                    cancellationToken)
                .ConfigureAwait(false);

        var tileCoverage =
            MapStudioElevationCoverageResolver
                .Resolve(
                    descriptor.Tiles,
                    anchor,
                    marginMeters:
                        1.0);

        return MapStudioElevationCoverageResolver
            .Union(
                tileCoverage,
                selectedSouth,
                selectedWest,
                selectedNorth,
                selectedEast);
    }
}
