using MapStudio.Core.IO;
using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Indexing;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Scenery;
using MapStudio.Core.Omsi.Splines;
using MapStudio.Core.Omsi.Timetables;
using MapStudio.Renderer.Viewport;
using MapStudio.Renderer.Picking;
using System.Globalization;
using System.Text.Json;

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
    private static readonly HttpClient
        GoogleMapsHttpClient =
            new()
            {
                Timeout =
                    TimeSpan.FromSeconds(
                        30)
            };

    private readonly OmsiTileReader _tileReader =
        new();

    private readonly Dictionary<
        string,
        NativePendingTransformEdit>
        _pendingTransforms =
            new(
                StringComparer.OrdinalIgnoreCase);

    private OmsiAssetIndex?
        _assetIndex;

    public string? OmsiRootPath { get; private set; }

    public IReadOnlyList<OmsiMapDescriptor> Maps { get; private set; } =
        Array.Empty<OmsiMapDescriptor>();

    public NativeMapSnapshot? CurrentMap { get; private set; }

    public string? LastBackupDirectory { get; private set; }

    public int PendingTransformCount =>
        _pendingTransforms.Count;

    public async Task<
        IReadOnlyList<
            OmsiAssetIndexEntry>>
        GetAssetLibraryAsync(
            OmsiAssetKind? kind = null,
            int limit = 100_000,
            CancellationToken cancellationToken =
                default)
    {
        var index =
            _assetIndex ??
            throw new InvalidOperationException(
                "Selecione primeiro a instalação do OMSI 2.");

        return
            await index
                .GetEntriesAsync(
                    kind,
                    limit,
                    cancellationToken)
                .ConfigureAwait(false);
    }

    public async Task<
        OmsiAssetIndexRefreshResult>
        RefreshAssetLibraryAsync(
            IProgress<
                OmsiAssetIndexProgress>?
                progress = null,
            CancellationToken cancellationToken =
                default)
    {
        var root =
            OmsiRootPath ??
            throw new InvalidOperationException(
                "Selecione primeiro a instalação do OMSI 2.");

        var index =
            _assetIndex ??
            throw new InvalidOperationException(
                "Índice de assets não inicializado.");

        return
            await index
                .RefreshAsync(
                    root,
                    progress,
                    cancellationToken)
                .ConfigureAwait(false);
    }

    public async Task<
        OmsiAssetIndexStatistics>
        GetAssetLibraryStatisticsAsync(
            CancellationToken cancellationToken =
                default)
    {
        var index =
            _assetIndex ??
            throw new InvalidOperationException(
                "Selecione primeiro a instalação do OMSI 2.");

        return
            await index
                .GetStatisticsAsync(
                    cancellationToken)
                .ConfigureAwait(false);
    }

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

    public async Task<NativeGoogleMapReference>
        LoadGoogleMapReferenceAsync(
            string apiKey,
            int width = 640,
            int height = 640,
            CancellationToken cancellationToken =
                default)
    {
        if (
            string.IsNullOrWhiteSpace(
                apiKey) ||
            width is < 128 or > 640 ||
            height is < 128 or > 640)
        {
            throw new InvalidDataException(
                "invalidGoogleReferenceRequest");
        }

        var georeference =
            await LoadMapGeoreferenceAsync(
                    cancellationToken)
                .ConfigureAwait(false)
            ?? throw new InvalidDataException(
                "mapGeoreferenceRequired");

        ValidateGeoreference(
            georeference);

        var invariant =
            CultureInfo.InvariantCulture;

        var center =
            string.Create(
                invariant,
                $"{georeference.Latitude:G17},{georeference.Longitude:G17}");

        var mapType =
            georeference.MapType
                .Trim()
                .ToLowerInvariant();

        var uri =
            "https://maps.googleapis.com/maps/api/staticmap" +
            "?center=" +
            Uri.EscapeDataString(
                center) +
            "&zoom=" +
            georeference.Zoom.ToString(
                invariant) +
            "&size=" +
            width.ToString(
                invariant) +
            "x" +
            height.ToString(
                invariant) +
            "&scale=1&format=png&maptype=" +
            Uri.EscapeDataString(
                mapType) +
            "&key=" +
            Uri.EscapeDataString(
                apiKey.Trim());

        using var response =
            await GoogleMapsHttpClient
                .GetAsync(
                    uri,
                    cancellationToken)
                .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"googleMapsReferenceHttp:{(int)response.StatusCode}");
        }

        var bytes =
            await response.Content
                .ReadAsByteArrayAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        if (
            bytes.Length == 0 ||
            bytes.LongLength >
                16L *
                1024L *
                1024L)
        {
            throw new InvalidDataException(
                "googleMapsReferenceInvalidPayload");
        }

        var cacheRoot =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder
                        .LocalApplicationData),
                "OMSI Map Studio",
                "reference-cache");

        Directory.CreateDirectory(
            cacheRoot);

        foreach (
            var stale in
                Directory
                    .EnumerateFiles(
                        cacheRoot,
                        "google-reference-*.png")
                    .OrderByDescending(
                        File.GetLastWriteTimeUtc)
                    .Skip(8)
                    .ToArray())
        {
            try
            {
                File.Delete(stale);
            }
            catch
            {
            }
        }

        var path =
            Path.Combine(
                cacheRoot,
                "google-reference-" +
                Guid.NewGuid()
                    .ToString("N") +
                ".png");

        await File.WriteAllBytesAsync(
                path,
                bytes,
                cancellationToken)
            .ConfigureAwait(false);

        var metersPerPixel =
            156543.03392804097 *
            Math.Cos(
                georeference.Latitude *
                Math.PI /
                180.0) /
            Math.Pow(
                2,
                georeference.Zoom);

        var anchorWorldX =
            georeference.AnchorTileX *
                300.0 +
            georeference.AnchorX;

        var anchorWorldZ =
            georeference.AnchorTileY *
                300.0 +
            georeference.AnchorY;

        return new NativeGoogleMapReference(
            path,
            width,
            height,
            metersPerPixel,
            anchorWorldX,
            anchorWorldZ,
            georeference.Latitude,
            georeference.Longitude,
            georeference.Zoom,
            mapType,
            "Google Maps");
    }

    public async Task<NativeGoogleElevationGrid>
        LoadGoogleElevationGridAsync(
            string apiKey,
            int tileX,
            int tileY,
            int sampleCount,
            CancellationToken cancellationToken =
                default)
    {
        if (
            string.IsNullOrWhiteSpace(
                apiKey) ||
            sampleCount is
                < 3 or > 33)
        {
            throw new InvalidDataException(
                "invalidGoogleElevationRequest");
        }

        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        if (
            !snapshot.Map.Tiles.Any(
                tile =>
                    tile.X ==
                        tileX &&
                    tile.Y ==
                        tileY))
        {
            throw new InvalidDataException(
                "unknownTile");
        }

        var georeference =
            await LoadMapGeoreferenceAsync(
                    cancellationToken)
                .ConfigureAwait(false)
            ?? throw new InvalidDataException(
                "mapGeoreferenceRequired");

        ValidateGeoreference(
            georeference);

        const double earthRadius =
            6378137.0;

        var anchorWorldX =
            georeference.AnchorTileX *
                300.0 +
            georeference.AnchorX;

        var anchorWorldY =
            georeference.AnchorTileY *
                300.0 +
            georeference.AnchorY;

        var coordinates =
            new List<(
                double Latitude,
                double Longitude)>(
                    checked(
                        sampleCount *
                        sampleCount));

        for (
            var row = 0;
            row < sampleCount;
            row++)
        {
            var localY =
                row *
                300.0 /
                (
                    sampleCount -
                    1
                );

            for (
                var column = 0;
                column < sampleCount;
                column++)
            {
                var localX =
                    column *
                    300.0 /
                    (
                        sampleCount -
                        1
                    );

                var worldX =
                    tileX *
                        300.0 +
                    localX;

                var worldY =
                    tileY *
                        300.0 +
                    localY;

                var eastMeters =
                    worldX -
                    anchorWorldX;

                var northMeters =
                    -(
                        worldY -
                        anchorWorldY
                    );

                var latitudeDelta =
                    northMeters /
                    earthRadius *
                    180.0 /
                    Math.PI;

                var longitudeScale =
                    Math.Cos(
                        georeference.Latitude *
                        Math.PI /
                        180.0);

                if (
                    Math.Abs(
                        longitudeScale) <
                    0.000001)
                {
                    throw new InvalidDataException(
                        "invalidGoogleElevationRequest");
                }

                var longitudeDelta =
                    eastMeters /
                    (
                        earthRadius *
                        longitudeScale
                    ) *
                    180.0 /
                    Math.PI;

                coordinates.Add(
                    (
                        georeference.Latitude +
                            latitudeDelta,
                        georeference.Longitude +
                            longitudeDelta
                    ));
            }
        }

        var elevations =
            new List<double>(
                coordinates.Count);

        const int batchSize =
            64;

        for (
            var offset = 0;
            offset < coordinates.Count;
            offset += batchSize)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            var batch =
                coordinates
                    .Skip(offset)
                    .Take(
                        Math.Min(
                            batchSize,
                            coordinates.Count -
                            offset))
                    .Select(
                        coordinate =>
                            string.Create(
                                CultureInfo
                                    .InvariantCulture,
                                $"{coordinate.Latitude:G17},{coordinate.Longitude:G17}"))
                    .ToArray();

            var uri =
                "https://maps.googleapis.com/maps/api/elevation/json" +
                "?locations=" +
                Uri.EscapeDataString(
                    string.Join(
                        "|",
                        batch)) +
                "&key=" +
                Uri.EscapeDataString(
                    apiKey.Trim());

            using var response =
                await GoogleMapsHttpClient
                    .GetAsync(
                        uri,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (!response
                .IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"googleElevationHttp:{(int)response.StatusCode}");
            }

            await using var stream =
                await response.Content
                    .ReadAsStreamAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

            using var document =
                await JsonDocument
                    .ParseAsync(
                        stream,
                        cancellationToken:
                            cancellationToken)
                    .ConfigureAwait(false);

            var root =
                document.RootElement;

            if (
                !root.TryGetProperty(
                    "status",
                    out var status) ||
                !string.Equals(
                    status.GetString(),
                    "OK",
                    StringComparison.OrdinalIgnoreCase) ||
                !root.TryGetProperty(
                    "results",
                    out var results) ||
                results.ValueKind !=
                    JsonValueKind.Array ||
                results.GetArrayLength() !=
                    batch.Length)
            {
                throw new InvalidDataException(
                    status.ValueKind ==
                        JsonValueKind.String
                        ? "googleElevation:" +
                          status.GetString()
                        : "googleElevationGridError");
            }

            foreach (
                var result in
                    results.EnumerateArray())
            {
                if (
                    !result.TryGetProperty(
                        "elevation",
                        out var elevation) ||
                    !elevation.TryGetDouble(
                        out var value) ||
                    !double.IsFinite(
                        value))
                {
                    throw new InvalidDataException(
                        "googleElevationGridError");
                }

                elevations.Add(
                    value);
            }
        }

        if (
            elevations.Count !=
            coordinates.Count)
        {
            throw new InvalidDataException(
                "googleElevationGridError");
        }

        return new NativeGoogleElevationGrid(
            tileX,
            tileY,
            sampleCount,
            sampleCount,
            elevations,
            elevations.Min(),
            elevations.Max(),
            georeference.Latitude,
            georeference.Longitude);
    }

    public async Task<NativeTerrainElevationApplyResult>
        ApplyTerrainElevationGridAsync(
            NativeGoogleElevationGrid grid,
            double verticalOffset,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentNullException.ThrowIfNull(
            grid);

        if (!double.IsFinite(
                verticalOffset))
        {
            throw new InvalidDataException(
                "invalidElevationGrid");
        }

        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        if (_pendingTransforms.Count > 0)
        {
            throw new InvalidOperationException(
                "savePendingBeforeTerrainEdit");
        }

        var tile =
            snapshot.Map.Tiles
                .FirstOrDefault(
                    candidate =>
                        candidate.X ==
                            grid.TileX &&
                        candidate.Y ==
                            grid.TileY)
            ?? throw new InvalidDataException(
                "unknownTile");

        if (
            !OmsiMapPathResolver
                .TryResolveTilePath(
                    snapshot.Map.DirectoryPath,
                    tile.RelativeMapPath,
                    out var tilePath))
        {
            throw new InvalidDataException(
                "terrainTilePathInvalid");
        }

        var terrainPath =
            tilePath +
            ".terrain";

        if (!File.Exists(
                terrainPath))
        {
            throw new InvalidDataException(
                "terrainFileMissing");
        }

        var terrain =
            await new OmsiTerrainReader()
                .ReadAsync(
                    terrainPath,
                    cancellationToken)
                .ConfigureAwait(false);

        var result =
            OmsiTerrainLeveler
                .ApplyElevationGrid(
                    terrain,
                    grid.Rows,
                    grid.Columns,
                    grid.Elevations,
                    verticalOffset);

        if (
            result.ChangedSamples ==
            0)
        {
            return new NativeTerrainElevationApplyResult(
                CurrentMap,
                0,
                string.Empty);
        }

        var timestamp =
            DateTimeOffset.UtcNow
                .ToString(
                    "yyyyMMdd-HHmmssfff'Z'",
                    CultureInfo.InvariantCulture);

        var backupRoot =
            Path.Combine(
                snapshot.Map.DirectoryPath,
                ".mapstudio-backups",
                timestamp +
                "-elevation-" +
                Guid.NewGuid()
                    .ToString("N"));

        var relativePath =
            Path.GetRelativePath(
                snapshot.Map.DirectoryPath,
                terrainPath);

        if (!IsSafeRelativePath(
                relativePath))
        {
            throw new InvalidDataException(
                "invalidTerrainPath");
        }

        await SafeFileTransaction
            .WriteAllAsync(
                [
                    new PendingFileWrite(
                        terrainPath,
                        Path.Combine(
                            backupRoot,
                            relativePath),
                        OmsiTerrainWriter
                            .Write(
                                result.Terrain))
                ],
                cancellationToken)
            .ConfigureAwait(false);

        LastBackupDirectory =
            backupRoot;

        if (
            snapshot.Tiles.Any(
                loaded =>
                    loaded.Reference.X ==
                        tile.X &&
                    loaded.Reference.Y ==
                        tile.Y))
        {
            var refreshed =
                await _tileReader
                    .ReadContentAsync(
                        tilePath,
                        cancellationToken)
                    .ConfigureAwait(false);

            CurrentMap =
                snapshot with
                {
                    Tiles =
                        snapshot.Tiles
                            .Select(
                                loaded =>
                                    loaded.Reference.X ==
                                        tile.X &&
                                    loaded.Reference.Y ==
                                        tile.Y
                                        ? new NativeLoadedTile(
                                            loaded.Reference,
                                            refreshed)
                                        : loaded)
                            .ToArray()
                };
        }

        return new NativeTerrainElevationApplyResult(
            CurrentMap!,
            result.ChangedSamples,
            backupRoot);
    }

    public async Task<NativeTileCreateResult>
        CreateTileFromTemplateAsync(
            int tileX,
            int tileY,
            CancellationToken cancellationToken =
                default)
    {
        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        var root =
            OmsiRootPath ??
            throw new InvalidOperationException(
                "Instalação OMSI não selecionada.");

        if (_pendingTransforms.Count > 0)
        {
            throw new InvalidOperationException(
                "savePendingBeforeTileCreation");
        }

        if (
            snapshot.Map.Tiles.Any(
                tile =>
                    tile.X == tileX &&
                    tile.Y == tileY))
        {
            throw new InvalidDataException(
                "mapTileCoordinateAlreadyExists");
        }

        var templateRoot =
            Path.Combine(
                root,
                "template");

        var templateDirectory =
            Path.Combine(
                templateRoot,
                "NewMap");

        if (
            !Directory.Exists(
                templateDirectory) &&
            Directory.Exists(
                templateRoot))
        {
            templateDirectory =
                Directory
                    .EnumerateDirectories(
                        templateRoot)
                    .FirstOrDefault(
                        candidate =>
                        {
                            var name =
                                Path.GetFileName(
                                    candidate);

                            return
                                string.Equals(
                                    name,
                                    "NewMap",
                                    StringComparison
                                        .OrdinalIgnoreCase) ||
                                string.Equals(
                                    name,
                                    "New Map",
                                    StringComparison
                                        .OrdinalIgnoreCase);
                        })
                ?? templateDirectory;
        }

        if (!Directory.Exists(
                templateDirectory))
        {
            throw new DirectoryNotFoundException(
                templateDirectory);
        }

        var templateMap =
            await OmsiMapCatalog
                .OpenMapAsync(
                    templateDirectory,
                    cancellationToken)
                .ConfigureAwait(false);

        var templateTile =
            OmsiTileRegionSelector
                .FindInitialTile(
                    templateMap.Tiles)
            ?? throw new InvalidDataException(
                "newMapTemplateHasNoTile");

        if (
            !OmsiMapPathResolver
                .TryResolveTilePath(
                    templateDirectory,
                    templateTile
                        .RelativeMapPath,
                    out var sourceMapPath) ||
            !File.Exists(
                sourceMapPath) ||
            !File.Exists(
                sourceMapPath +
                ".terrain"))
        {
            throw new InvalidDataException(
                "newMapTemplateTileInvalid");
        }

        var sourceFileName =
            Path.GetFileName(
                sourceMapPath);

        var sourceDirectory =
            Path.GetDirectoryName(
                sourceMapPath)
            ?? templateDirectory;

        var targetRelativeMapPath =
            $"tile_{tileX}_{tileY}.map";

        var targetMapPath =
            Path.Combine(
                snapshot.Map
                    .DirectoryPath,
                targetRelativeMapPath);

        var templateFiles =
            Directory
                .EnumerateFiles(
                    sourceDirectory,
                    sourceFileName +
                    "*",
                    SearchOption
                        .TopDirectoryOnly)
                .Where(
                    source =>
                        !source.EndsWith(
                            ".prt",
                            StringComparison
                                .OrdinalIgnoreCase))
                .OrderBy(
                    source =>
                        source,
                    StringComparer
                        .OrdinalIgnoreCase)
                .ToArray();

        if (
            !templateFiles.Any(
                source =>
                    string.Equals(
                        source,
                        sourceMapPath,
                        StringComparison
                            .OrdinalIgnoreCase)))
        {
            throw new InvalidDataException(
                "newMapTemplateTileInvalid");
        }

        var destinationFiles =
            templateFiles
                .Select(
                    source =>
                    {
                        var sourceName =
                            Path.GetFileName(
                                source);

                        var suffix =
                            sourceName[
                                sourceFileName
                                    .Length..];

                        return
                            (
                                Source:
                                    source,
                                Destination:
                                    targetMapPath +
                                    suffix
                            );
                    })
                .ToArray();

        if (
            destinationFiles.Any(
                item =>
                    File.Exists(
                        item.Destination) ||
                    Directory.Exists(
                        item.Destination)))
        {
            throw new IOException(
                "mapTileFileAlreadyExists");
        }

        var globalDocument =
            await OmsiConfigParser
                .ParseFileAsync(
                    snapshot.Map
                        .GlobalConfigPath,
                    cancellationToken)
                .ConfigureAwait(false);

        var globalBytes =
            OmsiGlobalTileCatalogEditor
                .AppendTile(
                    globalDocument,
                    new OmsiTileReference(
                        tileX,
                        tileY,
                        targetRelativeMapPath));

        var timestamp =
            DateTimeOffset.UtcNow
                .ToString(
                    "yyyyMMdd-HHmmssfff'Z'",
                    CultureInfo
                        .InvariantCulture);

        var backupRoot =
            Path.Combine(
                snapshot.Map
                    .DirectoryPath,
                ".mapstudio-backups",
                timestamp +
                $"-create-tile-{tileX}-{tileY}-" +
                Guid.NewGuid()
                    .ToString("N"));

        var backupGlobal =
            Path.Combine(
                backupRoot,
                "global.cfg");

        var createdFiles =
            new List<string>(
                destinationFiles.Length);

        var globalChanged =
            false;

        try
        {
            foreach (
                var item in
                    destinationFiles)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                File.Copy(
                    item.Source,
                    item.Destination,
                    overwrite:
                        false);

                createdFiles.Add(
                    item.Destination);
            }

            await SafeFileTransaction
                .WriteAllAsync(
                    [
                        new PendingFileWrite(
                            snapshot.Map
                                .GlobalConfigPath,
                            backupGlobal,
                            globalBytes)
                    ],
                    cancellationToken)
                .ConfigureAwait(false);

            globalChanged =
                true;

            var updatedMap =
                await OmsiMapCatalog
                    .OpenMapAsync(
                        snapshot.Map
                            .DirectoryPath,
                        cancellationToken)
                    .ConfigureAwait(false);

            var createdTile =
                updatedMap.Tiles
                    .FirstOrDefault(
                        tile =>
                            tile.X == tileX &&
                            tile.Y == tileY)
                ?? throw new InvalidDataException(
                    "createdMapTileNotFound");

            var wasFullMap =
                snapshot.Tiles.Count >=
                snapshot.Map.Tiles.Count;

            var selectedTiles =
                wasFullMap
                    ? updatedMap.Tiles
                    : OmsiTileRegionSelector
                        .Select(
                            updatedMap.Tiles,
                            tileX,
                            tileY,
                            radius:
                                1);

            var loaded =
                await LoadSnapshotAsync(
                        updatedMap,
                        createdTile,
                        selectedTiles,
                        cancellationToken)
                    .ConfigureAwait(false);

            CurrentMap =
                loaded;

            _pendingTransforms
                .Clear();

            LastBackupDirectory =
                backupRoot;

            Maps =
                await new OmsiMapCatalog()
                    .DiscoverAsync(
                        root,
                        cancellationToken)
                    .ConfigureAwait(false);

            return new NativeTileCreateResult(
                loaded,
                createdTile,
                backupRoot,
                createdFiles
                    .ToArray());
        }
        catch
        {
            if (
                globalChanged &&
                File.Exists(
                    backupGlobal))
            {
                try
                {
                    File.Copy(
                        backupGlobal,
                        snapshot.Map
                            .GlobalConfigPath,
                        overwrite:
                            true);
                }
                catch
                {
                }
            }

            foreach (
                var path in
                    createdFiles
                        .AsEnumerable()
                        .Reverse())
            {
                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
                catch
                {
                }
            }

            throw;
        }
    }

    public async Task<NativeCoordinateMapCreateResult>
        CreateCoordinateMapAsync(
            string directoryName,
            string displayName,
            double latitude,
            double longitude,
            CancellationToken cancellationToken =
                default)
    {
        var root =
            OmsiRootPath ??
            throw new InvalidOperationException(
                "Selecione primeiro a instalação do OMSI 2.");

        directoryName =
            directoryName.Trim();

        displayName =
            displayName.Trim();

        if (
            string.IsNullOrWhiteSpace(
                directoryName) ||
            string.IsNullOrWhiteSpace(
                displayName) ||
            directoryName.Length > 80 ||
            displayName.Length > 120 ||
            latitude is < -90 or > 90 ||
            longitude is < -180 or > 180 ||
            directoryName is "." or ".." ||
            !string.Equals(
                Path.GetFileName(
                    directoryName),
                directoryName,
                StringComparison.Ordinal) ||
            directoryName.IndexOfAny(
                Path.GetInvalidFileNameChars()) >=
            0)
        {
            throw new InvalidDataException(
                "invalidCoordinateMapRequest");
        }

        var templateRoot =
            Path.Combine(
                root,
                "template");

        var templateDirectory =
            Path.Combine(
                templateRoot,
                "NewMap");

        if (
            !Directory.Exists(
                templateDirectory) &&
            Directory.Exists(
                templateRoot))
        {
            templateDirectory =
                Directory
                    .EnumerateDirectories(
                        templateRoot)
                    .FirstOrDefault(
                        candidate =>
                        {
                            var name =
                                Path.GetFileName(
                                    candidate);

                            return
                                string.Equals(
                                    name,
                                    "NewMap",
                                    StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(
                                    name,
                                    "New Map",
                                    StringComparison.OrdinalIgnoreCase);
                        })
                ?? templateDirectory;
        }

        if (!Directory.Exists(
                templateDirectory))
        {
            throw new DirectoryNotFoundException(
                templateDirectory);
        }

        var mapsRoot =
            Path.GetFullPath(
                Path.Combine(
                    root,
                    "maps"));

        var targetDirectory =
            Path.GetFullPath(
                Path.Combine(
                    mapsRoot,
                    directoryName));

        var requiredPrefix =
            mapsRoot
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;

        if (
            !targetDirectory.StartsWith(
                requiredPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "invalidCoordinateMapRequest");
        }

        if (
            Directory.Exists(
                targetDirectory) ||
            File.Exists(
                targetDirectory))
        {
            throw new IOException(
                "coordinateMapAlreadyExists");
        }

        try
        {
            Directory.CreateDirectory(
                targetDirectory);

            foreach (
                var sourceDirectory in
                    Directory.EnumerateDirectories(
                        templateDirectory,
                        "*",
                        SearchOption.AllDirectories))
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                var relative =
                    Path.GetRelativePath(
                        templateDirectory,
                        sourceDirectory);

                if (!IsSafeRelativePath(
                        relative))
                {
                    throw new InvalidDataException(
                        "invalidTemplatePath");
                }

                Directory.CreateDirectory(
                    Path.Combine(
                        targetDirectory,
                        relative));
            }

            foreach (
                var sourceFile in
                    Directory.EnumerateFiles(
                        templateDirectory,
                        "*",
                        SearchOption.AllDirectories))
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                var relative =
                    Path.GetRelativePath(
                        templateDirectory,
                        sourceFile);

                if (!IsSafeRelativePath(
                        relative))
                {
                    throw new InvalidDataException(
                        "invalidTemplatePath");
                }

                var destination =
                    Path.Combine(
                        targetDirectory,
                        relative);

                Directory.CreateDirectory(
                    Path.GetDirectoryName(
                        destination)!);

                File.Copy(
                    sourceFile,
                    destination,
                    overwrite: false);
            }

            var globalConfigPath =
                Path.Combine(
                    targetDirectory,
                    "global.cfg");

            if (!File.Exists(
                    globalConfigPath))
            {
                throw new InvalidDataException(
                    "newMapTemplateInvalid");
            }

            var globalDocument =
                await OmsiConfigParser
                    .ParseFileAsync(
                        globalConfigPath,
                        cancellationToken)
                    .ConfigureAwait(false);

            var lines =
                globalDocument
                    .Lines
                    .ToList();

            SetSimpleConfigSectionValue(
                lines,
                "name",
                displayName);

            SetSimpleConfigSectionValue(
                lines,
                "friendlyname",
                displayName);

            var patchedGlobal =
                new OmsiConfigDocument(
                    lines,
                    Array.Empty<
                        OmsiConfigSection>(),
                    globalDocument.NewLine,
                    globalDocument
                        .HasTrailingNewLine,
                    globalDocument
                        .TextEncoding,
                    globalDocument
                        .HasByteOrderMark);

            await File.WriteAllBytesAsync(
                    globalConfigPath,
                    patchedGlobal.ToBytes(),
                    cancellationToken)
                .ConfigureAwait(false);

            var map =
                await OmsiMapCatalog
                    .OpenMapAsync(
                        targetDirectory,
                        cancellationToken)
                    .ConfigureAwait(false);

            var initialTile =
                OmsiTileRegionSelector
                    .FindInitialTile(
                        map.Tiles);

            var anchorTileX =
                initialTile?.X ??
                0;

            var anchorTileY =
                initialTile?.Y ??
                0;

            await SaveMapGeoreferenceFileAsync(
                    map,
                    new NativeMapGeoreference(
                        latitude,
                        longitude,
                        anchorTileX,
                        anchorTileY,
                        150.0,
                        150.0,
                        18,
                        "hybrid",
                        "Google Maps"),
                    cancellationToken)
                .ConfigureAwait(false);

            Maps =
                await new OmsiMapCatalog()
                    .DiscoverAsync(
                        root,
                        cancellationToken)
                    .ConfigureAwait(false);

            var snapshot =
                await OpenMapAsync(
                        targetDirectory,
                        loadFullMap: true,
                        cancellationToken)
                    .ConfigureAwait(false);

            return new NativeCoordinateMapCreateResult(
                snapshot,
                targetDirectory,
                latitude,
                longitude);
        }
        catch
        {
            try
            {
                if (Directory.Exists(
                        targetDirectory))
                {
                    Directory.Delete(
                        targetDirectory,
                        recursive: true);
                }
            }
            catch
            {
            }

            throw;
        }
    }

    public async Task<string>
        SaveMapGeoreferenceAsync(
            NativeMapGeoreference georeference,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentNullException.ThrowIfNull(
            georeference);

        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        ValidateGeoreference(
            georeference);

        return await SaveMapGeoreferenceFileAsync(
                snapshot.Map,
                georeference,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<NativeMapGeoreference?>
        LoadMapGeoreferenceAsync(
            CancellationToken cancellationToken =
                default)
    {
        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        var path =
            Path.Combine(
                snapshot.Map.DirectoryPath,
                ".mapstudio",
                "georeference.json");

        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream =
            File.OpenRead(path);

        return await JsonSerializer
            .DeserializeAsync<
                NativeMapGeoreference>(
                    stream,
                    cancellationToken:
                        cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<string>
        SaveMapGeoreferenceFileAsync(
            OmsiMapDescriptor map,
            NativeMapGeoreference georeference,
            CancellationToken cancellationToken)
    {
        ValidateGeoreference(
            georeference);

        var metadataDirectory =
            Path.Combine(
                map.DirectoryPath,
                ".mapstudio");

        Directory.CreateDirectory(
            metadataDirectory);

        var path =
            Path.Combine(
                metadataDirectory,
                "georeference.json");

        var payload =
            JsonSerializer.Serialize(
                new
                {
                    version = 1,
                    provider =
                        georeference.Provider,
                    latitude =
                        georeference.Latitude,
                    longitude =
                        georeference.Longitude,
                    anchorTileX =
                        georeference.AnchorTileX,
                    anchorTileY =
                        georeference.AnchorTileY,
                    anchorX =
                        georeference.AnchorX,
                    anchorY =
                        georeference.AnchorY,
                    zoom =
                        georeference.Zoom,
                    mapType =
                        georeference.MapType,
                    savedAtUtc =
                        DateTimeOffset.UtcNow
                },
                new JsonSerializerOptions(
                    JsonSerializerDefaults.Web)
                {
                    WriteIndented =
                        true
                });

        var tempPath =
            path +
            "." +
            Guid.NewGuid()
                .ToString("N") +
            ".tmp";

        await File.WriteAllTextAsync(
                tempPath,
                payload,
                cancellationToken)
            .ConfigureAwait(false);

        File.Move(
            tempPath,
            path,
            overwrite: true);

        return path;
    }

    private static void ValidateGeoreference(
        NativeMapGeoreference value)
    {
        if (
            value.Latitude is
                < -90 or > 90 ||
            value.Longitude is
                < -180 or > 180 ||
            value.AnchorX is
                < 0 or > 300 ||
            value.AnchorY is
                < 0 or > 300 ||
            value.Zoom is
                < 0 or > 22 ||
            value.MapType.Trim()
                .ToLowerInvariant() is not
                (
                    "roadmap" or
                    "satellite" or
                    "hybrid" or
                    "terrain"
                ))
        {
            throw new InvalidDataException(
                "invalidMapGeoreference");
        }
    }

    private static void SetSimpleConfigSectionValue(
        List<string> lines,
        string keyword,
        string value)
    {
        var marker =
            "[" +
            keyword +
            "]";

        for (
            var index = 0;
            index < lines.Count;
            index++)
        {
            if (!string.Equals(
                    lines[index].Trim(),
                    marker,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var valueIndex =
                index + 1;

            while (
                valueIndex <
                    lines.Count &&
                string.IsNullOrWhiteSpace(
                    lines[valueIndex]))
            {
                valueIndex++;
            }

            if (
                valueIndex <
                    lines.Count &&
                !lines[valueIndex]
                    .TrimStart()
                    .StartsWith(
                        "[",
                        StringComparison.Ordinal))
            {
                lines[valueIndex] =
                    value;
                return;
            }

            lines.Insert(
                index + 1,
                value);

            return;
        }

        if (
            lines.Count > 0 &&
            lines[^1].Length !=
                0)
        {
            lines.Add(
                string.Empty);
        }

        lines.Add(
            marker);

        lines.Add(
            value);
    }

    public async Task<NativeAssetReplacementResult>
        ReplaceMapAssetPathAsync(
            PickingKind kind,
            string oldPath,
            string newPath,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            oldPath);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            newPath);

        if (
            string.Equals(
                oldPath,
                newPath,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "replacementPathUnchanged");
        }

        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        var root =
            OmsiRootPath ??
            throw new InvalidOperationException(
                "Instalação OMSI não selecionada.");

        if (_pendingTransforms.Count > 0)
        {
            throw new InvalidOperationException(
                "savePendingBeforeDependencyReplacement");
        }

        var replaceObjects =
            kind ==
            PickingKind.Object;

        var replaceSplines =
            kind ==
            PickingKind.Spline;

        if (
            !replaceObjects &&
            !replaceSplines)
        {
            throw new InvalidDataException(
                "replacementKindUnsupported");
        }

        var replacementExists =
            replaceObjects
                ? OmsiSceneryObjectPathResolver
                    .TryResolve(
                        root,
                        newPath,
                        out var replacementPath) &&
                  File.Exists(
                      replacementPath)
                : OmsiSplinePathResolver
                    .TryResolve(
                        root,
                        newPath,
                        out replacementPath) &&
                  File.Exists(
                      replacementPath);

        if (!replacementExists)
        {
            throw new FileNotFoundException(
                "replacementAssetMissing",
                newPath);
        }

        var timestamp =
            DateTimeOffset.UtcNow
                .ToString(
                    "yyyyMMdd-HHmmssfff'Z'",
                    CultureInfo.InvariantCulture);

        var backupRoot =
            Path.Combine(
                snapshot.Map.DirectoryPath,
                ".mapstudio-backups",
                timestamp +
                "-replace-" +
                Guid.NewGuid()
                    .ToString("N"));

        var writes =
            new List<PendingFileWrite>();

        var replacements = 0;

        foreach (
            var tile in
                snapshot.Map.Tiles)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            if (
                !OmsiMapPathResolver
                    .TryResolveTilePath(
                        snapshot.Map.DirectoryPath,
                        tile.RelativeMapPath,
                        out var tilePath) ||
                !File.Exists(tilePath))
            {
                continue;
            }

            var document =
                await OmsiConfigParser
                    .ParseFileAsync(
                        tilePath,
                        cancellationToken)
                    .ConfigureAwait(false);

            var result =
                OmsiTileAssetPathRewriter
                    .Replace(
                        document,
                        oldPath,
                        newPath,
                        replaceObjects,
                        replaceSplines);

            var changed =
                replaceObjects
                    ? result.ObjectReplacements
                    : result.SplineReplacements;

            if (changed <= 0)
            {
                continue;
            }

            var relativePath =
                Path.GetRelativePath(
                    snapshot.Map.DirectoryPath,
                    tilePath);

            if (!IsSafeRelativePath(
                    relativePath))
            {
                throw new InvalidDataException(
                    "replacementTilePathInvalid");
            }

            writes.Add(
                new PendingFileWrite(
                    tilePath,
                    Path.Combine(
                        backupRoot,
                        relativePath),
                    result.Bytes));

            replacements +=
                changed;
        }

        if (writes.Count > 0)
        {
            await SafeFileTransaction
                .WriteAllAsync(
                    writes,
                    cancellationToken)
                .ConfigureAwait(false);

            await ReloadCurrentLoadedTilesAsync(
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return new NativeAssetReplacementResult(
            CurrentMap!,
            replacements,
            writes.Count,
            writes.Count > 0
                ? backupRoot
                : string.Empty);
    }

    public async Task<NativeBackupRestoreResult>
        RestoreMapStudioBackupAsync(
            string backupDirectory,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            backupDirectory);

        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        if (_pendingTransforms.Count > 0)
        {
            throw new InvalidOperationException(
                "savePendingBeforeBackupRestore");
        }

        var backupsRoot =
            Path.GetFullPath(
                Path.Combine(
                    snapshot.Map.DirectoryPath,
                    ".mapstudio-backups"));

        var sourceRoot =
            Path.GetFullPath(
                backupDirectory);

        var relativeSource =
            Path.GetRelativePath(
                backupsRoot,
                sourceRoot);

        if (
            !IsSafeRelativePath(
                relativeSource) ||
            !Directory.Exists(
                sourceRoot))
        {
            throw new InvalidDataException(
                "invalidBackupPath");
        }

        var sourceFiles =
            Directory
                .EnumerateFiles(
                    sourceRoot,
                    "*",
                    SearchOption
                        .AllDirectories)
                .ToArray();

        if (sourceFiles.Length == 0)
        {
            throw new InvalidDataException(
                "emptyBackup");
        }

        var timestamp =
            DateTimeOffset.UtcNow
                .ToString(
                    "yyyyMMdd-HHmmssfff'Z'",
                    CultureInfo.InvariantCulture);

        var rollbackRoot =
            Path.Combine(
                backupsRoot,
                timestamp +
                "-restore-" +
                Guid.NewGuid()
                    .ToString("N"));

        var writes =
            new List<
                PendingFileWrite>(
                    sourceFiles.Length);

        foreach (
            var sourceFile in
                sourceFiles)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            var relativePath =
                Path.GetRelativePath(
                    sourceRoot,
                    sourceFile);

            if (!IsSafeRelativePath(
                    relativePath))
            {
                throw new InvalidDataException(
                    "invalidBackupEntry");
            }

            var target =
                Path.GetFullPath(
                    Path.Combine(
                        snapshot.Map.DirectoryPath,
                        relativePath));

            var relativeTarget =
                Path.GetRelativePath(
                    snapshot.Map.DirectoryPath,
                    target);

            if (
                !IsSafeRelativePath(
                    relativeTarget) ||
                !File.Exists(
                    target))
            {
                throw new InvalidDataException(
                    "invalidBackupTarget");
            }

            writes.Add(
                new PendingFileWrite(
                    target,
                    Path.Combine(
                        rollbackRoot,
                        relativePath),
                    await File
                        .ReadAllBytesAsync(
                            sourceFile,
                            cancellationToken)
                        .ConfigureAwait(false)));
        }

        await SafeFileTransaction
            .WriteAllAsync(
                writes,
                cancellationToken)
            .ConfigureAwait(false);

        await ReloadCurrentLoadedTilesAsync(
                cancellationToken)
            .ConfigureAwait(false);

        return new NativeBackupRestoreResult(
            CurrentMap!,
            writes.Count,
            sourceRoot,
            rollbackRoot);
    }

    private async Task ReloadCurrentLoadedTilesAsync(
        CancellationToken cancellationToken)
    {
        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        var loaded =
            await LoadSnapshotAsync(
                    snapshot.Map,
                    snapshot.ActiveTile,
                    snapshot.Tiles
                        .Select(
                            tile =>
                                tile.Reference)
                        .ToArray(),
                    cancellationToken)
                .ConfigureAwait(false);

        CurrentMap =
            loaded;

        _pendingTransforms.Clear();
    }

    private static bool IsSafeRelativePath(
        string relativePath) =>
        !Path.IsPathRooted(
            relativePath) &&
        !relativePath.Equals(
            "..",
            StringComparison.Ordinal) &&
        !relativePath.StartsWith(
            ".." +
            Path.DirectorySeparatorChar,
            StringComparison.Ordinal) &&
        !relativePath.StartsWith(
            ".." +
            Path.AltDirectorySeparatorChar,
            StringComparison.Ordinal);

    public async Task<NativeMapSnapshot>
        InsertSceneryObjectMultiBatchAsync(
            IReadOnlyList<
                NativeSceneryPlacementBatchGroup>
                groups,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentNullException.ThrowIfNull(
            groups);

        var totalPlacements =
            groups.Sum(
                group =>
                    group.Placements.Count);

        if (
            groups.Count == 0 ||
            groups.Count > 16 ||
            totalPlacements == 0 ||
            totalPlacements > 512)
        {
            throw new InvalidDataException(
                "invalidSceneryMultiBatch");
        }

        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        var root =
            OmsiRootPath ??
            throw new InvalidOperationException(
                "Instalação OMSI não selecionada.");

        if (_pendingTransforms.Count > 0)
        {
            throw new InvalidOperationException(
                "savePendingBeforeInsertion");
        }

        var mapTiles =
            snapshot.Map.Tiles
                .ToDictionary(
                    tile =>
                        (
                            tile.X,
                            tile.Y
                        ));

        foreach (
            var group in groups)
        {
            ArgumentException
                .ThrowIfNullOrWhiteSpace(
                    group.SceneryObjectPath);

            if (
                group.Placements.Count ==
                    0 ||
                group.Placements.Count >
                    256 ||
                group.Placements.Any(
                    request =>
                        !string.Equals(
                            request
                                .SceneryObjectPath,
                            group
                                .SceneryObjectPath,
                            StringComparison
                                .OrdinalIgnoreCase)))
            {
                throw new InvalidDataException(
                    "invalidSceneryMultiBatchGroup");
            }

            if (
                !OmsiSceneryObjectPathResolver
                    .TryResolve(
                        root,
                        group.SceneryObjectPath,
                        out var fullPath) ||
                !File.Exists(fullPath))
            {
                throw new FileNotFoundException(
                    "sceneryAssetMissing",
                    group.SceneryObjectPath);
            }

            foreach (
                var request in
                    group.Placements)
            {
                if (
                    !mapTiles.ContainsKey(
                        (
                            request.Tile.X,
                            request.Tile.Y
                        )))
                {
                    throw new InvalidDataException(
                        "placementTileUnknown");
                }
            }
        }

        var loadedByPath =
            snapshot.Tiles
                .ToDictionary(
                    tile =>
                        tile.Reference
                            .RelativeMapPath,
                    tile =>
                        tile.Content,
                    StringComparer
                        .OrdinalIgnoreCase);

        var contents =
            new List<
                OmsiTileContent>(
                    snapshot.Map.Tiles.Count);

        foreach (
            var tile in
                snapshot.Map.Tiles)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            if (
                loadedByPath.TryGetValue(
                    tile.RelativeMapPath,
                    out var loaded))
            {
                contents.Add(
                    loaded);

                continue;
            }

            if (
                !OmsiMapPathResolver
                    .TryResolveTilePath(
                        snapshot.Map
                            .DirectoryPath,
                        tile.RelativeMapPath,
                        out var path) ||
                !File.Exists(path))
            {
                continue;
            }

            contents.Add(
                await _tileReader
                    .ReadContentAsync(
                        path,
                        cancellationToken)
                    .ConfigureAwait(false));
        }

        var templates =
            new Dictionary<
                string,
                (
                    string HeaderValue,
                    IReadOnlyList<string>
                        ExtraValues
                )>(
                    StringComparer.OrdinalIgnoreCase);

        var maxUsedId =
            contents
                .SelectMany(
                    content =>
                        content.Objects)
                .Select(
                    item =>
                        item.ObjectId)
                .DefaultIfEmpty(0)
                .Max();

        foreach (
            var group in groups)
        {
            if (templates.ContainsKey(
                    group.SceneryObjectPath))
            {
                continue;
            }

            var analysis =
                OmsiObjectInsertionAnalyzer
                    .Analyze(
                        contents,
                        group
                            .SceneryObjectPath);

            maxUsedId =
                Math.Max(
                    maxUsedId,
                    analysis.MaxUsedId);

            var headerValue =
                analysis
                    .MatchingObjectTemplate
                    ?.HeaderValue ??
                "0";

            IReadOnlyList<string>
                extraValues =
                    analysis
                        .MatchingObjectTemplate
                        ?.ExtraValues ??
                    Array.Empty<string>();

            if (
                analysis
                    .MatchingObjectTemplate is
                    null &&
                OmsiSceneryObjectPathResolver
                    .TryResolve(
                        root,
                        group
                            .SceneryObjectPath,
                        out var fullPath))
            {
                var metadata =
                    await new OmsiSceneryObjectReader()
                        .ReadMetadataAsync(
                            fullPath,
                            cancellationToken)
                        .ConfigureAwait(false);

                if (
                    metadata.Tree is
                        { } tree)
                {
                    var height =
                        (
                            tree.MinimumHeight +
                            tree.MaximumHeight
                        ) /
                        2.0;

                    var aspect =
                        (
                            tree.MinimumAspect +
                            tree.MaximumAspect
                        ) /
                        2.0;

                    extraValues =
                        [
                            "4",
                            tree.TextureName,
                            height.ToString(
                                "G17",
                                CultureInfo
                                    .InvariantCulture),
                            aspect.ToString(
                                "G17",
                                CultureInfo
                                    .InvariantCulture)
                        ];
                }
            }

            templates[
                group.SceneryObjectPath] =
                (
                    headerValue,
                    extraValues
                );
        }

        if (
            maxUsedId >
            int.MaxValue -
                totalPlacements)
        {
            throw new InvalidDataException(
                "objectIdExhausted");
        }

        var prepared =
            new List<
                (
                    string SceneryObjectPath,
                    NativeSceneryPlacementRequest
                        Request,
                    int ObjectId
                )>(
                    totalPlacements);

        var nextId =
            maxUsedId + 1;

        foreach (
            var group in groups)
        {
            foreach (
                var request in
                    group.Placements)
            {
                prepared.Add(
                    (
                        group.SceneryObjectPath,
                        request,
                        nextId++
                    ));
            }
        }

        var timestamp =
            DateTimeOffset.UtcNow
                .ToString(
                    "yyyyMMdd-HHmmssfff'Z'",
                    CultureInfo.InvariantCulture);

        var backupRoot =
            Path.Combine(
                snapshot.Map
                    .DirectoryPath,
                ".mapstudio-backups",
                timestamp +
                "-native-construction-set-" +
                Guid.NewGuid()
                    .ToString("N"));

        var writes =
            new List<PendingFileWrite>();

        var editedLoadedTiles =
            new Dictionary<
                string,
                OmsiTileReference>(
                    StringComparer.OrdinalIgnoreCase);

        foreach (
            var tileGroup in
                prepared.GroupBy(
                    item =>
                        (
                            item.Request.Tile.X,
                            item.Request.Tile.Y
                        )))
        {
            var tile =
                mapTiles[
                    tileGroup.Key];

            if (
                !OmsiMapPathResolver
                    .TryResolveTilePath(
                        snapshot.Map
                            .DirectoryPath,
                        tile.RelativeMapPath,
                        out var targetPath) ||
                !File.Exists(targetPath))
            {
                throw new InvalidDataException(
                    "placementTilePathInvalid");
            }

            var document =
                await OmsiConfigParser
                    .ParseFileAsync(
                        targetPath,
                        cancellationToken)
                    .ConfigureAwait(false);

            var items =
                tileGroup.ToArray();

            var insertion =
                OmsiTileObjectInserter
                    .AppendMany(
                        document,
                        items
                            .Select(
                                item =>
                                {
                                    var template =
                                        templates[
                                            item
                                                .SceneryObjectPath];

                                    return new OmsiNewPlacedObject(
                                        template
                                            .HeaderValue,
                                        item
                                            .SceneryObjectPath,
                                        item.ObjectId,
                                        item.Request.X,
                                        item.Request.Y,
                                        item.Request.Z,
                                        item.Request
                                            .Rotation,
                                        item.Request.Pitch,
                                        item.Request.Bank,
                                        template
                                            .ExtraValues);
                                })
                            .ToArray());

            var relativePath =
                Path.GetRelativePath(
                    snapshot.Map
                        .DirectoryPath,
                    targetPath);

            if (!IsSafeRelativePath(
                    relativePath))
            {
                throw new InvalidDataException(
                    "placementTilePathInvalid");
            }

            writes.Add(
                new PendingFileWrite(
                    targetPath,
                    Path.Combine(
                        backupRoot,
                        relativePath),
                    insertion.Bytes));

            if (
                snapshot.Tiles.Any(
                    loaded =>
                        loaded.Reference.X ==
                            tile.X &&
                        loaded.Reference.Y ==
                            tile.Y))
            {
                editedLoadedTiles[
                    targetPath] =
                    tile;
            }
        }

        await SafeFileTransaction
            .WriteAllAsync(
                writes,
                cancellationToken)
            .ConfigureAwait(false);

        LastBackupDirectory =
            backupRoot;

        var refreshedByPath =
            new Dictionary<
                string,
                OmsiTileContent>(
                    StringComparer.OrdinalIgnoreCase);

        foreach (
            var pair in
                editedLoadedTiles)
        {
            refreshedByPath[
                pair.Value.RelativeMapPath] =
                await _tileReader
                    .ReadContentAsync(
                        pair.Key,
                        cancellationToken)
                    .ConfigureAwait(false);
        }

        CurrentMap =
            snapshot with
            {
                Tiles =
                    snapshot.Tiles
                        .Select(
                            tile =>
                                refreshedByPath
                                    .TryGetValue(
                                        tile.Reference
                                            .RelativeMapPath,
                                        out var refreshed)
                                    ? new NativeLoadedTile(
                                        tile.Reference,
                                        refreshed)
                                    : tile)
                        .ToArray()
            };

        return CurrentMap;
    }

    public async Task<NativeMapSnapshot>
        InsertSceneryObjectBatchAsync(
            IReadOnlyList<
                NativeSceneryPlacementRequest>
                requests,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentNullException.ThrowIfNull(
            requests);

        if (
            requests.Count == 0 ||
            requests.Count > 256)
        {
            throw new InvalidDataException(
                "invalidSceneryBatch");
        }

        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        var root =
            OmsiRootPath ??
            throw new InvalidOperationException(
                "Instalação OMSI não selecionada.");

        if (_pendingTransforms.Count > 0)
        {
            throw new InvalidOperationException(
                "savePendingBeforeInsertion");
        }

        var sceneryPath =
            requests[0]
                .SceneryObjectPath;

        if (
            requests.Any(
                request =>
                    !string.Equals(
                        request
                            .SceneryObjectPath,
                        sceneryPath,
                        StringComparison
                            .OrdinalIgnoreCase)))
        {
            throw new InvalidDataException(
                "mixedSceneryBatchUnsupported");
        }

        var mapTiles =
            snapshot.Map.Tiles
                .ToDictionary(
                    tile =>
                        (
                            tile.X,
                            tile.Y
                        ));

        foreach (
            var request in requests)
        {
            if (
                !mapTiles.ContainsKey(
                    (
                        request.Tile.X,
                        request.Tile.Y
                    )))
            {
                throw new InvalidDataException(
                    "placementTileUnknown");
            }
        }

        var loadedByPath =
            snapshot.Tiles
                .ToDictionary(
                    tile =>
                        tile.Reference
                            .RelativeMapPath,
                    tile =>
                        tile.Content,
                    StringComparer
                        .OrdinalIgnoreCase);

        var contents =
            new List<
                OmsiTileContent>(
                    snapshot.Map.Tiles.Count);

        foreach (
            var tile in
                snapshot.Map.Tiles)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            if (
                loadedByPath.TryGetValue(
                    tile.RelativeMapPath,
                    out var loaded))
            {
                contents.Add(
                    loaded);

                continue;
            }

            if (
                !OmsiMapPathResolver
                    .TryResolveTilePath(
                        snapshot.Map
                            .DirectoryPath,
                        tile.RelativeMapPath,
                        out var path) ||
                !File.Exists(path))
            {
                continue;
            }

            contents.Add(
                await _tileReader
                    .ReadContentAsync(
                        path,
                        cancellationToken)
                    .ConfigureAwait(false));
        }

        var analysis =
            OmsiObjectInsertionAnalyzer
                .Analyze(
                    contents,
                    sceneryPath);

        var template =
            analysis
                .MatchingObjectTemplate;

        var headerValue =
            template?.HeaderValue ??
            "0";

        IReadOnlyList<string>
            extraValues =
                template?.ExtraValues ??
                Array.Empty<string>();

        if (
            template is null &&
            OmsiSceneryObjectPathResolver
                .TryResolve(
                    root,
                    sceneryPath,
                    out var fullScoPath) &&
            File.Exists(fullScoPath))
        {
            var metadata =
                await new OmsiSceneryObjectReader()
                    .ReadMetadataAsync(
                        fullScoPath,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (
                metadata.Tree is
                    { } tree)
            {
                var height =
                    (
                        tree.MinimumHeight +
                        tree.MaximumHeight
                    ) /
                    2.0;

                var aspect =
                    (
                        tree.MinimumAspect +
                        tree.MaximumAspect
                    ) /
                    2.0;

                extraValues =
                    [
                        "4",
                        tree.TextureName,
                        height.ToString(
                            "G17",
                            CultureInfo
                                .InvariantCulture),
                        aspect.ToString(
                            "G17",
                            CultureInfo
                                .InvariantCulture)
                    ];
            }
        }

        if (
            analysis.MaxUsedId >
            int.MaxValue -
                requests.Count)
        {
            throw new InvalidDataException(
                "objectIdExhausted");
        }

        var prepared =
            requests
                .Select(
                    (request, index) =>
                        (
                            Request: request,
                            ObjectId:
                                analysis.MaxUsedId +
                                index +
                                1
                        ))
                .ToArray();

        var timestamp =
            DateTimeOffset.UtcNow
                .ToString(
                    "yyyyMMdd-HHmmssfff'Z'",
                    CultureInfo
                        .InvariantCulture);

        var backupRoot =
            Path.Combine(
                snapshot.Map
                    .DirectoryPath,
                ".mapstudio-backups",
                timestamp +
                "-native-batch-" +
                Guid.NewGuid()
                    .ToString("N"));

        var writes =
            new List<
                PendingFileWrite>();

        var editedLoadedTiles =
            new Dictionary<
                string,
                OmsiTileReference>(
                    StringComparer
                        .OrdinalIgnoreCase);

        foreach (
            var group in prepared
                .GroupBy(
                    item =>
                        (
                            item.Request.Tile.X,
                            item.Request.Tile.Y
                        )))
        {
            var tile =
                mapTiles[
                    group.Key];

            if (
                !OmsiMapPathResolver
                    .TryResolveTilePath(
                        snapshot.Map
                            .DirectoryPath,
                        tile.RelativeMapPath,
                        out var targetPath) ||
                !File.Exists(targetPath))
            {
                throw new InvalidDataException(
                    "placementTilePathInvalid");
            }

            var document =
                await OmsiConfigParser
                    .ParseFileAsync(
                        targetPath,
                        cancellationToken)
                    .ConfigureAwait(false);

            var items =
                group.ToArray();

            var insertion =
                OmsiTileObjectInserter
                    .AppendMany(
                        document,
                        items
                            .Select(
                                item =>
                                    new OmsiNewPlacedObject(
                                        headerValue,
                                        sceneryPath,
                                        item.ObjectId,
                                        item.Request.X,
                                        item.Request.Y,
                                        item.Request.Z,
                                        item.Request
                                            .Rotation,
                                        item.Request.Pitch,
                                        item.Request.Bank,
                                        extraValues))
                            .ToArray());

            var relativePath =
                Path.GetRelativePath(
                    snapshot.Map
                        .DirectoryPath,
                    targetPath);

            if (
                relativePath.StartsWith(
                    "..",
                    StringComparison.Ordinal) ||
                Path.IsPathRooted(
                    relativePath))
            {
                throw new InvalidDataException(
                    "placementTilePathInvalid");
            }

            writes.Add(
                new PendingFileWrite(
                    targetPath,
                    Path.Combine(
                        backupRoot,
                        relativePath),
                    insertion.Bytes));

            if (
                snapshot.Tiles.Any(
                    loaded =>
                        loaded.Reference.X ==
                            tile.X &&
                        loaded.Reference.Y ==
                            tile.Y))
            {
                editedLoadedTiles[
                    targetPath] =
                    tile;
            }
        }

        await SafeFileTransaction
            .WriteAllAsync(
                writes,
                cancellationToken)
            .ConfigureAwait(false);

        LastBackupDirectory =
            backupRoot;

        var refreshedByPath =
            new Dictionary<
                string,
                OmsiTileContent>(
                    StringComparer
                        .OrdinalIgnoreCase);

        foreach (
            var pair in
                editedLoadedTiles)
        {
            refreshedByPath[
                pair.Value.RelativeMapPath] =
                await _tileReader
                    .ReadContentAsync(
                        pair.Key,
                        cancellationToken)
                    .ConfigureAwait(false);
        }

        CurrentMap =
            snapshot with
            {
                Tiles =
                    snapshot.Tiles
                        .Select(
                            tile =>
                                refreshedByPath
                                    .TryGetValue(
                                        tile.Reference
                                            .RelativeMapPath,
                                        out var refreshed)
                                    ? new NativeLoadedTile(
                                        tile.Reference,
                                        refreshed)
                                    : tile)
                        .ToArray()
            };

        return CurrentMap;
    }

    public async Task<NativeMapSnapshot>
        InsertSceneryObjectAsync(
            NativeSceneryPlacementRequest
                request,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        var root =
            OmsiRootPath ??
            throw new InvalidOperationException(
                "Instalação OMSI não selecionada.");

        if (_pendingTransforms.Count > 0)
        {
            throw new InvalidOperationException(
                "savePendingBeforeInsertion");
        }

        if (
            !snapshot.Tiles.Any(
                tile =>
                    tile.Reference.X ==
                        request.Tile.X &&
                    tile.Reference.Y ==
                        request.Tile.Y))
        {
            throw new InvalidDataException(
                "placementTileNotLoaded");
        }

        var loadedByPath =
            snapshot.Tiles
                .ToDictionary(
                    tile =>
                        tile.Reference
                            .RelativeMapPath,
                    tile =>
                        tile.Content,
                    StringComparer
                        .OrdinalIgnoreCase);

        var contents =
            new List<
                OmsiTileContent>(
                    snapshot.Map.Tiles.Count);

        foreach (
            var tile in
                snapshot.Map.Tiles)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            if (
                loadedByPath.TryGetValue(
                    tile.RelativeMapPath,
                    out var loaded))
            {
                contents.Add(
                    loaded);

                continue;
            }

            if (
                !OmsiMapPathResolver
                    .TryResolveTilePath(
                        snapshot.Map
                            .DirectoryPath,
                        tile.RelativeMapPath,
                        out var path))
            {
                continue;
            }

            contents.Add(
                await _tileReader
                    .ReadContentAsync(
                        path,
                        cancellationToken)
                    .ConfigureAwait(false));
        }

        var analysis =
            OmsiObjectInsertionAnalyzer
                .Analyze(
                    contents,
                    request
                        .SceneryObjectPath);

        var template =
            analysis
                .MatchingObjectTemplate;

        var headerValue =
            template?.HeaderValue ??
            "0";

        IReadOnlyList<string>
            extraValues =
                template?.ExtraValues ??
                Array.Empty<string>();

        if (
            template is null &&
            OmsiSceneryObjectPathResolver
                .TryResolve(
                    root,
                    request
                        .SceneryObjectPath,
                    out var fullScoPath))
        {
            var metadata =
                await new OmsiSceneryObjectReader()
                    .ReadMetadataAsync(
                        fullScoPath,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (
                metadata.Tree is
                    { } tree)
            {
                var height =
                    (
                        tree.MinimumHeight +
                        tree.MaximumHeight
                    ) /
                    2.0;

                var aspect =
                    (
                        tree.MinimumAspect +
                        tree.MaximumAspect
                    ) /
                    2.0;

                extraValues =
                    [
                        "4",
                        tree.TextureName,
                        height.ToString(
                            "G17",
                            CultureInfo
                                .InvariantCulture),
                        aspect.ToString(
                            "G17",
                            CultureInfo
                                .InvariantCulture)
                    ];
            }
        }

        if (
            !OmsiMapPathResolver
                .TryResolveTilePath(
                    snapshot.Map
                        .DirectoryPath,
                    request.Tile
                        .RelativeMapPath,
                    out var targetPath))
        {
            throw new InvalidDataException(
                "placementTilePathInvalid");
        }

        var document =
            await OmsiConfigParser
                .ParseFileAsync(
                    targetPath,
                    cancellationToken)
                .ConfigureAwait(false);

        var insertion =
            OmsiTileObjectInserter
                .Append(
                    document,
                    new OmsiNewPlacedObject(
                        headerValue,
                        request
                            .SceneryObjectPath,
                        analysis.GetNextId(),
                        request.X,
                        request.Y,
                        request.Z,
                        request.Rotation,
                        request.Pitch,
                        request.Bank,
                        extraValues));

        var backupRoot =
            Path.Combine(
                snapshot.Map.DirectoryPath,
                ".mapstudio-backups",
                DateTimeOffset.UtcNow.ToString(
                    "yyyyMMdd-HHmmssfff'Z'",
                    CultureInfo.InvariantCulture) +
                "-native-object-" +
                Guid.NewGuid().ToString("N"));

        var relativeTarget =
            Path.GetRelativePath(
                snapshot.Map.DirectoryPath,
                targetPath);

        if (!IsSafeRelativePath(relativeTarget))
        {
            throw new InvalidDataException(
                "placementTilePathInvalid");
        }

        var backupPath =
            Path.Combine(
                backupRoot,
                relativeTarget);

        await SafeFileTransaction
            .WriteAllAsync(
                [
                    new PendingFileWrite(
                        targetPath,
                        backupPath,
                        insertion.Bytes)
                ],
                cancellationToken)
            .ConfigureAwait(false);

        LastBackupDirectory =
            backupRoot;

        var refreshedContent =
            await _tileReader
                .ReadContentAsync(
                    targetPath,
                    cancellationToken)
                .ConfigureAwait(false);

        CurrentMap =
            snapshot with
            {
                Tiles =
                    snapshot.Tiles
                        .Select(
                            tile =>
                                tile.Reference.X ==
                                    request.Tile.X &&
                                tile.Reference.Y ==
                                    request.Tile.Y
                                    ? new NativeLoadedTile(
                                        tile.Reference,
                                        refreshedContent)
                                    : tile)
                        .ToArray()
            };

        return CurrentMap;
    }

    public async Task<NativeSplineInsertionResult>
        InsertSplineAsync(
            NativeSplinePlacementRequest request,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        if (_pendingTransforms.Count > 0)
        {
            throw new InvalidOperationException(
                "savePendingBeforeSplineInsertion");
        }

        var loadedByPath =
            snapshot.Tiles.ToDictionary(
                tile => tile.Reference.RelativeMapPath,
                tile => tile.Content,
                StringComparer.OrdinalIgnoreCase);

        var mapContents =
            new List<(
                OmsiTileReference Reference,
                OmsiTileContent Content)>(
                    snapshot.Map.Tiles.Count);

        foreach (var tile in snapshot.Map.Tiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (
                loadedByPath.TryGetValue(
                    tile.RelativeMapPath,
                    out var loaded))
            {
                mapContents.Add(
                    (tile, loaded));

                continue;
            }

            if (
                !OmsiMapPathResolver.TryResolveTilePath(
                    snapshot.Map.DirectoryPath,
                    tile.RelativeMapPath,
                    out var path))
            {
                continue;
            }

            mapContents.Add(
                (
                    tile,
                    await _tileReader
                        .ReadContentAsync(
                            path,
                            cancellationToken)
                        .ConfigureAwait(false)
                ));
        }

        var contents =
            mapContents
                .Select(item => item.Content)
                .ToArray();

        var maxUsedId =
            contents
                .SelectMany(
                    content =>
                        content.Objects
                            .Select(item => item.ObjectId)
                            .Concat(
                                content.Splines
                                    .Select(item => item.SplineId)))
                .DefaultIfEmpty(0)
                .Max();

        if (maxUsedId >= int.MaxValue)
        {
            throw new InvalidDataException(
                "splineIdExhausted");
        }

        var newSplineId =
            checked(maxUsedId + 1);

        var template =
            contents
                .SelectMany(content => content.Splines)
                .FirstOrDefault(
                    item =>
                        item.IsHeightSpline ==
                            request.IsHeightSpline &&
                        string.Equals(
                            item.SplinePath,
                            request.SplinePath,
                            StringComparison.OrdinalIgnoreCase))
            ?? OmsiSplinePlacementTemplateAnalyzer
                .FindNeutralTemplate(
                    contents,
                    request.IsHeightSpline);

        if (
            request.IsHeightSpline &&
            template is null)
        {
            throw new InvalidDataException(
                "splineInsertTemplateUnavailable");
        }

        var headerValue =
            template?.HeaderValue ??
            "0";

        var extraValues =
            template?.ExtraValues ??
            Array.Empty<string>();

        if (
            !OmsiMapPathResolver.TryResolveTilePath(
                snapshot.Map.DirectoryPath,
                request.Tile.RelativeMapPath,
                out var targetPath))
        {
            throw new InvalidDataException(
                "splinePlacementTilePathInvalid");
        }

        var targetDocument =
            await OmsiConfigParser
                .ParseFileAsync(
                    targetPath,
                    cancellationToken)
                .ConfigureAwait(false);

        var insertion =
            OmsiTileSplineInserter.Append(
                targetDocument,
                new OmsiNewPlacedSpline(
                    headerValue,
                    request.SplinePath,
                    newSplineId,
                    request.PreviousSplineId,
                    request.NextSplineId,
                    request.X,
                    request.Z,
                    request.Y,
                    request.Rotation,
                    request.Length,
                    request.Radius,
                    request.GradientStart,
                    request.GradientEnd,
                    request.IsHeightSpline,
                    extraValues));

        var targetBytes =
            insertion.Bytes;

        var backupRoot =
            Path.Combine(
                snapshot.Map.DirectoryPath,
                ".mapstudio-backups",
                DateTimeOffset.UtcNow.ToString(
                    "yyyyMMdd-HHmmssfff'Z'",
                    CultureInfo.InvariantCulture) +
                "-native-spline-" +
                Guid.NewGuid().ToString("N"));

        var writes =
            new List<PendingFileWrite>();

        var affectedPaths =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase)
            {
                request.Tile.RelativeMapPath
            };

        if (
            request.PreviousSplineId >= 0 &&
            request.NextSplineId >= 0 &&
            request.PreviousSplineId ==
                request.NextSplineId)
        {
            throw new InvalidDataException(
                "sameSplineUsedAtBothEnds");
        }

        var linkEditsByPath =
            new Dictionary<
                string,
                (
                    OmsiTileReference Tile,
                    List<OmsiSplineLinkEdit> Edits
                )>(
                    StringComparer.OrdinalIgnoreCase);

        void AddLinkEdit(
            OmsiTileReference tile,
            string tilePath,
            OmsiSplineLinkEdit edit)
        {
            if (
                !linkEditsByPath.TryGetValue(
                    tilePath,
                    out var entry))
            {
                entry =
                    (
                        tile,
                        new List<
                            OmsiSplineLinkEdit>()
                    );

                linkEditsByPath[
                    tilePath] =
                    entry;
            }

            entry.Edits.Add(
                edit);

            affectedPaths.Add(
                tile.RelativeMapPath);
        }

        if (request.PreviousSplineId >= 0)
        {
            var previousEntry =
                mapContents
                    .FirstOrDefault(
                        entry =>
                            entry.Content.Splines
                                .Any(
                                    item =>
                                        item.SplineId ==
                                        request.PreviousSplineId));

            var previousSpline =
                previousEntry.Content
                    ?.Splines
                    .FirstOrDefault(
                        item =>
                            item.SplineId ==
                            request.PreviousSplineId);

            if (previousSpline is null)
            {
                throw new InvalidDataException(
                    "previousSplineNotFound");
            }

            if (
                previousSpline.NextSplineId !=
                -1)
            {
                throw new InvalidDataException(
                    "previousSplineAlreadyLinked");
            }

            if (
                !OmsiMapPathResolver
                    .TryResolveTilePath(
                        snapshot.Map.DirectoryPath,
                        previousEntry.Reference
                            .RelativeMapPath,
                        out var previousPath))
            {
                throw new InvalidDataException(
                    "previousSplineTilePathInvalid");
            }

            AddLinkEdit(
                previousEntry.Reference,
                previousPath,
                new OmsiSplineLinkEdit(
                    previousSpline.SourceSectionOrdinal,
                    previousSpline.SplinePath,
                    previousSpline.SplineId,
                    previousSpline.PreviousSplineId,
                    previousSpline.NextSplineId,
                    previousSpline.IsHeightSpline,
                    previousSpline.PreviousSplineId,
                    newSplineId));
        }

        if (request.NextSplineId >= 0)
        {
            var nextEntry =
                mapContents
                    .FirstOrDefault(
                        entry =>
                            entry.Content.Splines
                                .Any(
                                    item =>
                                        item.SplineId ==
                                        request.NextSplineId));

            var nextSpline =
                nextEntry.Content
                    ?.Splines
                    .FirstOrDefault(
                        item =>
                            item.SplineId ==
                            request.NextSplineId);

            if (nextSpline is null)
            {
                throw new InvalidDataException(
                    "nextSplineNotFound");
            }

            if (
                nextSpline.PreviousSplineId !=
                -1)
            {
                throw new InvalidDataException(
                    "nextSplineAlreadyLinked");
            }

            if (
                !OmsiMapPathResolver
                    .TryResolveTilePath(
                        snapshot.Map.DirectoryPath,
                        nextEntry.Reference
                            .RelativeMapPath,
                        out var nextPath))
            {
                throw new InvalidDataException(
                    "nextSplineTilePathInvalid");
            }

            AddLinkEdit(
                nextEntry.Reference,
                nextPath,
                new OmsiSplineLinkEdit(
                    nextSpline.SourceSectionOrdinal,
                    nextSpline.SplinePath,
                    nextSpline.SplineId,
                    nextSpline.PreviousSplineId,
                    nextSpline.NextSplineId,
                    nextSpline.IsHeightSpline,
                    newSplineId,
                    nextSpline.NextSplineId));
        }

        if (
            linkEditsByPath.TryGetValue(
                targetPath,
                out var targetLinkEdits))
        {
            var combinedDocument =
                OmsiConfigParser.ParseBytes(
                    targetBytes);

            targetBytes =
                OmsiTileSplineLinkEditor
                    .ApplyLinks(
                        combinedDocument,
                        targetLinkEdits.Edits)
                    .Bytes;

            linkEditsByPath.Remove(
                targetPath);
        }

        foreach (
            var pair in
                linkEditsByPath)
        {
            var document =
                await OmsiConfigParser
                    .ParseFileAsync(
                        pair.Key,
                        cancellationToken)
                    .ConfigureAwait(false);

            var bytes =
                OmsiTileSplineLinkEditor
                    .ApplyLinks(
                        document,
                        pair.Value.Edits)
                    .Bytes;

            writes.Add(
                new PendingFileWrite(
                    pair.Key,
                    Path.Combine(
                        backupRoot,
                        Path.GetRelativePath(
                            snapshot.Map.DirectoryPath,
                            pair.Key)),
                    bytes));
        }

        writes.Add(
            new PendingFileWrite(
                targetPath,
                Path.Combine(
                    backupRoot,
                    Path.GetRelativePath(
                        snapshot.Map.DirectoryPath,
                        targetPath)),
                targetBytes));

        await SafeFileTransaction
            .WriteAllAsync(
                writes,
                cancellationToken)
            .ConfigureAwait(false);

        LastBackupDirectory =
            backupRoot;

        var refreshed =
            new List<NativeLoadedTile>(
                snapshot.Tiles.Count);

        foreach (var tile in snapshot.Tiles)
        {
            if (
                !affectedPaths.Contains(
                    tile.Reference.RelativeMapPath))
            {
                refreshed.Add(tile);
                continue;
            }

            if (
                !OmsiMapPathResolver.TryResolveTilePath(
                    snapshot.Map.DirectoryPath,
                    tile.Reference.RelativeMapPath,
                    out var path))
            {
                throw new InvalidDataException(
                    "splineReloadTilePathInvalid");
            }

            refreshed.Add(
                new NativeLoadedTile(
                    tile.Reference,
                    await _tileReader
                        .ReadContentAsync(
                            path,
                            cancellationToken)
                        .ConfigureAwait(false)));
        }

        CurrentMap =
            snapshot with
            {
                Tiles =
                    refreshed.ToArray()
            };

        return new NativeSplineInsertionResult(
            CurrentMap,
            newSplineId);
    }

    public async Task<NativeSplineBatchInsertionResult>
        InsertSplineBatchAsync(
            IReadOnlyList<NativeSplinePlacementRequest> requests,
            IReadOnlyList<NativeProceduralRoadPlacementLink>? links = null,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requests);

        if (requests.Count == 0 || requests.Count > 5_000)
        {
            throw new ArgumentOutOfRangeException(nameof(requests));
        }

        if (requests.Any(request =>
                request.PreviousSplineId >= 0 ||
                request.NextSplineId >= 0))
        {
            throw new InvalidDataException(
                "batchSplineExplicitLinksUnsupported");
        }

        links ??=
            Array.Empty<
                NativeProceduralRoadPlacementLink>();

        var previousByIndex =
            new Dictionary<int, int>();

        var nextByIndex =
            new Dictionary<int, int>();

        foreach (var link in links)
        {
            if (
                link.PreviousRequestIndex < 0 ||
                link.PreviousRequestIndex >=
                    requests.Count ||
                link.NextRequestIndex < 0 ||
                link.NextRequestIndex >=
                    requests.Count ||
                link.PreviousRequestIndex ==
                    link.NextRequestIndex ||
                nextByIndex.ContainsKey(
                    link.PreviousRequestIndex) ||
                previousByIndex.ContainsKey(
                    link.NextRequestIndex))
            {
                throw new InvalidDataException(
                    "batchSplineLinkInvalid");
            }

            nextByIndex[
                link.PreviousRequestIndex] =
                link.NextRequestIndex;

            previousByIndex[
                link.NextRequestIndex] =
                link.PreviousRequestIndex;
        }

        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        if (_pendingTransforms.Count > 0)
        {
            throw new InvalidOperationException(
                "savePendingBeforeSplineBatchInsertion");
        }

        var loadedByPath =
            snapshot.Tiles.ToDictionary(
                tile => tile.Reference.RelativeMapPath,
                tile => tile.Content,
                StringComparer.OrdinalIgnoreCase);

        var mapContents =
            new List<(OmsiTileReference Reference, OmsiTileContent Content)>(
                snapshot.Map.Tiles.Count);

        foreach (var tile in snapshot.Map.Tiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (loadedByPath.TryGetValue(
                    tile.RelativeMapPath,
                    out var loaded))
            {
                mapContents.Add((tile, loaded));
                continue;
            }

            if (!OmsiMapPathResolver.TryResolveTilePath(
                    snapshot.Map.DirectoryPath,
                    tile.RelativeMapPath,
                    out var path))
            {
                continue;
            }

            mapContents.Add(
                (
                    tile,
                    await _tileReader
                        .ReadContentAsync(path, cancellationToken)
                        .ConfigureAwait(false)
                ));
        }

        var contents =
            mapContents
                .Select(item => item.Content)
                .ToArray();

        var maxUsedId =
            contents
                .SelectMany(content =>
                    content.Objects
                        .Select(item => item.ObjectId)
                        .Concat(
                            content.Splines
                                .Select(item => item.SplineId)))
                .DefaultIfEmpty(0)
                .Max();

        if (maxUsedId > int.MaxValue - requests.Count)
        {
            throw new InvalidDataException(
                "splineIdExhausted");
        }

        var grouped =
            new Dictionary<
                string,
                (
                    OmsiTileReference Tile,
                    List<(int Index, NativeSplinePlacementRequest Request)> Requests
                )>(
                    StringComparer.OrdinalIgnoreCase);

        for (
            var requestIndex = 0;
            requestIndex < requests.Count;
            requestIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var request =
                requests[requestIndex];

            if (!OmsiMapPathResolver.TryResolveTilePath(
                    snapshot.Map.DirectoryPath,
                    request.Tile.RelativeMapPath,
                    out var targetPath))
            {
                throw new InvalidDataException(
                    "splinePlacementTilePathInvalid");
            }

            if (!grouped.TryGetValue(
                    targetPath,
                    out var group))
            {
                group =
                    (
                        request.Tile,
                        new List<(int Index, NativeSplinePlacementRequest Request)>()
                    );

                grouped[targetPath] =
                    group;
            }

            group.Requests.Add(
                (
                    requestIndex,
                    request
                ));
        }

        var backupRoot =
            Path.Combine(
                snapshot.Map.DirectoryPath,
                ".mapstudio-backups",
                DateTimeOffset.UtcNow.ToString(
                    "yyyyMMdd-HHmmssfff'Z'",
                    CultureInfo.InvariantCulture) +
                "-procedural-roads-" +
                Guid.NewGuid().ToString("N"));

        var writes =
            new List<PendingFileWrite>(
                grouped.Count);

        var affectedPaths =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        var assignedIds =
            new int[
                requests.Count];

        for (
            var index = 0;
            index < requests.Count;
            index++)
        {
            assignedIds[index] =
                checked(
                    maxUsedId +
                    index +
                    1);
        }

        maxUsedId =
            assignedIds[^1];

        var insertedIds =
            new List<int>(
                requests.Count);

        foreach (var pair in grouped)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var document =
                await OmsiConfigParser
                    .ParseFileAsync(
                        pair.Key,
                        cancellationToken)
                    .ConfigureAwait(false);

            byte[]? finalBytes =
                null;

            foreach (var item in pair.Value.Requests)
            {
                var requestIndex =
                    item.Index;

                var request =
                    item.Request;

                var template =
                    contents
                        .SelectMany(content => content.Splines)
                        .FirstOrDefault(item =>
                            item.IsHeightSpline ==
                                request.IsHeightSpline &&
                            string.Equals(
                                item.SplinePath,
                                request.SplinePath,
                                StringComparison.OrdinalIgnoreCase))
                    ?? OmsiSplinePlacementTemplateAnalyzer
                        .FindNeutralTemplate(
                            contents,
                            request.IsHeightSpline);

                if (request.IsHeightSpline &&
                    template is null)
                {
                    throw new InvalidDataException(
                        "splineInsertTemplateUnavailable");
                }

                var newSplineId =
                    assignedIds[
                        requestIndex];

                var previousSplineId =
                    previousByIndex
                        .TryGetValue(
                            requestIndex,
                            out var previousIndex)
                        ? assignedIds[
                            previousIndex]
                        : -1;

                var nextSplineId =
                    nextByIndex
                        .TryGetValue(
                            requestIndex,
                            out var nextIndex)
                        ? assignedIds[
                            nextIndex]
                        : -1;

                var insertion =
                    OmsiTileSplineInserter.Append(
                        document,
                        new OmsiNewPlacedSpline(
                            template?.HeaderValue ?? "0",
                            request.SplinePath,
                            newSplineId,
                            previousSplineId,
                            nextSplineId,
                            request.X,
                            request.Z,
                            request.Y,
                            request.Rotation,
                            request.Length,
                            request.Radius,
                            request.GradientStart,
                            request.GradientEnd,
                            request.IsHeightSpline,
                            template?.ExtraValues ??
                                Array.Empty<string>()));

                finalBytes =
                    insertion.Bytes;

                document =
                    OmsiConfigParser.ParseBytes(
                        finalBytes);

                insertedIds.Add(
                    newSplineId);
            }

            if (finalBytes is null)
            {
                continue;
            }

            var relativeTarget =
                Path.GetRelativePath(
                    snapshot.Map.DirectoryPath,
                    pair.Key);

            writes.Add(
                new PendingFileWrite(
                    pair.Key,
                    Path.Combine(
                        backupRoot,
                        relativeTarget),
                    finalBytes));

            affectedPaths.Add(
                pair.Value.Tile.RelativeMapPath);
        }

        if (writes.Count == 0 ||
            insertedIds.Count != requests.Count)
        {
            throw new InvalidDataException(
                "proceduralRoadBatchEmpty");
        }

        await SafeFileTransaction
            .WriteAllAsync(
                writes,
                cancellationToken)
            .ConfigureAwait(false);

        LastBackupDirectory =
            backupRoot;

        var refreshed =
            new List<NativeLoadedTile>(
                snapshot.Tiles.Count);

        foreach (var tile in snapshot.Tiles)
        {
            if (!affectedPaths.Contains(
                    tile.Reference.RelativeMapPath))
            {
                refreshed.Add(tile);
                continue;
            }

            if (!OmsiMapPathResolver.TryResolveTilePath(
                    snapshot.Map.DirectoryPath,
                    tile.Reference.RelativeMapPath,
                    out var path))
            {
                throw new InvalidDataException(
                    "splineReloadTilePathInvalid");
            }

            refreshed.Add(
                new NativeLoadedTile(
                    tile.Reference,
                    await _tileReader
                        .ReadContentAsync(
                            path,
                            cancellationToken)
                        .ConfigureAwait(false)));
        }

        CurrentMap =
            snapshot with
            {
                Tiles =
                    refreshed.ToArray()
            };

        return new NativeSplineBatchInsertionResult(
            CurrentMap,
            insertedIds,
            backupRoot);
    }

    public async Task<NativeTrafficRulesUpdateResult>
        UpdateTrafficRulesAsync(
            PickingKind ownerKind,
            int tileX,
            int tileY,
            int entityId,
            IReadOnlyList<
                OmsiTrafficRule>
                rules,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentNullException.ThrowIfNull(
            rules);

        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        if (_pendingTransforms.Count > 0)
        {
            throw new InvalidOperationException(
                "savePendingBeforeTrafficRuleEdit");
        }

        var loaded =
            snapshot.Tiles
                .FirstOrDefault(
                    tile =>
                        tile.Reference.X ==
                            tileX &&
                        tile.Reference.Y ==
                            tileY)
            ?? throw new InvalidDataException(
                "trafficRuleTileNotLoaded");

        var splineOwner =
            ownerKind ==
            PickingKind.Spline;

        if (
            ownerKind is not
                (
                    PickingKind.Object or
                    PickingKind.Spline
                ))
        {
            throw new InvalidDataException(
                "trafficRuleOwnerKindInvalid");
        }

        var sourceOrdinal =
            splineOwner
                ? loaded.Content.Splines
                    .FirstOrDefault(
                        item =>
                            item.SplineId ==
                            entityId)
                    ?.SourceSectionOrdinal ??
                  -1
                : loaded.Content.Objects
                    .FirstOrDefault(
                        item =>
                            item.ObjectId ==
                            entityId)
                    ?.SourceSectionOrdinal ??
                  -1;

        if (sourceOrdinal < 0)
        {
            throw new InvalidDataException(
                "trafficRuleOwnerMissing");
        }

        if (
            !OmsiMapPathResolver
                .TryResolveTilePath(
                    snapshot.Map.DirectoryPath,
                    loaded.Reference
                        .RelativeMapPath,
                    out var tilePath) ||
            !File.Exists(tilePath))
        {
            throw new InvalidDataException(
                "trafficRuleTilePathInvalid");
        }

        var document =
            await OmsiConfigParser
                .ParseFileAsync(
                    tilePath,
                    cancellationToken)
                .ConfigureAwait(false);

        var bytes =
            new OmsiTileTrafficRulePatcher()
                .Patch(
                    document,
                    splineOwner,
                    sourceOrdinal,
                    rules);

        var backupPath =
            CreateNativeBackupPath(
                snapshot.Map.DirectoryPath,
                tilePath);

        await SafeFileTransaction
            .WriteAllAsync(
                [
                    new PendingFileWrite(
                        tilePath,
                        backupPath,
                        bytes)
                ],
                cancellationToken)
            .ConfigureAwait(false);

        var refreshed =
            await _tileReader
                .ReadContentAsync(
                    tilePath,
                    cancellationToken)
                .ConfigureAwait(false);

        CurrentMap =
            snapshot with
            {
                Tiles =
                    snapshot.Tiles
                        .Select(
                            tile =>
                                tile.Reference.X ==
                                    tileX &&
                                tile.Reference.Y ==
                                    tileY
                                    ? new NativeLoadedTile(
                                        tile.Reference,
                                        refreshed)
                                    : tile)
                        .ToArray()
            };

        return new NativeTrafficRulesUpdateResult(
            CurrentMap,
            backupPath);
    }

    public async Task<NativeTrafficLightProgramUpdateResult>
        UpdateTrafficLightProgramAsync(
            NativeTrafficLightProgramInfo info,
            string programName,
            double? cycleDuration,
            IReadOnlyList<
                OmsiTrafficLightPhase>
                phases,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentNullException.ThrowIfNull(
            info);

        ArgumentNullException.ThrowIfNull(
            phases);

        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        var root =
            OmsiRootPath ??
            throw new InvalidOperationException(
                "Instalação OMSI não selecionada.");

        if (
            _pendingTransforms.Count >
            0)
        {
            throw new InvalidOperationException(
                "savePendingBeforeTrafficLightEdit");
        }

        if (
            string.IsNullOrWhiteSpace(
                programName) ||
            cycleDuration is
                < 0 ||
            phases.Count ==
                0 ||
            phases.Count >
                4096 ||
            phases.Any(
                phase =>
                    phase.Duration <
                        0 ||
                    !double.IsFinite(
                        phase.Duration)))
        {
            throw new InvalidDataException(
                "invalidTrafficLightProgram");
        }

        if (
            !OmsiSceneryObjectPathResolver
                .TryResolve(
                    root,
                    info.AssetPath,
                    out var target) ||
            !File.Exists(
                target))
        {
            throw new FileNotFoundException(
                "trafficLightSceneryObjectMissing",
                info.AssetPath);
        }

        var document =
            await OmsiConfigParser
                .ParseFileAsync(
                    target,
                    cancellationToken)
                .ConfigureAwait(false);

        var metadata =
            OmsiSceneryObjectReader
                .ReadMetadata(
                    document);

        if (
            info.ControllerIndex <
                0 ||
            info.ControllerIndex >=
                metadata
                    .TrafficLightControllers
                    .Count)
        {
            throw new InvalidDataException(
                "trafficLightControllerIndexInvalid");
        }

        var controllers =
            metadata
                .TrafficLightControllers
                .Select(
                    controller =>
                        controller with
                        {
                            Programs =
                                controller
                                    .Programs
                                    .ToArray()
                        })
                .ToArray();

        var controller =
            controllers[
                info.ControllerIndex];

        if (
            info.ProgramIndex <
                0 ||
            info.ProgramIndex >=
                controller
                    .Programs
                    .Count)
        {
            throw new InvalidDataException(
                "trafficLightProgramIndexInvalid");
        }

        var programs =
            controller
                .Programs
                .ToArray();

        programs[
            info.ProgramIndex] =
            new OmsiTrafficLightProgram(
                programName.Trim(),
                phases.ToArray());

        controllers[
            info.ControllerIndex] =
            controller with
            {
                CycleDuration =
                    cycleDuration,
                Programs =
                    programs
            };

        var bytes =
            new OmsiSceneryTrafficLightPatcher()
                .Patch(
                    document,
                    controllers);

        var backupPath =
            CreateNativeBackupPath(
                snapshot.Map
                    .DirectoryPath,
                target);

        await SafeFileTransaction
            .WriteAllAsync(
                [
                    new PendingFileWrite(
                        target,
                        backupPath,
                        bytes)
                ],
                cancellationToken)
            .ConfigureAwait(false);

        var reloaded =
            await new OmsiSceneryObjectReader()
                .ReadMetadataAsync(
                    target,
                    cancellationToken)
                .ConfigureAwait(false);

        var updatedController =
            reloaded
                .TrafficLightControllers[
                    info.ControllerIndex];

        var updatedProgram =
            updatedController
                .Programs[
                    info.ProgramIndex];

        return new NativeTrafficLightProgramUpdateResult(
            updatedProgram,
            updatedController
                .CycleDuration,
            target,
            backupPath);
    }

    public async Task<NativeTimetableLineUpdateResult>
        UpdateTimetableLineAsync(
            OmsiTimetableLine line,
            OmsiTimetableLine updatedLine,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentNullException.ThrowIfNull(
            line);

        ArgumentNullException.ThrowIfNull(
            updatedLine);

        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        if (_pendingTransforms.Count > 0)
        {
            throw new InvalidOperationException(
                "savePendingBeforeTimetableEdit");
        }

        var mapRoot =
            Path.GetFullPath(
                snapshot.Map.DirectoryPath);

        var target =
            Path.GetFullPath(
                line.FilePath);

        var relative =
            Path.GetRelativePath(
                mapRoot,
                target);

        if (
            !IsSafeRelativePath(
                relative) ||
            !relative.StartsWith(
                "TTData" +
                Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                Path.GetExtension(
                    target),
                ".ttl",
                StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(
                target))
        {
            throw new InvalidDataException(
                "invalidTimetableLinePath");
        }

        var existingText =
            await File.ReadAllTextAsync(
                    target,
                    System.Text.Encoding
                        .Latin1,
                    cancellationToken)
                .ConfigureAwait(false);

        var newLine =
            existingText.Contains(
                "\r\n",
                StringComparison.Ordinal)
                ? "\r\n"
                : "\n";

        var normalized =
            updatedLine with
            {
                FilePath =
                    line.FilePath,
                RelativePath =
                    line.RelativePath,
                Name =
                    line.Name
            };

        var bytes =
            new OmsiTimetableLineWriter()
                .Write(
                    normalized,
                    newLine);

        var backupPath =
            CreateNativeBackupPath(
                snapshot.Map.DirectoryPath,
                target);

        await SafeFileTransaction
            .WriteAllAsync(
                [
                    new PendingFileWrite(
                        target,
                        backupPath,
                        bytes)
                ],
                cancellationToken)
            .ConfigureAwait(false);

        var reloaded =
            await new OmsiTimetableLineReader()
                .ReadAsync(
                    snapshot.Map.DirectoryPath,
                    target,
                    cancellationToken)
                .ConfigureAwait(false);

        return new NativeTimetableLineUpdateResult(
            reloaded,
            backupPath);
    }

    public async Task<NativeTimetableTripUpdateResult>
        UpdateTimetableTripAsync(
            OmsiTimetableTrip trip,
            OmsiTimetableTrip updatedTrip,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentNullException.ThrowIfNull(
            trip);

        ArgumentNullException.ThrowIfNull(
            updatedTrip);

        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        if (_pendingTransforms.Count > 0)
        {
            throw new InvalidOperationException(
                "savePendingBeforeTimetableEdit");
        }

        var mapRoot =
            Path.GetFullPath(
                snapshot.Map.DirectoryPath);

        var target =
            Path.GetFullPath(
                trip.FilePath);

        var relative =
            Path.GetRelativePath(
                mapRoot,
                target);

        if (
            !IsSafeRelativePath(
                relative) ||
            !relative.StartsWith(
                "TTData" +
                Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                Path.GetExtension(
                    target),
                ".ttp",
                StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(
                target))
        {
            throw new InvalidDataException(
                "invalidTimetableTripPath");
        }

        var existingText =
            await File.ReadAllTextAsync(
                    target,
                    System.Text.Encoding
                        .Latin1,
                    cancellationToken)
                .ConfigureAwait(false);

        var newLine =
            existingText.Contains(
                "\r\n",
                StringComparison.Ordinal)
                ? "\r\n"
                : "\n";

        var normalized =
            updatedTrip with
            {
                FilePath =
                    trip.FilePath,
                RelativePath =
                    trip.RelativePath,
                Name =
                    trip.Name
            };

        var bytes =
            new OmsiTimetableTripWriter()
                .Write(
                    normalized,
                    newLine);

        var backupPath =
            CreateNativeBackupPath(
                snapshot.Map.DirectoryPath,
                target);

        await SafeFileTransaction
            .WriteAllAsync(
                [
                    new PendingFileWrite(
                        target,
                        backupPath,
                        bytes)
                ],
                cancellationToken)
            .ConfigureAwait(false);

        var reloaded =
            await new OmsiTimetableTripReader()
                .ReadAsync(
                    snapshot.Map.DirectoryPath,
                    target,
                    cancellationToken)
                .ConfigureAwait(false);

        return new NativeTimetableTripUpdateResult(
            reloaded,
            backupPath);
    }

    public async Task<NativeBusStopUpdateResult>
        UpdateBusStopAsync(
            int stopIndex,
            OmsiTimetableBusStop updatedStop,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentNullException.ThrowIfNull(
            updatedStop);

        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        if (_pendingTransforms.Count > 0)
        {
            throw new InvalidOperationException(
                "savePendingBeforeTimetableEdit");
        }

        var target =
            Path.GetFullPath(
                Path.Combine(
                    snapshot.Map.DirectoryPath,
                    "TTData",
                    "Busstops.cfg"));

        var relative =
            Path.GetRelativePath(
                snapshot.Map.DirectoryPath,
                target);

        if (
            !IsSafeRelativePath(
                relative) ||
            !File.Exists(
                target))
        {
            throw new InvalidDataException(
                "invalidBusStopPath");
        }

        var reader =
            new OmsiTimetableBusStopReader();

        var stops =
            (
                await reader
                    .ReadAsync(
                        target,
                        cancellationToken)
                    .ConfigureAwait(false)
            )
            .ToList();

        if (
            stopIndex < 0 ||
            stopIndex >=
                stops.Count)
        {
            throw new InvalidDataException(
                "busStopIndexInvalid");
        }

        stops[stopIndex] =
            updatedStop;

        var duplicateIds =
            stops
                .GroupBy(
                    stop =>
                        stop.Id)
                .Any(
                    group =>
                        group.Count() >
                        1);

        if (duplicateIds)
        {
            throw new InvalidDataException(
                "duplicateBusStopId");
        }

        var existingText =
            await File.ReadAllTextAsync(
                    target,
                    System.Text.Encoding
                        .Latin1,
                    cancellationToken)
                .ConfigureAwait(false);

        var newLine =
            existingText.Contains(
                "\r\n",
                StringComparison.Ordinal)
                ? "\r\n"
                : "\n";

        var bytes =
            new OmsiTimetableBusStopWriter()
                .Write(
                    stops,
                    newLine);

        var backupPath =
            CreateNativeBackupPath(
                snapshot.Map.DirectoryPath,
                target);

        await SafeFileTransaction
            .WriteAllAsync(
                [
                    new PendingFileWrite(
                        target,
                        backupPath,
                        bytes)
                ],
                cancellationToken)
            .ConfigureAwait(false);

        var reloaded =
            await reader
                .ReadAsync(
                    target,
                    cancellationToken)
                .ConfigureAwait(false);

        return new NativeBusStopUpdateResult(
            reloaded,
            stopIndex,
            backupPath);
    }

    public async Task<NativeStationLinkUpdateResult>
        UpdateStationLinkAsync(
            int linkIndex,
            OmsiStationLink updatedLink,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentNullException.ThrowIfNull(
            updatedLink);

        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        if (_pendingTransforms.Count > 0)
        {
            throw new InvalidOperationException(
                "savePendingBeforeTimetableEdit");
        }

        var target =
            Path.GetFullPath(
                Path.Combine(
                    snapshot.Map.DirectoryPath,
                    "TTData",
                    "StnLinks.cfg"));

        var relative =
            Path.GetRelativePath(
                snapshot.Map.DirectoryPath,
                target);

        if (
            !IsSafeRelativePath(
                relative) ||
            !File.Exists(
                target))
        {
            throw new InvalidDataException(
                "invalidStationLinkPath");
        }

        var reader =
            new OmsiStationLinkReader();

        var links =
            (
                await reader
                    .ReadAsync(
                        target,
                        cancellationToken)
                    .ConfigureAwait(false)
            )
            .ToList();

        if (
            linkIndex < 0 ||
            linkIndex >=
                links.Count)
        {
            throw new InvalidDataException(
                "stationLinkIndexInvalid");
        }

        links[linkIndex] =
            updatedLink;

        var existingText =
            await File.ReadAllTextAsync(
                    target,
                    System.Text.Encoding
                        .Latin1,
                    cancellationToken)
                .ConfigureAwait(false);

        var newLine =
            existingText.Contains(
                "\r\n",
                StringComparison.Ordinal)
                ? "\r\n"
                : "\n";

        var bytes =
            new OmsiStationLinkWriter()
                .Write(
                    links,
                    newLine);

        var backupPath =
            CreateNativeBackupPath(
                snapshot.Map.DirectoryPath,
                target);

        await SafeFileTransaction
            .WriteAllAsync(
                [
                    new PendingFileWrite(
                        target,
                        backupPath,
                        bytes)
                ],
                cancellationToken)
            .ConfigureAwait(false);

        var reloaded =
            await reader
                .ReadAsync(
                    target,
                    cancellationToken)
                .ConfigureAwait(false);

        return new NativeStationLinkUpdateResult(
            reloaded,
            linkIndex,
            backupPath);
    }

    public async Task<NativeTimetableTrackUpdateResult>
        UpdateTimetableTrackAsync(
            OmsiTimetableTrack track,
            IReadOnlyList<
                OmsiTimetableTrackEntry>
                entries,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentNullException.ThrowIfNull(
            track);

        ArgumentNullException.ThrowIfNull(
            entries);

        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        if (_pendingTransforms.Count > 0)
        {
            throw new InvalidOperationException(
                "savePendingBeforeTimetableEdit");
        }

        var mapRoot =
            Path.GetFullPath(
                snapshot.Map
                    .DirectoryPath);

        var target =
            Path.GetFullPath(
                track.FilePath);

        var relative =
            Path.GetRelativePath(
                mapRoot,
                target);

        if (
            !IsSafeRelativePath(
                relative) ||
            !relative.StartsWith(
                "TTData" +
                Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                Path.GetExtension(
                    target),
                ".ttr",
                StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(
                target))
        {
            throw new InvalidDataException(
                "invalidTimetableTrackPath");
        }

        var existingText =
            await File.ReadAllTextAsync(
                    target,
                    System.Text.Encoding
                        .Latin1,
                    cancellationToken)
                .ConfigureAwait(false);

        var newLine =
            existingText.Contains(
                "\r\n",
                StringComparison.Ordinal)
                ? "\r\n"
                : "\n";

        var bytes =
            new OmsiTimetableTrackWriter()
                .Write(
                    track,
                    entries,
                    newLine);

        var backupPath =
            CreateNativeBackupPath(
                snapshot.Map
                    .DirectoryPath,
                target);

        await SafeFileTransaction
            .WriteAllAsync(
                [
                    new PendingFileWrite(
                        target,
                        backupPath,
                        bytes)
                ],
                cancellationToken)
            .ConfigureAwait(false);

        var updated =
            await new OmsiTimetableTrackReader()
                .ReadAsync(
                    snapshot.Map.DirectoryPath,
                    target,
                    cancellationToken)
                .ConfigureAwait(false);

        return new NativeTimetableTrackUpdateResult(
            updated,
            backupPath);
    }

    public async Task<NativeMapSnapshot>
        LevelTerrainAsync(
            NativeTerrainEditPoint point,
            double targetHeight,
            double radius,
            double feather,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentNullException.ThrowIfNull(
            point);

        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        if (_pendingTransforms.Count > 0)
        {
            throw new InvalidOperationException(
                "savePendingBeforeTerrainEdit");
        }

        var loaded =
            snapshot.Tiles
                .FirstOrDefault(
                    item =>
                        item.Reference.X ==
                            point.Tile.X &&
                        item.Reference.Y ==
                            point.Tile.Y &&
                        string.Equals(
                            item.Reference.RelativeMapPath,
                            point.Tile.RelativeMapPath,
                            StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException(
                "terrainTileNotLoaded");

        if (
            !OmsiMapPathResolver.TryResolveTilePath(
                snapshot.Map.DirectoryPath,
                loaded.Reference.RelativeMapPath,
                out var tilePath))
        {
            throw new InvalidDataException(
                "terrainTilePathInvalid");
        }

        var terrainPath =
            tilePath +
            ".terrain";

        if (!File.Exists(terrainPath))
        {
            throw new InvalidDataException(
                "terrainFileMissing");
        }

        var terrain =
            await new OmsiTerrainReader()
                .ReadAsync(
                    terrainPath,
                    cancellationToken)
                .ConfigureAwait(false);

        var result =
            OmsiTerrainLeveler
                .LevelCircularBrush(
                    terrain,
                    point.LocalX,
                    point.LocalY,
                    targetHeight,
                    radius,
                    feather);

        if (result.ChangedSamples == 0)
        {
            return snapshot;
        }

        var bytes =
            OmsiTerrainWriter
                .Write(
                    result.Terrain);

        await SafeFileTransaction
            .WriteAllAsync(
                [
                    new PendingFileWrite(
                        terrainPath,
                        CreateNativeBackupPath(
                            snapshot.Map.DirectoryPath,
                            terrainPath),
                        bytes)
                ],
                cancellationToken)
            .ConfigureAwait(false);

        var refreshedContent =
            await _tileReader
                .ReadContentAsync(
                    tilePath,
                    cancellationToken)
                .ConfigureAwait(false);

        CurrentMap =
            snapshot with
            {
                Tiles =
                    snapshot.Tiles
                        .Select(
                            item =>
                                string.Equals(
                                    item.Reference.RelativeMapPath,
                                    loaded.Reference.RelativeMapPath,
                                    StringComparison.OrdinalIgnoreCase)
                                    ? new NativeLoadedTile(
                                        item.Reference,
                                        refreshedContent)
                                    : item)
                        .ToArray()
            };

        return CurrentMap;
    }

    public async Task<NativeMapSnapshot>
        OffsetTerrainAsync(
            NativeTerrainEditPoint point,
            double deltaHeight,
            double radius,
            double feather,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentNullException.ThrowIfNull(
            point);

        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        if (_pendingTransforms.Count > 0)
        {
            throw new InvalidOperationException(
                "savePendingBeforeTerrainEdit");
        }

        var loaded =
            snapshot.Tiles
                .FirstOrDefault(
                    item =>
                        item.Reference.X ==
                            point.Tile.X &&
                        item.Reference.Y ==
                            point.Tile.Y &&
                        string.Equals(
                            item.Reference.RelativeMapPath,
                            point.Tile.RelativeMapPath,
                            StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException(
                "terrainTileNotLoaded");

        if (
            !OmsiMapPathResolver.TryResolveTilePath(
                snapshot.Map.DirectoryPath,
                loaded.Reference.RelativeMapPath,
                out var tilePath))
        {
            throw new InvalidDataException(
                "terrainTilePathInvalid");
        }

        var terrainPath =
            tilePath +
            ".terrain";

        if (!File.Exists(terrainPath))
        {
            throw new InvalidDataException(
                "terrainFileMissing");
        }

        var terrain =
            await new OmsiTerrainReader()
                .ReadAsync(
                    terrainPath,
                    cancellationToken)
                .ConfigureAwait(false);

        var result =
            OmsiTerrainLeveler
                .OffsetCircularBrush(
                    terrain,
                    point.LocalX,
                    point.LocalY,
                    deltaHeight,
                    radius,
                    feather);

        if (result.ChangedSamples == 0)
        {
            return snapshot;
        }

        var bytes =
            OmsiTerrainWriter
                .Write(
                    result.Terrain);

        await SafeFileTransaction
            .WriteAllAsync(
                [
                    new PendingFileWrite(
                        terrainPath,
                        CreateNativeBackupPath(
                            snapshot.Map.DirectoryPath,
                            terrainPath),
                        bytes)
                ],
                cancellationToken)
            .ConfigureAwait(false);

        var refreshedContent =
            await _tileReader
                .ReadContentAsync(
                    tilePath,
                    cancellationToken)
                .ConfigureAwait(false);

        CurrentMap =
            snapshot with
            {
                Tiles =
                    snapshot.Tiles
                        .Select(
                            item =>
                                string.Equals(
                                    item.Reference.RelativeMapPath,
                                    loaded.Reference.RelativeMapPath,
                                    StringComparison.OrdinalIgnoreCase)
                                    ? new NativeLoadedTile(
                                        item.Reference,
                                        refreshedContent)
                                    : item)
                        .ToArray()
            };

        return CurrentMap;
    }

    public async Task<NativeMapSnapshot>
        PaintTerrainTextureAsync(
            NativeTerrainEditPoint point,
            int layerIndex,
            byte targetAlpha,
            double radius,
            double feather,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentNullException.ThrowIfNull(
            point);

        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        if (_pendingTransforms.Count > 0)
        {
            throw new InvalidOperationException(
                "savePendingBeforeTerrainPaint");
        }

        if (
            layerIndex <= 0 ||
            layerIndex >=
                snapshot.Map
                    .GroundTextures
                    .Count)
        {
            throw new InvalidDataException(
                "terrainPaintLayerInvalid");
        }

        var groundTexture =
            snapshot.Map
                .GroundTextures[
                    layerIndex];

        var resolution =
            groundTexture
                .MaskResolution;

        var loaded =
            snapshot.Tiles
                .FirstOrDefault(
                    item =>
                        item.Reference.X ==
                            point.Tile.X &&
                        item.Reference.Y ==
                            point.Tile.Y &&
                        string.Equals(
                            item.Reference.RelativeMapPath,
                            point.Tile.RelativeMapPath,
                            StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException(
                "terrainTileNotLoaded");

        if (
            !OmsiMapPathResolver.TryResolveTilePath(
                snapshot.Map.DirectoryPath,
                loaded.Reference.RelativeMapPath,
                out var tilePath))
        {
            throw new InvalidDataException(
                "terrainTilePathInvalid");
        }

        var maskDirectory =
            Path.Combine(
                snapshot.Map.DirectoryPath,
                "texture",
                "map");

        var maskPath =
            Path.GetFullPath(
                Path.Combine(
                    maskDirectory,
                    Path.GetFileName(
                        tilePath) +
                    "." +
                    layerIndex.ToString(
                        CultureInfo.InvariantCulture) +
                    ".dds"));

        var mapRoot =
            Path.GetFullPath(
                snapshot.Map.DirectoryPath)
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);

        var requiredPrefix =
            mapRoot +
            Path.DirectorySeparatorChar;

        if (
            !maskPath.StartsWith(
                requiredPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "terrainMaskPathInvalid");
        }

        var existed =
            File.Exists(
                maskPath);

        OmsiTerrainTextureMaskData source;

        if (existed)
        {
            source =
                OmsiTerrainTextureMaskDataReader
                    .Read(
                        maskPath);
        }
        else
        {
            if (
                resolution is not int size ||
                size <= 0)
            {
                throw new InvalidDataException(
                    "terrainMaskResolutionUnavailable");
            }

            source =
                new OmsiTerrainTextureMaskData(
                    size,
                    size,
                    new byte[
                        checked(
                            size *
                            size)]);
        }

        var result =
            OmsiTerrainTextureMaskPainter
                .PaintCircularBrush(
                    source,
                    point.LocalX,
                    point.LocalY,
                    radius,
                    targetAlpha,
                    feather);

        if (result.ChangedPixels == 0)
        {
            return snapshot;
        }

        var bytes =
            OmsiTerrainTextureMaskWriter
                .Write(
                    result.Mask);

        if (existed)
        {
            await SafeFileTransaction
                .WriteAllAsync(
                    [
                        new PendingFileWrite(
                            maskPath,
                            CreateNativeBackupPath(
                                snapshot.Map.DirectoryPath,
                                maskPath),
                            bytes)
                    ],
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await CreateNewFileAtomicallyAsync(
                    maskPath,
                    bytes,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var refreshedContent =
            await _tileReader
                .ReadContentAsync(
                    tilePath,
                    cancellationToken)
                .ConfigureAwait(false);

        CurrentMap =
            snapshot with
            {
                Tiles =
                    snapshot.Tiles
                        .Select(
                            item =>
                                string.Equals(
                                    item.Reference.RelativeMapPath,
                                    loaded.Reference.RelativeMapPath,
                                    StringComparison.OrdinalIgnoreCase)
                                    ? new NativeLoadedTile(
                                        item.Reference,
                                        refreshedContent)
                                    : item)
                        .ToArray()
            };

        return CurrentMap;
    }

    public async Task<NativeMapSnapshot>
        UpdateSplineLinksAsync(
            NativeSelectionInfo selection,
            int desiredPreviousSplineId,
            int desiredNextSplineId,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentNullException.ThrowIfNull(
            selection);

        if (
            selection.Kind !=
                PickingKind.Spline ||
            selection.PreviousSplineId is
                not int originalPrevious ||
            selection.NextSplineId is
                not int originalNext ||
            selection.IsHeightSpline is
                not bool isHeightSpline)
        {
            throw new InvalidDataException(
                "splineLinkSelectionInvalid");
        }

        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        if (_pendingTransforms.Count > 0)
        {
            throw new InvalidOperationException(
                "savePendingBeforeLinkEdit");
        }

        var entries =
            new List<(
                OmsiTileReference Tile,
                OmsiPlacedSpline Spline)>();

        foreach (var tile in snapshot.Map.Tiles)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            if (
                !OmsiMapPathResolver.TryResolveTilePath(
                    snapshot.Map.DirectoryPath,
                    tile.RelativeMapPath,
                    out var tilePath) ||
                !File.Exists(tilePath))
            {
                continue;
            }

            var content =
                await _tileReader
                    .ReadContentAsync(
                        tilePath,
                        cancellationToken)
                    .ConfigureAwait(false);

            foreach (var spline in content.Splines)
            {
                entries.Add(
                    (tile, spline));
            }
        }

        if (
            entries
                .GroupBy(
                    item =>
                        item.Spline.SplineId)
                .Any(
                    group =>
                        group.Count() > 1))
        {
            throw new InvalidDataException(
                "duplicateSplineId");
        }

        var byId =
            entries.ToDictionary(
                item =>
                    item.Spline.SplineId);

        if (
            !byId.TryGetValue(
                selection.EntityId,
                out var source) ||
            source.Tile.X != selection.TileX ||
            source.Tile.Y != selection.TileY ||
            source.Spline.PreviousSplineId !=
                originalPrevious ||
            source.Spline.NextSplineId !=
                originalNext ||
            source.Spline.IsHeightSpline !=
                isHeightSpline ||
            !string.Equals(
                source.Spline.SplinePath,
                selection.AssetPath,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "splineLinkSourceChanged");
        }

        var states =
            byId.ToDictionary(
                pair => pair.Key,
                pair =>
                    new OmsiSplineLinkState(
                        pair.Key,
                        pair.Value
                            .Spline
                            .PreviousSplineId,
                        pair.Value
                            .Spline
                            .NextSplineId));

        var plan =
            OmsiSplineLinkPlanner
                .Plan(
                    states,
                    source.Spline.SplineId,
                    originalPrevious,
                    originalNext,
                    desiredPreviousSplineId,
                    desiredNextSplineId);

        if (plan.Count == 0)
        {
            return snapshot;
        }

        var writes =
            new List<PendingFileWrite>();

        var affectedPaths =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (
            var group in plan.Keys
                .Select(
                    id =>
                        byId[id])
                .GroupBy(
                    item =>
                        item.Tile.RelativeMapPath,
                    StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            var tile =
                group.First().Tile;

            if (
                !OmsiMapPathResolver.TryResolveTilePath(
                    snapshot.Map.DirectoryPath,
                    tile.RelativeMapPath,
                    out var tilePath) ||
                !File.Exists(tilePath))
            {
                throw new InvalidDataException(
                    "splineLinkTilePathInvalid");
            }

            var document =
                await OmsiConfigParser
                    .ParseFileAsync(
                        tilePath,
                        cancellationToken)
                    .ConfigureAwait(false);

            var edits =
                group
                    .Select(
                        item =>
                        {
                            var target =
                                plan[
                                    item.Spline.SplineId];

                            return new OmsiSplineLinkEdit(
                                item.Spline
                                    .SourceSectionOrdinal,
                                item.Spline
                                    .SplinePath,
                                item.Spline
                                    .SplineId,
                                item.Spline
                                    .PreviousSplineId,
                                item.Spline
                                    .NextSplineId,
                                item.Spline
                                    .IsHeightSpline,
                                target
                                    .PreviousSplineId,
                                target
                                    .NextSplineId);
                        })
                    .ToArray();

            var result =
                OmsiTileSplineLinkEditor
                    .ApplyLinks(
                        document,
                        edits);

            writes.Add(
                new PendingFileWrite(
                    tilePath,
                    CreateNativeBackupPath(
                        snapshot.Map.DirectoryPath,
                        tilePath),
                    result.Bytes));

            affectedPaths.Add(
                tile.RelativeMapPath);
        }

        await SafeFileTransaction
            .WriteAllAsync(
                writes,
                cancellationToken)
            .ConfigureAwait(false);

        var refreshed =
            new List<NativeLoadedTile>(
                snapshot.Tiles.Count);

        foreach (var loaded in snapshot.Tiles)
        {
            if (
                !affectedPaths.Contains(
                    loaded.Reference
                        .RelativeMapPath))
            {
                refreshed.Add(
                    loaded);

                continue;
            }

            if (
                !OmsiMapPathResolver.TryResolveTilePath(
                    snapshot.Map.DirectoryPath,
                    loaded.Reference.RelativeMapPath,
                    out var tilePath))
            {
                throw new InvalidDataException(
                    "splineLinkReloadPathInvalid");
            }

            refreshed.Add(
                new NativeLoadedTile(
                    loaded.Reference,
                    await _tileReader
                        .ReadContentAsync(
                            tilePath,
                            cancellationToken)
                        .ConfigureAwait(false)));
        }

        CurrentMap =
            snapshot with
            {
                Tiles =
                    refreshed.ToArray()
            };

        return CurrentMap;
    }

    public async Task<NativeMapSnapshot>
        DeleteSelectionAsync(
            NativeSelectionInfo selection,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentNullException.ThrowIfNull(
            selection);

        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        if (_pendingTransforms.Count > 0)
        {
            throw new InvalidOperationException(
                "savePendingBeforeDelete");
        }

        return selection.Kind switch
        {
            PickingKind.Object =>
                await DeleteObjectSelectionAsync(
                    snapshot,
                    selection,
                    cancellationToken)
                    .ConfigureAwait(false),

            PickingKind.Spline =>
                await DeleteSplineSelectionAsync(
                    snapshot,
                    selection,
                    cancellationToken)
                    .ConfigureAwait(false),

            _ =>
                throw new InvalidDataException(
                    "selectionDeleteUnsupported")
        };
    }

    private async Task<NativeMapSnapshot>
        DeleteObjectSelectionAsync(
            NativeMapSnapshot snapshot,
            NativeSelectionInfo selection,
            CancellationToken cancellationToken)
    {
        var tile =
            snapshot.Tiles
                .FirstOrDefault(
                    item =>
                        item.Reference.X ==
                            selection.TileX &&
                        item.Reference.Y ==
                            selection.TileY)
            ?? throw new InvalidDataException(
                "objectDeleteTileNotLoaded");

        var source =
            tile.Content.Objects
                .FirstOrDefault(
                    item =>
                        item.ObjectId ==
                            selection.EntityId &&
                        string.Equals(
                            item.SceneryObjectPath,
                            selection.AssetPath,
                            StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException(
                "objectSourceChanged");

        if (
            !OmsiMapPathResolver.TryResolveTilePath(
                snapshot.Map.DirectoryPath,
                tile.Reference.RelativeMapPath,
                out var tilePath) ||
            !File.Exists(tilePath))
        {
            throw new InvalidDataException(
                "objectDeleteTilePathInvalid");
        }

        var document =
            await OmsiConfigParser
                .ParseFileAsync(
                    tilePath,
                    cancellationToken)
                .ConfigureAwait(false);

        var result =
            OmsiTileObjectDeleter
                .Remove(
                    document,
                    source.SourceSectionOrdinal,
                    source.SceneryObjectPath,
                    source.ObjectId);

        await SafeFileTransaction
            .WriteAllAsync(
                [
                    new PendingFileWrite(
                        tilePath,
                        CreateNativeBackupPath(
                            snapshot.Map.DirectoryPath,
                            tilePath),
                        result.Bytes)
                ],
                cancellationToken)
            .ConfigureAwait(false);

        var refreshedContent =
            await _tileReader
                .ReadContentAsync(
                    tilePath,
                    cancellationToken)
                .ConfigureAwait(false);

        CurrentMap =
            snapshot with
            {
                Tiles =
                    snapshot.Tiles
                        .Select(
                            item =>
                                string.Equals(
                                    item.Reference.RelativeMapPath,
                                    tile.Reference.RelativeMapPath,
                                    StringComparison.OrdinalIgnoreCase)
                                    ? new NativeLoadedTile(
                                        item.Reference,
                                        refreshedContent)
                                    : item)
                        .ToArray()
            };

        return CurrentMap;
    }

    private async Task<NativeMapSnapshot>
        DeleteSplineSelectionAsync(
            NativeMapSnapshot snapshot,
            NativeSelectionInfo selection,
            CancellationToken cancellationToken)
    {
        var entries =
            new List<(
                OmsiTileReference Tile,
                OmsiPlacedSpline Spline)>();

        foreach (var tile in snapshot.Map.Tiles)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            if (
                !OmsiMapPathResolver.TryResolveTilePath(
                    snapshot.Map.DirectoryPath,
                    tile.RelativeMapPath,
                    out var tilePath) ||
                !File.Exists(tilePath))
            {
                continue;
            }

            var content =
                await _tileReader
                    .ReadContentAsync(
                        tilePath,
                        cancellationToken)
                    .ConfigureAwait(false);

            foreach (var spline in content.Splines)
            {
                entries.Add(
                    (tile, spline));
            }
        }

        if (
            entries
                .GroupBy(
                    item =>
                        item.Spline.SplineId)
                .Any(
                    group =>
                        group.Count() > 1))
        {
            throw new InvalidDataException(
                "duplicateSplineId");
        }

        var byId =
            entries.ToDictionary(
                item =>
                    item.Spline.SplineId);

        if (
            !byId.TryGetValue(
                selection.EntityId,
                out var source) ||
            source.Tile.X != selection.TileX ||
            source.Tile.Y != selection.TileY ||
            !string.Equals(
                source.Spline.SplinePath,
                selection.AssetPath,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "splineSourceChanged");
        }

        var states =
            byId.ToDictionary(
                pair => pair.Key,
                pair =>
                    new OmsiSplineLinkState(
                        pair.Key,
                        pair.Value
                            .Spline
                            .PreviousSplineId,
                        pair.Value
                            .Spline
                            .NextSplineId));

        var unlinkPlan =
            OmsiSplineLinkPlanner
                .Plan(
                    states,
                    source.Spline.SplineId,
                    source.Spline.PreviousSplineId,
                    source.Spline.NextSplineId,
                    -1,
                    -1);

        var affectedEntries =
            unlinkPlan.Keys
                .Where(
                    id =>
                        id !=
                            source.Spline.SplineId)
                .Select(
                    id =>
                        byId[id])
                .Append(source)
                .GroupBy(
                    item =>
                        item.Tile.RelativeMapPath,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        var writes =
            new List<PendingFileWrite>();

        var affectedPaths =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var group in affectedEntries)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            var tile =
                group.First().Tile;

            if (
                !OmsiMapPathResolver.TryResolveTilePath(
                    snapshot.Map.DirectoryPath,
                    tile.RelativeMapPath,
                    out var tilePath) ||
                !File.Exists(tilePath))
            {
                throw new InvalidDataException(
                    "splineDeleteTilePathInvalid");
            }

            var document =
                await OmsiConfigParser
                    .ParseFileAsync(
                        tilePath,
                        cancellationToken)
                    .ConfigureAwait(false);

            var linkEdits =
                group
                    .Where(
                        item =>
                            item.Spline.SplineId !=
                                source.Spline.SplineId &&
                            unlinkPlan.ContainsKey(
                                item.Spline.SplineId))
                    .Select(
                        item =>
                        {
                            var target =
                                unlinkPlan[
                                    item.Spline.SplineId];

                            return new OmsiSplineLinkEdit(
                                item.Spline
                                    .SourceSectionOrdinal,
                                item.Spline
                                    .SplinePath,
                                item.Spline
                                    .SplineId,
                                item.Spline
                                    .PreviousSplineId,
                                item.Spline
                                    .NextSplineId,
                                item.Spline
                                    .IsHeightSpline,
                                target
                                    .PreviousSplineId,
                                target
                                    .NextSplineId);
                        })
                    .ToArray();

            if (linkEdits.Length > 0)
            {
                var linkResult =
                    OmsiTileSplineLinkEditor
                        .ApplyLinks(
                            document,
                            linkEdits);

                document =
                    OmsiConfigParser
                        .ParseBytes(
                            linkResult.Bytes);
            }

            byte[] bytes;

            if (
                group.Any(
                    item =>
                        item.Spline.SplineId ==
                            source.Spline.SplineId))
            {
                bytes =
                    OmsiTileSplineDeleter
                        .Remove(
                            document,
                            source.Spline
                                .SourceSectionOrdinal,
                            source.Spline
                                .SplinePath,
                            source.Spline
                                .SplineId,
                            source.Spline
                                .PreviousSplineId,
                            source.Spline
                                .NextSplineId,
                            source.Spline
                                .IsHeightSpline)
                        .Bytes;
            }
            else
            {
                bytes =
                    document.ToBytes();
            }

            writes.Add(
                new PendingFileWrite(
                    tilePath,
                    CreateNativeBackupPath(
                        snapshot.Map.DirectoryPath,
                        tilePath),
                    bytes));

            affectedPaths.Add(
                tile.RelativeMapPath);
        }

        await SafeFileTransaction
            .WriteAllAsync(
                writes,
                cancellationToken)
            .ConfigureAwait(false);

        var refreshed =
            new List<NativeLoadedTile>(
                snapshot.Tiles.Count);

        foreach (var loaded in snapshot.Tiles)
        {
            if (
                !affectedPaths.Contains(
                    loaded.Reference
                        .RelativeMapPath))
            {
                refreshed.Add(
                    loaded);

                continue;
            }

            if (
                !OmsiMapPathResolver.TryResolveTilePath(
                    snapshot.Map.DirectoryPath,
                    loaded.Reference.RelativeMapPath,
                    out var tilePath))
            {
                throw new InvalidDataException(
                    "splineDeleteReloadPathInvalid");
            }

            refreshed.Add(
                new NativeLoadedTile(
                    loaded.Reference,
                    await _tileReader
                        .ReadContentAsync(
                            tilePath,
                            cancellationToken)
                        .ConfigureAwait(false)));
        }

        CurrentMap =
            snapshot with
            {
                Tiles =
                    refreshed.ToArray()
            };

        return CurrentMap;
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

        var assetIndexPath =
            GetAssetIndexPath(
                normalized);

        _assetIndex =
            new OmsiAssetIndex(
                assetIndexPath);

        await _assetIndex
            .InitializeAsync(
                cancellationToken)
            .ConfigureAwait(false);

        OmsiRootPath = normalized;
        Maps = maps;
        CurrentMap = null;
        _pendingTransforms.Clear();

        return maps;
    }

    public async Task<
        IReadOnlyList<OmsiMapDescriptor>>
        RefreshMapCatalogAsync(
            CancellationToken cancellationToken =
                default)
    {
        var root =
            OmsiRootPath ??
            throw new InvalidOperationException(
                "Selecione primeiro a instalação do OMSI 2.");

        var maps =
            await new OmsiMapCatalog()
                .DiscoverAsync(
                    root,
                    cancellationToken)
                .ConfigureAwait(false);

        Maps =
            maps;

        return maps;
    }

    public Task<NativeMapSnapshot>
        OpenMapAsync(
            string mapDirectory,
            CancellationToken cancellationToken =
                default) =>
        OpenMapAsync(
            mapDirectory,
            loadFullMap: true,
            cancellationToken);

    public async Task<NativeMapSnapshot>
        OpenMapAsync(
            string mapDirectory,
            bool loadFullMap,
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
                StringComparison.OrdinalIgnoreCase))
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
            loadFullMap
                ? map.Tiles
                : initialTile is null
                    ? Array.Empty<
                        OmsiTileReference>()
                    : OmsiTileRegionSelector
                        .Select(
                            map.Tiles,
                            initialTile.X,
                            initialTile.Y,
                            radius: 1);

        var snapshot =
            await LoadSnapshotAsync(
                    map,
                    initialTile,
                    selectedTiles,
                    cancellationToken)
                .ConfigureAwait(false);

        CurrentMap = snapshot;
        _pendingTransforms.Clear();

        return snapshot;
    }

    public async Task<NativeMapSnapshot>
        LoadFullMapAsync(
            CancellationToken cancellationToken =
                default)
    {
        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        if (_pendingTransforms.Count > 0)
        {
            throw new InvalidOperationException(
                "savePendingBeforeMapLoadModeChange");
        }

        var loaded =
            await LoadSnapshotAsync(
                    snapshot.Map,
                    snapshot.ActiveTile,
                    snapshot.Map.Tiles,
                    cancellationToken)
                .ConfigureAwait(false);

        CurrentMap =
            loaded;

        return loaded;
    }

    public async Task<NativeMapSnapshot>
        LoadRegionAsync(
            int centerX,
            int centerY,
            int radius = 1,
            CancellationToken cancellationToken =
                default)
    {
        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        if (_pendingTransforms.Count > 0)
        {
            throw new InvalidOperationException(
                "savePendingBeforeMapLoadModeChange");
        }

        var center =
            snapshot.Map.Tiles
                .FirstOrDefault(
                    tile =>
                        tile.X ==
                            centerX &&
                        tile.Y ==
                            centerY)
            ?? throw new InvalidDataException(
                "mapTileNotFound");

        var selected =
            OmsiTileRegionSelector
                .Select(
                    snapshot.Map.Tiles,
                    centerX,
                    centerY,
                    radius);

        var loaded =
            await LoadSnapshotAsync(
                    snapshot.Map,
                    center,
                    selected,
                    cancellationToken)
                .ConfigureAwait(false);

        CurrentMap =
            loaded;

        return loaded;
    }

    public NativeMapSnapshot
        SetActiveTile(
            int tileX,
            int tileY)
    {
        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        var tile =
            snapshot.Map.Tiles
                .FirstOrDefault(
                    item =>
                        item.X == tileX &&
                        item.Y == tileY)
            ?? throw new InvalidDataException(
                "mapTileNotFound");

        CurrentMap =
            snapshot with
            {
                ActiveTile =
                    tile
            };

        return CurrentMap;
    }

    private async Task<NativeMapSnapshot>
        LoadSnapshotAsync(
            OmsiMapDescriptor map,
            OmsiTileReference? activeTile,
            IReadOnlyList<
                OmsiTileReference> selectedTiles,
            CancellationToken cancellationToken)
    {
        using var gate =
            new SemaphoreSlim(
                initialCount:
                    Math.Clamp(
                        Environment
                            .ProcessorCount,
                        2,
                        6));

        var loadTasks =
            selectedTiles
                .Select(
                    async tile =>
                    {
                        await gate
                            .WaitAsync(
                                cancellationToken)
                            .ConfigureAwait(false);

                        try
                        {
                            return await LoadTileAsync(
                                    map,
                                    tile,
                                    cancellationToken)
                                .ConfigureAwait(false);
                        }
                        finally
                        {
                            gate.Release();
                        }
                    })
                .ToArray();

        var loadedTiles =
            await Task.WhenAll(
                    loadTasks)
                .ConfigureAwait(false);

        return new NativeMapSnapshot(
            map,
            activeTile,
            loadedTiles
                .Where(
                    tile =>
                        tile is not null)
                .Select(
                    tile => tile!)
                .ToArray());
    }

    private static async Task
        CreateNewFileAtomicallyAsync(
            string targetPath,
            byte[] bytes,
            CancellationToken cancellationToken)
    {
        var fullTarget =
            Path.GetFullPath(
                targetPath);

        var directory =
            Path.GetDirectoryName(
                fullTarget) ??
            throw new InvalidOperationException(
                "targetDirectoryMissing");

        Directory.CreateDirectory(
            directory);

        if (File.Exists(fullTarget))
        {
            throw new IOException(
                "Target file already exists.");
        }

        var tempPath =
            Path.Combine(
                directory,
                $".{Path.GetFileName(fullTarget)}.mapstudio-{Guid.NewGuid():N}.tmp");

        try
        {
            await File.WriteAllBytesAsync(
                    tempPath,
                    bytes,
                    cancellationToken)
                .ConfigureAwait(false);

            File.Move(
                tempPath,
                fullTarget,
                overwrite: false);
        }
        finally
        {
            try
            {
                if (File.Exists(
                        tempPath))
                {
                    File.Delete(
                        tempPath);
                }
            }
            catch
            {
                // Best effort cleanup.
            }
        }
    }

    private static string CreateNativeBackupPath(
        string mapDirectory,
        string targetPath)
    {
        var backupDirectory =
            Path.Combine(
                mapDirectory,
                ".mapstudio-backups");

        return Path.Combine(
            backupDirectory,
            Path.GetFileName(targetPath) +
            "." +
            DateTime.UtcNow.ToString(
                "yyyyMMdd-HHmmssfff") +
            "." +
            Guid.NewGuid().ToString("N") +
            ".bak");
    }

    private static string GetAssetIndexPath(
        string omsiRoot)
    {
        var hash =
            Convert.ToHexString(
                System.Security.Cryptography
                    .SHA256.HashData(
                        System.Text.Encoding
                            .UTF8.GetBytes(
                                omsiRoot)))
                .Substring(
                    0,
                    16);

        var cacheRoot =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment
                        .SpecialFolder
                        .LocalApplicationData),
                "OMSI Map Studio",
                "cache",
                "native");

        return
            Path.Combine(
                cacheRoot,
                $"assets-{hash}.sqlite");
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
