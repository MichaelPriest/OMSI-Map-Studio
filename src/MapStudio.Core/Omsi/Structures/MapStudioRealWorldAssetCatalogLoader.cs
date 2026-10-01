using MapStudio.Core.Omsi.Indexing;

namespace MapStudio.Core.Omsi.Structures;

public sealed record MapStudioRealWorldAssetCatalogResult(
    string DatabasePath,
    OmsiAssetIndexRefreshResult Refresh,
    IReadOnlyList<OmsiAssetIndexEntry> SceneryObjects,
    IReadOnlyList<OmsiAssetIndexEntry> Splines,
    MapStudioOmsiConstructionAssetCatalog ConstructionAssets);

public sealed class MapStudioRealWorldAssetCatalogLoader
{
    public async Task<MapStudioRealWorldAssetCatalogResult>
        LoadAsync(
            string omsiRoot,
            IProgress<OmsiAssetIndexProgress>? progress = null,
            CancellationToken cancellationToken = default)
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

        var refresh =
            await index
                .RefreshAsync(
                    root,
                    progress,
                    cancellationToken)
                .ConfigureAwait(false);

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
            constructionAssets);
    }
}
