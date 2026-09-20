using MapStudio.Core.IO;
using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Renderer.Viewport;

namespace MapStudio.Native.Services;

public sealed record NativeLoadedTile(
    OmsiTileReference Reference,
    OmsiTileContent Content);

public sealed record NativeMapSnapshot(
    OmsiMapDescriptor Map,
    OmsiTileReference? ActiveTile,
    IReadOnlyList<NativeLoadedTile> Tiles)
{
    public int ObjectCount =>
        Tiles.Sum(
            tile =>
                tile.Content.Objects.Count);

    public int SplineCount =>
        Tiles.Sum(
            tile =>
                tile.Content.Splines.Count);

    public int TerrainCount =>
        Tiles.Count(
            tile =>
                tile.Content.Terrain is not null);
}

public sealed class OmsiNativeSession
{
    private readonly OmsiTileReader _tileReader =
        new();

    private readonly Dictionary<
        string,
        NativePendingTransformEdit>
        _pendingTransforms =
            new(
                StringComparer.OrdinalIgnoreCase);

    public string? OmsiRootPath { get; private set; }

    public IReadOnlyList<OmsiMapDescriptor> Maps { get; private set; } =
        Array.Empty<OmsiMapDescriptor>();

    public NativeMapSnapshot? CurrentMap { get; private set; }

    public int PendingTransformCount =>
        _pendingTransforms.Count;

    public void StageTransformEdit(
        NativePendingTransformEdit edit)
    {
        ArgumentNullException.ThrowIfNull(
            edit);

        var key =
            CreatePendingKey(
                edit);

        _pendingTransforms[key] =
            edit;
    }

    public async Task<NativeMapSnapshot>
        SavePendingTransformsAsync(
            CancellationToken cancellationToken =
                default)
    {
        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        if (_pendingTransforms.Count == 0)
        {
            return snapshot;
        }

        var staged =
            _pendingTransforms.Values
                .ToArray();

        var writes =
            new List<
                PendingFileWrite>();

        var affectedPaths =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (
            var group in
                staged.GroupBy(
                    edit =>
                        edit.Tile
                            .RelativeMapPath,
                    StringComparer
                        .OrdinalIgnoreCase))
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            if (
                !OmsiMapPathResolver
                    .TryResolveTilePath(
                        snapshot.Map
                            .DirectoryPath,
                        group.Key,
                        out var tilePath))
            {
                throw new InvalidDataException(
                    "tilePathInvalid");
            }

            var document =
                await OmsiConfigParser
                    .ParseFileAsync(
                        tilePath,
                        cancellationToken)
                    .ConfigureAwait(false);

            var objectEdits =
                group
                    .Where(
                        edit =>
                            edit.ObjectEdit is
                                not null)
                    .Select(
                        edit =>
                            edit.ObjectEdit!)
                    .ToArray();

            if (objectEdits.Length > 0)
            {
                var result =
                    OmsiTileObjectEditor
                        .ApplyTransforms(
                            document,
                            objectEdits);

                document =
                    OmsiConfigParser
                        .ParseBytes(
                            result.Bytes);
            }

            var splineEdits =
                group
                    .Where(
                        edit =>
                            edit.SplineEdit is
                                not null)
                    .Select(
                        edit =>
                            edit.SplineEdit!)
                    .ToArray();

            if (splineEdits.Length > 0)
            {
                var result =
                    OmsiTileSplineEditor
                        .ApplyTransforms(
                            document,
                            splineEdits);

                document =
                    OmsiConfigParser
                        .ParseBytes(
                            result.Bytes);
            }

            var backupDirectory =
                Path.Combine(
                    snapshot.Map
                        .DirectoryPath,
                    ".mapstudio-backups");

            var backupPath =
                Path.Combine(
                    backupDirectory,
                    Path.GetFileName(
                        tilePath) +
                    "." +
                    DateTime.UtcNow
                        .ToString(
                            "yyyyMMdd-HHmmssfff") +
                    "." +
                    Guid.NewGuid()
                        .ToString("N") +
                    ".bak");

            writes.Add(
                new PendingFileWrite(
                    tilePath,
                    backupPath,
                    document.ToBytes()));

            affectedPaths.Add(
                group.Key);
        }

        await SafeFileTransaction
            .WriteAllAsync(
                writes,
                cancellationToken)
            .ConfigureAwait(false);

        var refreshed =
            new List<
                NativeLoadedTile>(
                    snapshot.Tiles.Count);

