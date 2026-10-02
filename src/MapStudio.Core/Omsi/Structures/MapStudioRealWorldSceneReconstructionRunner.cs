using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Terrain;
using MapStudio.Core.Generation.Scene;
using MapStudio.Core.Omsi.Indexing;
using MapStudio.Core.Omsi.Maps;

namespace MapStudio.Core.Omsi.Structures;

public sealed record MapStudioRealWorldSceneReconstructionResult(
    MapStudioInfrastructureBatchResult Infrastructure,
    MapStudioBuildingBatchReconstructionResult Buildings,
    MapStudioVegetationBatchReconstructionResult Vegetation,
    MapStudioStreetFurnitureBatchReconstructionResult StreetFurniture,
    string SessionBackupDirectory)
{
    public int PlacedObjectCount =>
        Infrastructure.Placements.Count +
        Buildings.Placements.Count +
        Vegetation.Placements.Count +
        StreetFurniture.Placements.Count;

    public int ReviewCount =>
        Infrastructure.ReviewFeatureIds.Count +
        Buildings.ReviewBuildingIds.Count +
        Vegetation.ReviewVegetationIds.Count +
        StreetFurniture.ReviewFeatureIds.Count;

    public int VisualRefinedBuildingCount =>
        Buildings.VisualRefinedBuildingCount;
}

public sealed class MapStudioRealWorldSceneReconstructionRunner
{
    public async Task<MapStudioRealWorldSceneReconstructionResult>
        RunAsync(
            string omsiRoot,
            string mapDirectory,
            string osmXml,
            MapStudioGeographicAnchor anchor,
            IReadOnlyList<OmsiAssetIndexEntry> assets,
            IReadOnlyDictionary<
                string,
                IReadOnlyList<MapStudioSceneEvidence>>?
                streetFurnitureEvidence = null,
            CancellationToken cancellationToken = default,
            MapStudioGeoreferencedElevationSurface? elevation = null,
            IMapStudioBuildingVisualEvidenceProvider?
                buildingVisualEvidenceProvider = null,
            IProgress<MapStudioBuildingVisualEvidenceProgress>?
                buildingVisualEvidenceProgress = null,
            IReadOnlySet<string>?
                excludedStreetFurnitureFeatureIds = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(omsiRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(mapDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(osmXml);
        ArgumentNullException.ThrowIfNull(assets);

        var root =
            Path.GetFullPath(omsiRoot);

        var mapRoot =
            Path.GetFullPath(mapDirectory);

        var snapshot =
            await CreateSessionSnapshotAsync(
                    root,
                    mapRoot,
                    cancellationToken)
                .ConfigureAwait(false);

        try
        {
            var infrastructure =
                await new MapStudioInfrastructureBatchReconstructionRunner()
                    .RunAsync(
                        root,
                        mapRoot,
                        osmXml,
                        anchor,
                        cancellationToken,
                        elevation)
                    .ConfigureAwait(false);

            var buildings =
                await new MapStudioBuildingBatchReconstructionRunner()
                    .RunAsync(
                        root,
                        mapRoot,
                        osmXml,
                        anchor,
                        cancellationToken,
                        elevation,
                        buildingVisualEvidenceProvider,
                        buildingVisualEvidenceProgress)
                    .ConfigureAwait(false);

            var vegetation =
                await new MapStudioVegetationBatchReconstructionRunner()
                    .RunAsync(
                        root,
                        mapRoot,
                        osmXml,
                        anchor,
                        assets,
                        cancellationToken,
                        elevation)
                    .ConfigureAwait(false);

            var streetFurniture =
                await new MapStudioStreetFurnitureBatchReconstructionRunner()
                    .RunAsync(
                        root,
                        mapRoot,
                        osmXml,
                        anchor,
                        assets,
                        streetFurnitureEvidence,
                        cancellationToken,
                        elevation,
                        excludedStreetFurnitureFeatureIds)
                    .ConfigureAwait(false);

            return new MapStudioRealWorldSceneReconstructionResult(
                infrastructure,
                buildings,
                vegetation,
                streetFurniture,
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
                    "realWorldSceneRollbackFailed",
                    original,
                    rollback);
            }

            throw;
        }
    }

    private static async Task<SceneSnapshot>
        CreateSessionSnapshotAsync(
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
                "real-world-scene-session",
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
                    "realWorldSceneTilePathInvalid");
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

        return new SceneSnapshot(
            backupRoot,
            files);
    }

    private static void RestoreSnapshot(
        SceneSnapshot snapshot)
    {
        foreach (var file in snapshot.Files)
        {
            if (!File.Exists(file.BackupPath))
            {
                throw new InvalidDataException(
                    "realWorldSceneSnapshotMissing");
            }

            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    file.OriginalPath)!);

            File.Copy(
                file.BackupPath,
                file.OriginalPath,
                overwrite: true);
        }
    }

    private sealed record SceneSnapshot(
        string BackupRoot,
        IReadOnlyList<SnapshotFile> Files);

    private sealed record SnapshotFile(
        string OriginalPath,
        string BackupPath);
}
