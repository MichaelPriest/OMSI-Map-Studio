using MapStudio.Core.Omsi.Maps;

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

    public string? OmsiRootPath { get; private set; }

    public IReadOnlyList<OmsiMapDescriptor> Maps { get; private set; } =
        Array.Empty<OmsiMapDescriptor>();

    public NativeMapSnapshot? CurrentMap { get; private set; }

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
                "A pasta selecionada não contém OMSI 2\maps.");
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
                "O mapa precisa estar dentro de OMSI 2\maps.");
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

        return snapshot;
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
