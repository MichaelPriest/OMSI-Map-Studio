using Microsoft.Data.Sqlite;
using MapStudio.Core.Omsi.Indexing;

namespace MapStudio.Core.Omsi.Structures;

public sealed record MapStudioRealWorldAssetCatalogResult(
    string DatabasePath,
    OmsiAssetIndexRefreshResult Refresh,
    IReadOnlyList<OmsiAssetIndexEntry> SceneryObjects,
    IReadOnlyList<OmsiAssetIndexEntry> Splines,
    MapStudioOmsiConstructionAssetCatalog ConstructionAssets,
    bool ReusedExistingIndex = false);

public sealed class MapStudioRealWorldAssetCatalogLoader
{
    public async Task<MapStudioRealWorldAssetCatalogResult>
        LoadAsync(
            string omsiRoot,
            IProgress<OmsiAssetIndexProgress>? progress = null,
            CancellationToken cancellationToken = default,
            bool forceRefresh = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(omsiRoot);

        var root =
            Path.GetFullPath(omsiRoot);

        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException(root);
        }

        var databasePath =
            Path.Combine(
                root,
                ".mapstudio",
                "cache",
                "assets.sqlite");

        var index =
            new OmsiAssetIndex(databasePath);

        var reusedExistingIndex =
            false;

        OmsiAssetIndexRefreshResult refresh;

        if (
            !forceRefresh &&
            File.Exists(databasePath))
        {
            try
            {
                var statistics =
                    await index
                        .GetStatisticsAsync(
                            cancellationToken)
                        .ConfigureAwait(false);

                if (statistics.RefreshedAtUtc is not null)
                {
                    refresh =
                        new OmsiAssetIndexRefreshResult(
                            ExaminedFiles:
                                0,
                            TotalEntries:
                                statistics.TotalEntries,
                            AddedFiles:
                                0,
                            UpdatedFiles:
                                0,
                            UnchangedFiles:
                                statistics.TotalEntries,
                            RemovedFiles:
                                0,
                            DurationMilliseconds:
                                0);

                    reusedExistingIndex =
                        true;
                }
                else
                {
                    refresh =
                        await index
                            .RefreshAsync(
                                root,
                                progress,
                                cancellationToken)
                            .ConfigureAwait(false);
                }
            }
            catch (
                SqliteException)
            {
                refresh =
                    await index
                        .RefreshAsync(
                            root,
                            progress,
                            cancellationToken)
                        .ConfigureAwait(false);
            }
        }
        else
        {
            refresh =
                await index
                    .RefreshAsync(
                        root,
                        progress,
                        cancellationToken)
                    .ConfigureAwait(false);
        }

        var scenery =
            await index
                .GetEntriesAsync(
                    OmsiAssetKind.SceneryObject,
                    limit: 1_000_000,
                    cancellationToken)
                .ConfigureAwait(false);

        var splines =
            await index
                .GetEntriesAsync(
                    OmsiAssetKind.Spline,
                    limit: 1_000_000,
                    cancellationToken)
                .ConfigureAwait(false);

        var constructionAssets =
            MapStudioOmsiConstructionAssetClassifier
                .Build(
                    [
                        .. scenery,
                        .. splines
                    ]);

        return new MapStudioRealWorldAssetCatalogResult(
            databasePath,
            refresh,
            scenery,
            splines,
            constructionAssets,
            reusedExistingIndex);
    }
}