        foreach (var tile in snapshot.Tiles)
        {
            if (
                !affectedPaths.Contains(
                    tile.Reference
                        .RelativeMapPath))
            {
                refreshed.Add(
                    tile);

                continue;
            }

            if (
                !OmsiMapPathResolver
                    .TryResolveTilePath(
                        snapshot.Map
                            .DirectoryPath,
                        tile.Reference
                            .RelativeMapPath,
                        out var tilePath))
            {
                throw new InvalidDataException(
                    "tilePathInvalidAfterSave");
            }

            var content =
                await _tileReader
                    .ReadContentAsync(
                        tilePath,
                        cancellationToken)
                    .ConfigureAwait(false);

            refreshed.Add(
                new NativeLoadedTile(
                    tile.Reference,
                    content));
        }

        CurrentMap =
            snapshot with
            {
                Tiles =
                    refreshed
                        .ToArray()
            };

        _pendingTransforms.Clear();

        return CurrentMap;
    }

    public async Task<
        IReadOnlyList<OmsiMapDescriptor>>
        SelectOmsiRootAsync(
            string rootPath,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            rootPath);

        var normalized =
            Path.GetFullPath(
                rootPath)
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);

        var mapsDirectory =
            Path.Combine(
                normalized,
                "maps");

        if (!Directory.Exists(
                mapsDirectory))
        {
            throw new InvalidDataException(
                @"A pasta selecionada não contém OMSI 2\maps.");
        }

        var maps =
            await new OmsiMapCatalog()
                .DiscoverAsync(
                    normalized,
                    cancellationToken)
                .ConfigureAwait(false);

        OmsiRootPath = normalized;
        Maps = maps;
        CurrentMap = null;
        _pendingTransforms.Clear();

        return maps;
    }

    public async Task<NativeMapSnapshot>
        OpenMapAsync(
            string mapDirectory,
            CancellationToken cancellationToken =
                default)
    {
        var root =
            OmsiRootPath ??
            throw new InvalidOperationException(
                "Selecione primeiro a instalação do OMSI 2.");

        ArgumentException.ThrowIfNullOrWhiteSpace(
            mapDirectory);

        var mapsRoot =
            Path.GetFullPath(
                Path.Combine(
                    root,
                    "maps"))
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);

        var normalizedMap =
            Path.GetFullPath(
                mapDirectory)
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);

        var requiredPrefix =
            mapsRoot +
            Path.DirectorySeparatorChar;

        if (
            !normalizedMap.StartsWith(
                requiredPrefix,
                StringComparison
                    .OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                @"O mapa precisa estar dentro de OMSI 2\maps.");
        }

        var map =
            await OmsiMapCatalog
                .OpenMapAsync(
                    normalizedMap,
                    cancellationToken)
                .ConfigureAwait(false);

        var initialTile =
            OmsiTileRegionSelector
                .FindInitialTile(
                    map.Tiles);

        var selectedTiles =
            initialTile is null
                ? Array.Empty<
                    OmsiTileReference>()
                : OmsiTileRegionSelector
                    .Select(
                        map.Tiles,
                        initialTile.X,
                        initialTile.Y,
                        radius: 1);

        var loadTasks =
            selectedTiles
                .Select(
                    tile =>
                        LoadTileAsync(
                            map,
                            tile,
                            cancellationToken))
                .ToArray();

        var loadedTiles =
            await Task.WhenAll(
                loadTasks)
                .ConfigureAwait(false);

        var snapshot =
            new NativeMapSnapshot(
                map,
                initialTile,
                loadedTiles
                    .Where(
                        tile =>
                            tile is not null)
                    .Select(
                        tile => tile!)
                    .ToArray());

        CurrentMap = snapshot;
        _pendingTransforms.Clear();

        return snapshot;
    }

    private static string CreatePendingKey(
        NativePendingTransformEdit edit)
    {
        if (
            edit.ObjectEdit is
                { } objectEdit)
        {
            return
                $"object|{edit.Tile.X}|{edit.Tile.Y}|" +
                $"{objectEdit.SourceSectionOrdinal}|" +
                $"{objectEdit.ObjectId}|" +
                objectEdit.SceneryObjectPath;
        }

        if (
            edit.SplineEdit is
                { } splineEdit)
        {
            return
                $"spline|{edit.Tile.X}|{edit.Tile.Y}|" +
                $"{splineEdit.SourceSectionOrdinal}|" +
                $"{splineEdit.SplineId}|" +
                splineEdit.SplinePath;
        }

        throw new InvalidDataException(
            "pendingTransformMissingEdit");
    }

    private async Task<NativeLoadedTile?>
        LoadTileAsync(
            OmsiMapDescriptor map,
            OmsiTileReference tile,
            CancellationToken cancellationToken)
    {
        if (
            !OmsiMapPathResolver
                .TryResolveTilePath(
                    map.DirectoryPath,
                    tile.RelativeMapPath,
                    out var tilePath))
        {
            return null;
        }

        var content =
            await _tileReader
                .ReadContentAsync(
                    tilePath,
                    cancellationToken)
                .ConfigureAwait(false);

        return new NativeLoadedTile(
            tile,
            content);
    }
}
