using MapStudio.Core.Generation.Terrain;
using MapStudio.Core.IO;
using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Indexing;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Scenery;
using MapStudio.Core.Omsi.Splines;
using MapStudio.Core.Omsi.Timetables;
using MapStudio.Core.Workspace;
using MapStudio.Renderer.Viewport;
using MapStudio.Renderer.Picking;
using System.Globalization;
using System.Numerics;
using System.Text.Json;

namespace MapStudio.Native.Services;

public enum NativeContentRootKind
{
    None = 0,
    StandaloneWorkspace = 1,
    OmsiInstallation = 2
}

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

public sealed record NativeSplineSplitResult(
    NativeMapSnapshot Snapshot,
    int FirstSplineId,
    int SecondSplineId);

public sealed record NativeSplineFlowToggleResult(
    NativeMapSnapshot Snapshot,
    string SplinePath,
    bool Reversed,
    int ReversedVehiclePathCount);

public sealed record NativeDeleteBackupEntry(
    string TargetPath,
    string BackupPath);

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

    private static readonly HttpClient
        OpenStreetMapHttpClient =
            CreateOpenStreetMapHttpClient();

    private static readonly HttpClient
        OpenMeteoHttpClient =
            new()
            {
                Timeout =
                    TimeSpan.FromSeconds(
                        30)
            };

    private static HttpClient
        CreateOpenStreetMapHttpClient()
    {
        var client =
            new HttpClient
            {
                Timeout =
                    TimeSpan.FromSeconds(
                        30)
            };

        client.DefaultRequestHeaders
            .UserAgent
            .ParseAdd(
                "OMSI-Map-Studio/0.2 (+https://github.com/MichaelPriest/OMSI-Map-Studio)");

        return client;
    }

    private readonly OmsiTileReader _tileReader =
        new();

    private readonly OmsiTileContentCache
        _tileContentCache =
            new(
                capacity: 32);

    private readonly Dictionary<
        string,
        NativePendingTransformEdit>
        _pendingTransforms =
            new(
                StringComparer.OrdinalIgnoreCase);

    private OmsiAssetIndex?
        _assetIndex;

    private readonly
        MapStudioWorkspaceBootstrapper
        _workspaceBootstrapper =
            new();

    public string? OmsiRootPath { get; private set; }

    public NativeContentRootKind
        ContentRootKind { get; private set; }

    public bool IsStandaloneWorkspace =>
        ContentRootKind ==
        NativeContentRootKind
            .StandaloneWorkspace;

    public IReadOnlyList<OmsiMapDescriptor> Maps { get; private set; } =
        Array.Empty<OmsiMapDescriptor>();

    public NativeMapSnapshot? CurrentMap { get; private set; }

    public string? LastBackupDirectory { get; private set; }

    public IReadOnlyList<NativeDeleteBackupEntry>
        LastDeleteBackupEntries
    {
        get;
        private set;
    } =
        Array.Empty<NativeDeleteBackupEntry>();

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
                "Selecione primeiro o Workspace Map Studio ou uma instalação do OMSI 2.");

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
                "Selecione primeiro o Workspace Map Studio ou uma instalação do OMSI 2.");

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
                "Selecione primeiro o Workspace Map Studio ou uma instalação do OMSI 2.");

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

    private static bool HasPngSignature(
        string path)
    {
        try
        {
            using var stream =
                new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite);

            Span<byte> signature =
                stackalloc byte[8];

            return
                stream.Read(
                    signature) ==
                    signature.Length &&
                HasPngSignature(
                    signature);
        }
        catch
        {
            return false;
        }
    }

    private static bool HasPngSignature(
        ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 8 &&
        bytes[0] == 0x89 &&
        bytes[1] == 0x50 &&
        bytes[2] == 0x4E &&
        bytes[3] == 0x47 &&
        bytes[4] == 0x0D &&
        bytes[5] == 0x0A &&
        bytes[6] == 0x1A &&
        bytes[7] == 0x0A;

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
                OmsiTileGrid.TileSize +
            georeference.AnchorX;

        var anchorWorldZ =
            georeference.AnchorTileY *
                OmsiTileGrid.TileSize +
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

    public async Task<NativeGoogleMapReference>
        LoadCartoReferenceAsync(
            string apiKey,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            apiKey);

        var stage =
            "georeference-load";

        try
        {
            var snapshot =
                CurrentMap ??
                throw new InvalidOperationException(
                    "Nenhum mapa OMSI está aberto.");

            var georeference =
                await LoadMapGeoreferenceAsync(
                        cancellationToken)
                    .ConfigureAwait(false)
                ?? throw new InvalidDataException(
                    "mapGeoreferenceRequired");

            stage =
                "georeference-validation";

            ValidateGeoreference(
                georeference);

            var latitude =
                Math.Clamp(
                    georeference.Latitude,
                    -85.05112878,
                    85.05112878);

            var longitude =
                Math.Clamp(
                    georeference.Longitude,
                    -180.0,
                    180.0);

            var zoom =
                Math.Clamp(
                    georeference.Zoom,
                    0,
                    19);

            stage =
                "mercator-position";

            var webTileCount =
                1 <<
                zoom;

            var worldPixels =
                256.0 *
                webTileCount;

            var latitudeRadians =
                latitude *
                Math.PI /
                180.0;

            var anchorPixelX =
                (
                    longitude +
                    180.0
                ) /
                360.0 *
                worldPixels;

            var mercator =
                Math.Log(
                    Math.Tan(
                        latitudeRadians) +
                    1.0 /
                    Math.Cos(
                        latitudeRadians));

            var anchorPixelY =
                (
                    1.0 -
                    mercator /
                    Math.PI
                ) /
                2.0 *
                worldPixels;

            var metersPerPixel =
                156543.03392804097 *
                Math.Cos(
                    latitudeRadians) /
                Math.Pow(
                    2,
                    zoom);

            var georeferenceWorldX =
                georeference.AnchorTileX *
                    OmsiTileGrid.TileSize +
                georeference.AnchorX;

            var georeferenceWorldZ =
                georeference.AnchorTileY *
                    OmsiTileGrid.TileSize +
                georeference.AnchorY;

            var coverageTiles =
                snapshot.Tiles
                    .Select(
                        tile =>
                            tile.Reference)
                    .ToArray();

            if (
                coverageTiles.Length ==
                    0 &&
                snapshot.ActiveTile is
                    { } activeOnly)
            {
                coverageTiles =
                    [
                        activeOnly
                    ];
            }

            if (
                coverageTiles.Length ==
                0)
            {
                coverageTiles =
                    snapshot.Map.Tiles
                        .Take(
                            1)
                        .ToArray();
            }

            if (
                coverageTiles.Length ==
                0)
            {
                throw new InvalidDataException(
                    "mapReferenceCoverageRequired");
            }

            stage =
                "mosaic-coverage";

            var minimumWorldX =
                coverageTiles.Min(
                    tile =>
                        tile.X *
                        OmsiTileGrid.TileSize);

            var maximumWorldX =
                coverageTiles.Max(
                    tile =>
                        (
                            tile.X +
                            1
                        ) *
                        OmsiTileGrid.TileSize);

            var minimumWorldZ =
                coverageTiles.Min(
                    tile =>
                        tile.Y *
                        OmsiTileGrid.TileSize);

            var maximumWorldZ =
                coverageTiles.Max(
                    tile =>
                        (
                            tile.Y +
                            1
                        ) *
                        OmsiTileGrid.TileSize);

            var minimumPixelX =
                anchorPixelX +
                (
                    minimumWorldX -
                    georeferenceWorldX
                ) /
                metersPerPixel;

            var maximumPixelX =
                anchorPixelX +
                (
                    maximumWorldX -
                    georeferenceWorldX
                ) /
                metersPerPixel;

            var minimumPixelY =
                anchorPixelY +
                (
                    minimumWorldZ -
                    georeferenceWorldZ
                ) /
                metersPerPixel;

            var maximumPixelY =
                anchorPixelY +
                (
                    maximumWorldZ -
                    georeferenceWorldZ
                ) /
                metersPerPixel;

            var minimumTileX =
                Math.Clamp(
                    (int)Math.Floor(
                        Math.Min(
                            minimumPixelX,
                            maximumPixelX) /
                        256.0),
                    0,
                    webTileCount -
                        1);

            var maximumTileX =
                Math.Clamp(
                    (int)Math.Floor(
                        Math.Max(
                            minimumPixelX,
                            maximumPixelX) /
                        256.0),
                    0,
                    webTileCount -
                        1);

            var minimumTileY =
                Math.Clamp(
                    (int)Math.Floor(
                        Math.Min(
                            minimumPixelY,
                            maximumPixelY) /
                        256.0),
                    0,
                    webTileCount -
                        1);

            var maximumTileY =
                Math.Clamp(
                    (int)Math.Floor(
                        Math.Max(
                            minimumPixelY,
                            maximumPixelY) /
                        256.0),
                    0,
                    webTileCount -
                        1);

            var fullMapCoverage =
                snapshot.Map.Tiles.Count >
                    1 &&
                snapshot.Tiles.Count >=
                    snapshot.Map.Tiles.Count;

            double referenceFocusWorldX;
            double referenceFocusWorldZ;

            if (fullMapCoverage)
            {
                referenceFocusWorldX =
                    (
                        minimumWorldX +
                        maximumWorldX
                    ) *
                    0.5;

                referenceFocusWorldZ =
                    (
                        minimumWorldZ +
                        maximumWorldZ
                    ) *
                    0.5;
            }
            else
            {
                var activeTile =
                    snapshot.ActiveTile ??
                    coverageTiles[0];

                referenceFocusWorldX =
                    (
                        activeTile.X +
                        0.5
                    ) *
                    OmsiTileGrid.TileSize;

                referenceFocusWorldZ =
                    (
                        activeTile.Y +
                        0.5
                    ) *
                    OmsiTileGrid.TileSize;
            }

            var referenceFocusPixelX =
                anchorPixelX +
                (
                    referenceFocusWorldX -
                    georeferenceWorldX
                ) /
                metersPerPixel;

            var referenceFocusPixelY =
                anchorPixelY +
                (
                    referenceFocusWorldZ -
                    georeferenceWorldZ
                ) /
                metersPerPixel;

            var activeWebTileX =
                referenceFocusPixelX /
                256.0;

            var activeWebTileY =
                referenceFocusPixelY /
                256.0;

            const int maximumResidentTiles =
                81;

            var candidates =
                new List<(
                    int TileX,
                    int TileY,
                    double DistanceSquared)>();

            for (
                var tileY = minimumTileY;
                tileY <= maximumTileY;
                tileY++)
            {
                for (
                    var tileX = minimumTileX;
                    tileX <= maximumTileX;
                    tileX++)
                {
                    var dx =
                        tileX +
                        0.5 -
                        activeWebTileX;

                    var dy =
                        tileY +
                        0.5 -
                        activeWebTileY;

                    candidates.Add(
                        (
                            tileX,
                            tileY,
                            dx *
                                dx +
                            dy *
                                dy
                        ));
                }
            }

            var selected =
                candidates
                    .OrderBy(
                        candidate =>
                            candidate
                                .DistanceSquared)
                    .Take(
                        maximumResidentTiles)
                    .ToArray();

            if (selected.Length == 0)
            {
                throw new InvalidDataException(
                    "cartoReferenceEmptyMosaic");
            }

            stage =
                "cache-path";

            var cacheRoot =
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder
                            .LocalApplicationData),
                    "OMSI Map Studio",
                    "reference-cache",
                    "carto-keyed-v4",
                    zoom.ToString(
                        CultureInfo.InvariantCulture));

            Directory.CreateDirectory(
                cacheRoot);

            stage =
                "mosaic-download";

            using var cartoClient =
                CreateOpenStreetMapHttpClient();

            using var downloadGate =
                new SemaphoreSlim(
                    initialCount:
                        8,
                    maxCount:
                        8);

            var normalizedKey =
                apiKey.Trim();

            var tasks =
                selected.Select(
                    async candidate =>
                    {
                        await downloadGate
                            .WaitAsync(
                                cancellationToken)
                            .ConfigureAwait(false);

                        try
                        {
                            var tileDirectory =
                                Path.Combine(
                                    cacheRoot,
                                    candidate.TileX
                                        .ToString(
                                            CultureInfo.InvariantCulture));

                            Directory.CreateDirectory(
                                tileDirectory);

                            var path =
                                Path.Combine(
                                    tileDirectory,
                                    $"{candidate.TileY}.png");

                            var cacheValid =
                                File.Exists(
                                    path) &&
                                DateTime.UtcNow -
                                    File.GetLastWriteTimeUtc(
                                        path) <
                                TimeSpan.FromDays(
                                    7) &&
                                HasPngSignature(
                                    path);

                            if (!cacheValid)
                            {
                                var uri =
                                    new Uri(
                                        $"https://basemaps.cartocdn.com/rastertiles/voyager/{zoom}/{candidate.TileX}/{candidate.TileY}.png?key={Uri.EscapeDataString(normalizedKey)}",
                                        UriKind.Absolute);

                                byte[] bytes;

                                try
                                {
                                    bytes =
                                        await cartoClient
                                            .GetByteArrayAsync(
                                                uri,
                                                cancellationToken)
                                            .ConfigureAwait(false);
                                }
                                catch (
                                    NullReferenceException)
                                {
                                    using var fallbackClient =
                                        CreateOpenStreetMapHttpClient();

                                    bytes =
                                        await fallbackClient
                                            .GetByteArrayAsync(
                                                uri,
                                                cancellationToken)
                                            .ConfigureAwait(false);
                                }

                                if (
                                    bytes is null ||
                                    bytes.Length <
                                        64 ||
                                    bytes.LongLength >
                                        8L *
                                        1024L *
                                        1024L ||
                                    !HasPngSignature(
                                        bytes))
                                {
                                    throw new InvalidDataException(
                                        $"cartoReferenceInvalidPngPayload:{zoom}/{candidate.TileX}/{candidate.TileY}");
                                }

                                await File.WriteAllBytesAsync(
                                        path,
                                        bytes,
                                        cancellationToken)
                                    .ConfigureAwait(false);
                            }

                            var centerPixelX =
                                candidate.TileX *
                                    256.0 +
                                128.0;

                            var centerPixelY =
                                candidate.TileY *
                                    256.0 +
                                128.0;

                            return new NativeMapReferenceTile(
                                path,
                                256,
                                256,
                                metersPerPixel,
                                georeferenceWorldX +
                                    (
                                        centerPixelX -
                                        anchorPixelX
                                    ) *
                                    metersPerPixel,
                                georeferenceWorldZ +
                                    (
                                        centerPixelY -
                                        anchorPixelY
                                    ) *
                                    metersPerPixel,
                                candidate.TileX,
                                candidate.TileY);
                        }
                        finally
                        {
                            downloadGate
                                .Release();
                        }
                    })
                    .ToArray();

            var tiles =
                await Task.WhenAll(
                        tasks)
                    .ConfigureAwait(false);

            stage =
                "reference-metadata";

            var selectedMinimumX =
                selected.Min(
                    candidate =>
                        candidate.TileX);

            var selectedMaximumX =
                selected.Max(
                    candidate =>
                        candidate.TileX);

            var selectedMinimumY =
                selected.Min(
                    candidate =>
                        candidate.TileY);

            var selectedMaximumY =
                selected.Max(
                    candidate =>
                        candidate.TileY);

            var mosaicWidth =
                checked(
                    (
                        selectedMaximumX -
                        selectedMinimumX +
                        1
                    ) *
                    256);

            var mosaicHeight =
                checked(
                    (
                        selectedMaximumY -
                        selectedMinimumY +
                        1
                    ) *
                    256);

            var mosaicCenterPixelX =
                (
                    selectedMinimumX *
                        256.0 +
                    (
                        selectedMaximumX +
                        1
                    ) *
                        256.0
                ) *
                0.5;

            var mosaicCenterPixelY =
                (
                    selectedMinimumY *
                        256.0 +
                    (
                        selectedMaximumY +
                        1
                    ) *
                        256.0
                ) *
                0.5;

            var primary =
                tiles[0];

            return new NativeGoogleMapReference(
                primary.ImagePath,
                mosaicWidth,
                mosaicHeight,
                metersPerPixel,
                georeferenceWorldX +
                    (
                        mosaicCenterPixelX -
                        anchorPixelX
                    ) *
                    metersPerPixel,
                georeferenceWorldZ +
                    (
                        mosaicCenterPixelY -
                        anchorPixelY
                    ) *
                    metersPerPixel,
                latitude,
                longitude,
                zoom,
                "roadmap",
                "© OpenStreetMap contributors · © CARTO")
            {
                Tiles =
                    tiles
            };
        }
        catch (NullReferenceException exception)
        {
            TryWriteCartoReferenceDiagnostic(
                stage,
                exception);

            throw new InvalidOperationException(
                $"cartoReferenceNull:{stage}",
                exception);
        }
        catch (Exception exception)
        {
            TryWriteCartoReferenceDiagnostic(
                stage,
                exception);

            throw;
        }
    }

    private static void
        TryWriteCartoReferenceDiagnostic(
            string stage,
            Exception exception)
    {
        try
        {
            var logDirectory =
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder
                            .LocalApplicationData),
                    "OMSI Map Studio",
                    "logs");

            Directory.CreateDirectory(
                logDirectory);

            var logPath =
                Path.Combine(
                    logDirectory,
                    "carto-reference.log");

            File.AppendAllText(
                logPath,
                $"[{DateTimeOffset.Now:O}] stage={stage}{Environment.NewLine}" +
                exception +
                Environment.NewLine +
                Environment.NewLine);
        }
        catch
        {
            // Diagnostics must never replace the original CARTO failure.
        }
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
                OmsiTileGrid.TileSize +
            georeference.AnchorX;

        var anchorWorldY =
            georeference.AnchorTileY *
                OmsiTileGrid.TileSize +
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
                OmsiTileGrid.TileSize /
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
                    OmsiTileGrid.TileSize /
                    (
                        sampleCount -
                        1
                    );

                var worldX =
                    tileX *
                        OmsiTileGrid.TileSize +
                    localX;

                var worldY =
                    tileY *
                        OmsiTileGrid.TileSize +
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

    public async Task<NativeGoogleElevationGrid>
        LoadOpenMeteoElevationGridAsync(
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
                "invalidOpenMeteoElevationRequest");
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
                OmsiTileGrid.TileSize +
            georeference.AnchorX;

        var anchorWorldY =
            georeference.AnchorTileY *
                OmsiTileGrid.TileSize +
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
                OmsiTileGrid.TileSize /
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
                    OmsiTileGrid.TileSize /
                    (
                        sampleCount -
                        1
                    );

                var worldX =
                    tileX *
                        OmsiTileGrid.TileSize +
                    localX;

                var worldY =
                    tileY *
                        OmsiTileGrid.TileSize +
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
                        "invalidOpenMeteoElevationRequest");
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
            100;

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
                    .ToArray();

            var latitudes =
                string.Join(
                    ",",
                    batch.Select(
                        coordinate =>
                            coordinate.Latitude
                                .ToString(
                                    "G17",
                                    CultureInfo.InvariantCulture)));

            var longitudes =
                string.Join(
                    ",",
                    batch.Select(
                        coordinate =>
                            coordinate.Longitude
                                .ToString(
                                    "G17",
                                    CultureInfo.InvariantCulture)));

            var uri =
                "https://customer-api.open-meteo.com/v1/elevation" +
                "?latitude=" +
                Uri.EscapeDataString(
                    latitudes) +
                "&longitude=" +
                Uri.EscapeDataString(
                    longitudes) +
                "&apikey=" +
                Uri.EscapeDataString(
                    apiKey.Trim());

            using var response =
                await OpenMeteoHttpClient
                    .GetAsync(
                        uri,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (!response
                .IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"openMeteoElevationHttp:{(int)response.StatusCode}");
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
                    "elevation",
                    out var values) ||
                values.ValueKind !=
                    JsonValueKind.Array ||
                values.GetArrayLength() !=
                    batch.Length)
            {
                throw new InvalidDataException(
                    "openMeteoElevationGridError");
            }

            foreach (
                var elevation in
                    values.EnumerateArray())
            {
                if (
                    !elevation.TryGetDouble(
                        out var value) ||
                    !double.IsFinite(
                        value))
                {
                    throw new InvalidDataException(
                        "openMeteoElevationGridError");
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
                "openMeteoElevationGridError");
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

    public Task<NativeTerrainElevationApplyResult>
        ApplyLocalTerrainElevationGridAsync(
            int tileX,
            int tileY,
            MapStudioElevationGrid grid,
            double verticalOffset,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentNullException.ThrowIfNull(
            grid);

        return ApplyTerrainElevationGridAsync(
            new NativeGoogleElevationGrid(
                tileX,
                tileY,
                grid.Rows,
                grid.Columns,
                grid.Elevations,
                grid.MinimumElevation,
                grid.MaximumElevation,
                0,
                0),
            verticalOffset,
            cancellationToken);
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

    public async Task<NativeWaterUpdateResult>
        SetTileWaterAsync(
            int tileX,
            int tileY,
            OmsiWaterGrid water,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentNullException.ThrowIfNull(
            water);

        var waterBytes =
            OmsiWaterWriter
                .Write(
                    water);

        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        if (
            _pendingTransforms.Count >
                0)
        {
            throw new InvalidOperationException(
                "savePendingBeforeWaterEdit");
        }

        var tile =
            snapshot.Map.Tiles
                .FirstOrDefault(
                    candidate =>
                        candidate.X ==
                            tileX &&
                        candidate.Y ==
                            tileY)
            ?? throw new InvalidDataException(
                "unknownTile");

        if (
            !OmsiMapPathResolver
                .TryResolveTilePath(
                    snapshot.Map
                        .DirectoryPath,
                    tile.RelativeMapPath,
                    out var tilePath) ||
            !File.Exists(
                tilePath))
        {
            throw new InvalidDataException(
                "waterTilePathInvalid");
        }

        var document =
            await OmsiConfigParser
                .ParseFileAsync(
                    tilePath,
                    cancellationToken)
                .ConfigureAwait(false);

        var tileBytes =
            OmsiTileWaterMarkerEditor
                .EnsurePresent(
                    document);

        var waterPath =
            tilePath +
            ".water";

        var waterExisted =
            File.Exists(
                waterPath);

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
                $"-water-{tileX}-{tileY}-" +
                Guid.NewGuid()
                    .ToString("N"));

        var tileRelative =
            Path.GetRelativePath(
                snapshot.Map
                    .DirectoryPath,
                tilePath);

        var waterRelative =
            Path.GetRelativePath(
                snapshot.Map
                    .DirectoryPath,
                waterPath);

        if (
            !IsSafeRelativePath(
                tileRelative) ||
            !IsSafeRelativePath(
                waterRelative))
        {
            throw new InvalidDataException(
                "invalidWaterPath");
        }

        var backupTile =
            Path.Combine(
                backupRoot,
                tileRelative);

        var backupWater =
            Path.Combine(
                backupRoot,
                waterRelative);

        string? temporaryWaterPath =
            null;

        try
        {
            if (waterExisted)
            {
                await SafeFileTransaction
                    .WriteAllAsync(
                        [
                            new PendingFileWrite(
                                tilePath,
                                backupTile,
                                tileBytes),
                            new PendingFileWrite(
                                waterPath,
                                backupWater,
                                waterBytes)
                        ],
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                var tileDirectory =
                    Path.GetDirectoryName(
                        tilePath) ??
                    snapshot.Map
                        .DirectoryPath;

                temporaryWaterPath =
                    Path.Combine(
                        tileDirectory,
                        $".{Path.GetFileName(waterPath)}.mapstudio-{Guid.NewGuid():N}.tmp");

                await File
                    .WriteAllBytesAsync(
                        temporaryWaterPath,
                        waterBytes,
                        cancellationToken)
                    .ConfigureAwait(false);

                await SafeFileTransaction
                    .WriteAllAsync(
                        [
                            new PendingFileWrite(
                                tilePath,
                                backupTile,
                                tileBytes)
                        ],
                        cancellationToken)
                    .ConfigureAwait(false);

                File.Move(
                    temporaryWaterPath,
                    waterPath,
                    overwrite:
                        false);

                temporaryWaterPath =
                    null;
            }

            var refreshed =
                await _tileReader
                    .ReadContentAsync(
                        tilePath,
                        cancellationToken)
                    .ConfigureAwait(false);

            var updatedSnapshot =
                ReplaceLoadedTileContent(
                    snapshot,
                    tile,
                    refreshed);

            CurrentMap =
                updatedSnapshot;

            LastBackupDirectory =
                backupRoot;

            return new NativeWaterUpdateResult(
                updatedSnapshot,
                tile,
                refreshed.Water,
                backupRoot);
        }
        catch
        {
            try
            {
                if (
                    File.Exists(
                        backupTile))
                {
                    File.Copy(
                        backupTile,
                        tilePath,
                        overwrite:
                            true);
                }
            }
            catch
            {
            }

            try
            {
                if (waterExisted)
                {
                    if (
                        File.Exists(
                            backupWater))
                    {
                        File.Copy(
                            backupWater,
                            waterPath,
                            overwrite:
                                true);
                    }
                }
                else if (
                    File.Exists(
                        waterPath))
                {
                    File.Delete(
                        waterPath);
                }
            }
            catch
            {
            }

            throw;
        }
        finally
        {
            if (
                !string.IsNullOrWhiteSpace(
                    temporaryWaterPath))
            {
                try
                {
                    if (
                        File.Exists(
                            temporaryWaterPath))
                    {
                        File.Delete(
                            temporaryWaterPath);
                    }
                }
                catch
                {
                }
            }
        }
    }

    public async Task<NativeWaterUpdateResult>
        RemoveTileWaterAsync(
            int tileX,
            int tileY,
            CancellationToken cancellationToken =
                default)
    {
        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        if (
            _pendingTransforms.Count >
                0)
        {
            throw new InvalidOperationException(
                "savePendingBeforeWaterEdit");
        }

        var tile =
            snapshot.Map.Tiles
                .FirstOrDefault(
                    candidate =>
                        candidate.X ==
                            tileX &&
                        candidate.Y ==
                            tileY)
            ?? throw new InvalidDataException(
                "unknownTile");

        if (
            !OmsiMapPathResolver
                .TryResolveTilePath(
                    snapshot.Map
                        .DirectoryPath,
                    tile.RelativeMapPath,
                    out var tilePath) ||
            !File.Exists(
                tilePath))
        {
            throw new InvalidDataException(
                "waterTilePathInvalid");
        }

        var document =
            await OmsiConfigParser
                .ParseFileAsync(
                    tilePath,
                    cancellationToken)
                .ConfigureAwait(false);

        var markerPresent =
            document.FindFirstSection(
                "water") is not null;

        var waterPath =
            tilePath +
            ".water";

        var waterExists =
            File.Exists(
                waterPath);

        if (
            !markerPresent &&
            !waterExists)
        {
            return new NativeWaterUpdateResult(
                snapshot,
                tile,
                null,
                string.Empty);
        }

        var tileBytes =
            OmsiTileWaterMarkerEditor
                .Remove(
                    document);

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
                $"-remove-water-{tileX}-{tileY}-" +
                Guid.NewGuid()
                    .ToString("N"));

        var tileRelative =
            Path.GetRelativePath(
                snapshot.Map
                    .DirectoryPath,
                tilePath);

        var waterRelative =
            Path.GetRelativePath(
                snapshot.Map
                    .DirectoryPath,
                waterPath);

        if (
            !IsSafeRelativePath(
                tileRelative) ||
            !IsSafeRelativePath(
                waterRelative))
        {
            throw new InvalidDataException(
                "invalidWaterPath");
        }

        var backupTile =
            Path.Combine(
                backupRoot,
                tileRelative);

        var backupWater =
            Path.Combine(
                backupRoot,
                waterRelative);

        try
        {
            if (waterExists)
            {
                Directory.CreateDirectory(
                    Path.GetDirectoryName(
                        backupWater)!);

                File.Copy(
                    waterPath,
                    backupWater,
                    overwrite:
                        false);
            }

            await SafeFileTransaction
                .WriteAllAsync(
                    [
                        new PendingFileWrite(
                            tilePath,
                            backupTile,
                            tileBytes)
                    ],
                    cancellationToken)
                .ConfigureAwait(false);

            if (waterExists)
            {
                File.Delete(
                    waterPath);
            }

            var refreshed =
                await _tileReader
                    .ReadContentAsync(
                        tilePath,
                        cancellationToken)
                    .ConfigureAwait(false);

            var updatedSnapshot =
                ReplaceLoadedTileContent(
                    snapshot,
                    tile,
                    refreshed);

            CurrentMap =
                updatedSnapshot;

            LastBackupDirectory =
                backupRoot;

            return new NativeWaterUpdateResult(
                updatedSnapshot,
                tile,
                null,
                backupRoot);
        }
        catch
        {
            try
            {
                if (
                    File.Exists(
                        backupTile))
                {
                    File.Copy(
                        backupTile,
                        tilePath,
                        overwrite:
                            true);
                }
            }
            catch
            {
            }

            try
            {
                if (
                    waterExists &&
                    File.Exists(
                        backupWater))
                {
                    Directory.CreateDirectory(
                        Path.GetDirectoryName(
                            waterPath)!);

                    File.Copy(
                        backupWater,
                        waterPath,
                        overwrite:
                            true);
                }
            }
            catch
            {
            }

            throw;
        }
    }

    private static NativeMapSnapshot
        ReplaceLoadedTileContent(
            NativeMapSnapshot snapshot,
            OmsiTileReference tile,
            OmsiTileContent content)
    {
        if (
            !snapshot.Tiles.Any(
                loaded =>
                    loaded.Reference.X ==
                        tile.X &&
                    loaded.Reference.Y ==
                        tile.Y))
        {
            return snapshot;
        }

        return snapshot with
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
                                    content)
                                : loaded)
                    .ToArray()
        };
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
                "Nenhuma fonte de conteúdo ativa.");

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

            await StitchCreatedTileTerrainAsync(
                    snapshot.Map,
                    tileX,
                    tileY,
                    targetMapPath,
                    cancellationToken)
                .ConfigureAwait(false);

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

    public async Task<NativeTileDeleteResult>
        DeleteTileSafelyAsync(
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
                "Nenhuma fonte de conteúdo ativa.");

        if (_pendingTransforms.Count > 0)
        {
            throw new InvalidOperationException(
                "savePendingBeforeTileDeletion");
        }

        var tile =
            snapshot.Map.Tiles
                .FirstOrDefault(
                    item =>
                        item.X == tileX &&
                        item.Y == tileY)
            ?? throw new InvalidDataException(
                "mapTileNotFound");

        if (
            !OmsiMapPathResolver
                .TryResolveTilePath(
                    snapshot.Map
                        .DirectoryPath,
                    tile.RelativeMapPath,
                    out var tilePath) ||
            !File.Exists(
                tilePath))
        {
            throw new InvalidDataException(
                "mapTilePathInvalid");
        }

        var loadedContent =
            snapshot.Tiles
                .FirstOrDefault(
                    loaded =>
                        loaded.Reference.X ==
                            tileX &&
                        loaded.Reference.Y ==
                            tileY)
                ?.Content;

        var tileContent =
            loadedContent ??
            await _tileReader
                .ReadContentAsync(
                    tilePath,
                    cancellationToken)
                .ConfigureAwait(false);

        var globalDocument =
            await OmsiConfigParser
                .ParseFileAsync(
                    snapshot.Map
                        .GlobalConfigPath,
                    cancellationToken)
                .ConfigureAwait(false);

        var safety =
            OmsiTileDeletionSafetyAnalyzer
                .Analyze(
                    globalDocument,
                    tile,
                    tileContent);

        if (!safety.CanDelete)
        {
            throw new InvalidOperationException(
                "tileDeleteBlocked:" +
                string.Join(
                    ",",
                    safety.Reasons));
        }

        var globalBytes =
            OmsiGlobalTileCatalogRemover
                .RemoveLastTile(
                    globalDocument,
                    tile);

        var tileDirectory =
            Path.GetDirectoryName(
                tilePath)
            ?? snapshot.Map
                .DirectoryPath;

        var tileFileName =
            Path.GetFileName(
                tilePath);

        var associatedFiles =
            Directory
                .EnumerateFiles(
                    tileDirectory,
                    tileFileName +
                    "*",
                    SearchOption
                        .TopDirectoryOnly)
                .OrderBy(
                    file =>
                        file,
                    StringComparer
                        .OrdinalIgnoreCase)
                .ToArray();

        if (
            associatedFiles.Length ==
                0)
        {
            throw new InvalidDataException(
                "mapTileFilesMissing");
        }

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
                $"-delete-tile-{tileX}-{tileY}-" +
                Guid.NewGuid()
                    .ToString("N"));

        var backupGlobal =
            Path.Combine(
                backupRoot,
                "global.cfg");

        var fileBackups =
            new List<(
                string Source,
                string Backup)>(
                    associatedFiles.Length);

        foreach (
            var file in
                associatedFiles)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            var relative =
                Path.GetRelativePath(
                    snapshot.Map
                        .DirectoryPath,
                    file);

            if (!IsSafeRelativePath(
                    relative))
            {
                throw new InvalidDataException(
                    "invalidMapTilePath");
            }

            var backup =
                Path.Combine(
                    backupRoot,
                    relative);

            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    backup)!);

            File.Copy(
                file,
                backup,
                overwrite:
                    false);

            fileBackups.Add(
                (
                    file,
                    backup
                ));
        }

        var globalChanged =
            false;

        try
        {
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

            foreach (
                var item in
                    fileBackups)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                File.Delete(
                    item.Source);
            }

            var updatedMap =
                await OmsiMapCatalog
                    .OpenMapAsync(
                        snapshot.Map
                            .DirectoryPath,
                        cancellationToken)
                    .ConfigureAwait(false);

            var activeTile =
                updatedMap.Tiles
                    .OrderBy(
                        candidate =>
                            Math.Abs(
                                candidate.X -
                                tileX) +
                            Math.Abs(
                                candidate.Y -
                                tileY))
                    .ThenBy(
                        candidate =>
                            candidate.X)
                    .ThenBy(
                        candidate =>
                            candidate.Y)
                    .FirstOrDefault()
                ?? throw new InvalidDataException(
                    "mapHasNoTilesAfterDeletion");

            var wasFullMap =
                snapshot.Tiles.Count >=
                snapshot.Map.Tiles.Count;

            var selectedTiles =
                wasFullMap
                    ? updatedMap.Tiles
                    : OmsiTileRegionSelector
                        .Select(
                            updatedMap.Tiles,
                            activeTile.X,
                            activeTile.Y,
                            radius:
                                1);

            var loaded =
                await LoadSnapshotAsync(
                        updatedMap,
                        activeTile,
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

            return new NativeTileDeleteResult(
                loaded,
                tile,
                backupRoot,
                fileBackups.Count);
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
                var item in
                    fileBackups)
            {
                try
                {
                    if (
                        !File.Exists(
                            item.Source) &&
                        File.Exists(
                            item.Backup))
                    {
                        Directory.CreateDirectory(
                            Path.GetDirectoryName(
                                item.Source)!);

                        File.Copy(
                            item.Backup,
                            item.Source,
                            overwrite:
                                false);
                    }
                }
                catch
                {
                }
            }

            throw;
        }
    }

    private static async Task
        StitchCreatedTileTerrainAsync(
            OmsiMapDescriptor map,
            int tileX,
            int tileY,
            string targetMapPath,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            map);

        var targetTerrainPath =
            targetMapPath +
            ".terrain";

        if (!File.Exists(
                targetTerrainPath))
        {
            throw new InvalidDataException(
                "newMapTemplateTileInvalid");
        }

        var terrainReader =
            new OmsiTerrainReader();

        var targetTerrain =
            await terrainReader
                .ReadAsync(
                    targetTerrainPath,
                    cancellationToken)
                .ConfigureAwait(false);

        async Task<OmsiTerrainGrid?>
            ReadNeighborAsync(
                int neighborX,
                int neighborY)
        {
            var reference =
                map.Tiles
                    .FirstOrDefault(
                        tile =>
                            tile.X ==
                                neighborX &&
                            tile.Y ==
                                neighborY);

            if (
                reference is null ||
                !OmsiMapPathResolver
                    .TryResolveTilePath(
                        map.DirectoryPath,
                        reference.RelativeMapPath,
                        out var neighborMapPath))
            {
                return null;
            }

            var terrainPath =
                neighborMapPath +
                ".terrain";

            if (!File.Exists(
                    terrainPath))
            {
                return null;
            }

            try
            {
                return await terrainReader
                    .ReadAsync(
                        terrainPath,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (InvalidDataException)
            {
                return null;
            }
        }

        var negativeX =
            await ReadNeighborAsync(
                    tileX -
                        1,
                    tileY)
                .ConfigureAwait(false);

        var positiveX =
            await ReadNeighborAsync(
                    tileX +
                        1,
                    tileY)
                .ConfigureAwait(false);

        var negativeY =
            await ReadNeighborAsync(
                    tileX,
                    tileY -
                        1)
                .ConfigureAwait(false);

        var positiveY =
            await ReadNeighborAsync(
                    tileX,
                    tileY +
                        1)
                .ConfigureAwait(false);

        var negativeXNegativeY =
            await ReadNeighborAsync(
                    tileX -
                        1,
                    tileY -
                        1)
                .ConfigureAwait(false);

        var positiveXNegativeY =
            await ReadNeighborAsync(
                    tileX +
                        1,
                    tileY -
                        1)
                .ConfigureAwait(false);

        var negativeXPositiveY =
            await ReadNeighborAsync(
                    tileX -
                        1,
                    tileY +
                        1)
                .ConfigureAwait(false);

        var positiveXPositiveY =
            await ReadNeighborAsync(
                    tileX +
                        1,
                    tileY +
                        1)
                .ConfigureAwait(false);

        var stitched =
            OmsiTerrainBorderStitcher
                .StitchToNeighbors(
                    targetTerrain,
                    negativeX,
                    positiveX,
                    negativeY,
                    positiveY,
                    negativeXNegativeY,
                    positiveXNegativeY,
                    negativeXPositiveY,
                    positiveXPositiveY);

        if (
            stitched.ChangedSamples ==
                0)
        {
            return;
        }

        await File.WriteAllBytesAsync(
                targetTerrainPath,
                OmsiTerrainWriter.Write(
                    stitched.Terrain),
                cancellationToken)
            .ConfigureAwait(false);
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
                "Selecione primeiro o Workspace Map Studio ou uma instalação do OMSI 2.");

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

        var georeference =
            await JsonSerializer
                .DeserializeAsync<
                    NativeMapGeoreference>(
                        stream,
                        new JsonSerializerOptions(
                            JsonSerializerDefaults.Web)
                        {
                            PropertyNameCaseInsensitive =
                                true
                        },
                        cancellationToken)
                .ConfigureAwait(false);

        if (georeference is null)
        {
            return null;
        }

        return georeference with
        {
            MapType =
                string.IsNullOrWhiteSpace(
                    georeference.MapType)
                    ? "roadmap"
                    : georeference.MapType,
            Provider =
                string.IsNullOrWhiteSpace(
                    georeference.Provider)
                    ? "CARTO / OpenStreetMap"
                    : georeference.Provider
        };
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
            string.IsNullOrWhiteSpace(
                value.MapType) ||
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
                "Nenhuma fonte de conteúdo ativa.");

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
            groups.Count > 128 ||
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
                "Nenhuma fonte de conteúdo ativa.");

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
                "Nenhuma fonte de conteúdo ativa.");

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
                "Nenhuma fonte de conteúdo ativa.");

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

    public async Task<NativeSplineSplitResult>
        SplitSplineAsync(
            NativeSplineSplitRequest request,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        var selection =
            request.Selection;

        if (
            selection.Kind !=
                PickingKind.Spline ||
            selection.PreviousSplineId is
                not int previousSplineId ||
            selection.NextSplineId is
                not int nextSplineId ||
            selection.IsHeightSpline is
                not bool isHeightSpline)
        {
            throw new InvalidDataException(
                "splineSplitSelectionInvalid");
        }

        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        if (_pendingTransforms.Count > 0)
        {
            throw new InvalidOperationException(
                "savePendingBeforeSplineSplit");
        }

        var loadedByPath =
            snapshot.Tiles.ToDictionary(
                tile =>
                    tile.Reference
                        .RelativeMapPath,
                tile =>
                    tile.Content,
                StringComparer.OrdinalIgnoreCase);

        var mapContents =
            new List<(
                OmsiTileReference Reference,
                OmsiTileContent Content)>();

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
                mapContents.Add(
                    (
                        tile,
                        loaded
                    ));

                continue;
            }

            if (
                !OmsiMapPathResolver
                    .TryResolveTilePath(
                        snapshot.Map.DirectoryPath,
                        tile.RelativeMapPath,
                        out var path) ||
                !File.Exists(
                    path))
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

        var allSplines =
            mapContents
                .SelectMany(
                    entry =>
                        entry.Content.Splines
                            .Select(
                                spline =>
                                    (
                                        entry.Reference,
                                        Spline: spline
                                    )))
                .ToArray();

        if (
            allSplines
                .GroupBy(
                    item =>
                        item.Spline.SplineId)
                .Any(
                    group =>
                        group.Count() >
                        1))
        {
            throw new InvalidDataException(
                "duplicateSplineId");
        }

        var sourceEntry =
            allSplines
                .FirstOrDefault(
                    item =>
                        item.Spline.SplineId ==
                            selection.EntityId);

        if (
            sourceEntry.Spline is null ||
            sourceEntry.Reference.X !=
                selection.TileX ||
            sourceEntry.Reference.Y !=
                selection.TileY ||
            !string.Equals(
                sourceEntry.Spline.SplinePath,
                selection.AssetPath,
                StringComparison.OrdinalIgnoreCase) ||
            sourceEntry.Spline.PreviousSplineId !=
                previousSplineId ||
            sourceEntry.Spline.NextSplineId !=
                nextSplineId ||
            sourceEntry.Spline.IsHeightSpline !=
                isHeightSpline)
        {
            throw new InvalidDataException(
                "splineSourceChanged");
        }

        var source =
            sourceEntry.Spline;

        if (
            request.FirstLength <=
                0.5 ||
            request.SecondLength <=
                0.5 ||
            Math.Abs(
                request.FirstLength +
                request.SecondLength -
                source.Length) >
                Math.Max(
                    0.02,
                    source.Length *
                        0.0005))
        {
            throw new InvalidDataException(
                "invalidSplineSplitGeometry");
        }

        var maxUsedId =
            mapContents
                .SelectMany(
                    content =>
                        content.Content.Objects
                            .Select(
                                item =>
                                    item.ObjectId)
                            .Concat(
                                content.Content.Splines
                                    .Select(
                                        item =>
                                            item.SplineId)))
                .DefaultIfEmpty(
                    0)
                .Max();

        if (
            maxUsedId >=
            int.MaxValue)
        {
            throw new InvalidDataException(
                "splineIdExhausted");
        }

        var secondSplineId =
            checked(
                maxUsedId +
                1);

        var pathToTile =
            snapshot.Map.Tiles
                .ToDictionary(
                    tile =>
                        tile.RelativeMapPath,
                    StringComparer.OrdinalIgnoreCase);

        if (
            !OmsiMapPathResolver
                .TryResolveTilePath(
                    snapshot.Map.DirectoryPath,
                    sourceEntry.Reference
                        .RelativeMapPath,
                    out var sourcePath) ||
            !OmsiMapPathResolver
                .TryResolveTilePath(
                    snapshot.Map.DirectoryPath,
                    request.SecondTile
                        .RelativeMapPath,
                    out var secondPath))
        {
            throw new InvalidDataException(
                "splineSplitTilePathInvalid");
        }

        var documents =
            new Dictionary<
                string,
                OmsiConfigDocument>(
                    StringComparer.OrdinalIgnoreCase);

        async Task<OmsiConfigDocument>
            GetDocumentAsync(
                string path)
        {
            if (
                documents.TryGetValue(
                    path,
                    out var existing))
            {
                return existing;
            }

            var loaded =
                await OmsiConfigParser
                    .ParseFileAsync(
                        path,
                        cancellationToken)
                    .ConfigureAwait(false);

            documents[path] =
                loaded;

            return loaded;
        }

        void SetDocument(
            string path,
            byte[] bytes)
        {
            documents[path] =
                OmsiConfigParser
                    .ParseBytes(
                        bytes);
        }

        var sourceDocument =
            await GetDocumentAsync(
                sourcePath);

        var transformed =
            OmsiTileSplineEditor
                .ApplyTransforms(
                    sourceDocument,
                    [
                        new OmsiSplineTransformEdit(
                            source.SourceSectionOrdinal,
                            source.SplinePath,
                            source.SplineId,
                            source.PreviousSplineId,
                            source.NextSplineId,
                            source.IsHeightSpline,
                            source.X,
                            source.Z,
                            source.Y,
                            source.Rotation,
                            request.FirstLength,
                            source.Radius,
                            request.FirstGradientStart,
                            request.FirstGradientEnd)
                    ]);

        SetDocument(
            sourcePath,
            transformed.Bytes);

        var sourceLinked =
            OmsiTileSplineLinkEditor
                .ApplyLinks(
                    await GetDocumentAsync(
                        sourcePath),
                    [
                        new OmsiSplineLinkEdit(
                            source.SourceSectionOrdinal,
                            source.SplinePath,
                            source.SplineId,
                            source.PreviousSplineId,
                            source.NextSplineId,
                            source.IsHeightSpline,
                            source.PreviousSplineId,
                            secondSplineId)
                    ]);

        SetDocument(
            sourcePath,
            sourceLinked.Bytes);

        var secondDocument =
            await GetDocumentAsync(
                secondPath);

        var secondInsertion =
            OmsiTileSplineInserter
                .Append(
                    secondDocument,
                    new OmsiNewPlacedSpline(
                        source.HeaderValue,
                        source.SplinePath,
                        secondSplineId,
                        source.SplineId,
                        source.NextSplineId,
                        request.SecondX,
                        request.SecondZ,
                        request.SecondY,
                        request.SecondRotation,
                        request.SecondLength,
                        request.SecondRadius,
                        request.SecondGradientStart,
                        request.SecondGradientEnd,
                        source.IsHeightSpline,
                        source.ExtraValues));

        SetDocument(
            secondPath,
            secondInsertion.Bytes);

        if (
            source.NextSplineId >=
            0)
        {
            var nextEntry =
                allSplines
                    .FirstOrDefault(
                        item =>
                            item.Spline.SplineId ==
                            source.NextSplineId);

            if (
                nextEntry.Spline is null ||
                nextEntry.Spline.PreviousSplineId !=
                    source.SplineId)
            {
                throw new InvalidDataException(
                    "splineSplitNextLinkMismatch");
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
                    "splineSplitNextTilePathInvalid");
            }

            var nextLinked =
                OmsiTileSplineLinkEditor
                    .ApplyLinks(
                        await GetDocumentAsync(
                            nextPath),
                        [
                            new OmsiSplineLinkEdit(
                                nextEntry.Spline
                                    .SourceSectionOrdinal,
                                nextEntry.Spline
                                    .SplinePath,
                                nextEntry.Spline
                                    .SplineId,
                                nextEntry.Spline
                                    .PreviousSplineId,
                                nextEntry.Spline
                                    .NextSplineId,
                                nextEntry.Spline
                                    .IsHeightSpline,
                                secondSplineId,
                                nextEntry.Spline
                                    .NextSplineId)
                        ]);

            SetDocument(
                nextPath,
                nextLinked.Bytes);
        }

        var backupRoot =
            Path.Combine(
                snapshot.Map.DirectoryPath,
                ".mapstudio-backups",
                DateTimeOffset.UtcNow
                    .ToString(
                        "yyyyMMdd-HHmmssfff'Z'",
                        CultureInfo.InvariantCulture) +
                "-native-split-" +
                Guid.NewGuid()
                    .ToString("N"));

        var writes =
            documents
                .Select(
                    pair =>
                        new PendingFileWrite(
                            pair.Key,
                            Path.Combine(
                                backupRoot,
                                Path.GetRelativePath(
                                    snapshot.Map.DirectoryPath,
                                    pair.Key)),
                            pair.Value
                                .ToBytes()))
                .ToArray();

        await SafeFileTransaction
            .WriteAllAsync(
                writes,
                cancellationToken)
            .ConfigureAwait(false);

        LastBackupDirectory =
            backupRoot;

        var affectedPaths =
            documents.Keys
                .Select(
                    path =>
                        Path.GetFullPath(
                            path))
                .ToHashSet(
                    StringComparer.OrdinalIgnoreCase);

        var refreshedTiles =
            new List<NativeLoadedTile>(
                snapshot.Tiles.Count);

        foreach (
            var tile in
                snapshot.Tiles)
        {
            if (
                !OmsiMapPathResolver
                    .TryResolveTilePath(
                        snapshot.Map.DirectoryPath,
                        tile.Reference
                            .RelativeMapPath,
                        out var tilePath) ||
                !affectedPaths.Contains(
                    Path.GetFullPath(
                        tilePath)))
            {
                refreshedTiles.Add(
                    tile);

                continue;
            }

            refreshedTiles.Add(
                new NativeLoadedTile(
                    tile.Reference,
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
                    refreshedTiles
                        .ToArray()
            };

        return new NativeSplineSplitResult(
            CurrentMap,
            source.SplineId,
            secondSplineId);
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

        var existingSplineIds =
            contents
                .SelectMany(
                    content =>
                        content.Splines)
                .Select(
                    spline =>
                        spline.SplineId)
                .ToHashSet();

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
                    -1 &&
                existingSplineIds.Contains(
                    previousSpline.NextSplineId))
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
                    -1 &&
                existingSplineIds.Contains(
                    nextSpline.PreviousSplineId))
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

        if (requests.Count == 0 || requests.Count > 50_000)
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

    public async Task<NativeAttachmentUpdateResult>
        UpdateAttachmentAsync(
            OmsiTileReference tile,
            OmsiAttachmentTransformEdit edit,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentNullException.ThrowIfNull(
            tile);

        ArgumentNullException.ThrowIfNull(
            edit);

        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        if (_pendingTransforms.Count > 0)
        {
            throw new InvalidOperationException(
                "savePendingBeforeAttachmentEdit");
        }

        if (
            !OmsiMapPathResolver
                .TryResolveTilePath(
                    snapshot.Map
                        .DirectoryPath,
                    tile.RelativeMapPath,
                    out var targetPath))
        {
            throw new InvalidDataException(
                "attachmentTilePathInvalid");
        }

        var document =
            await OmsiConfigParser
                .ParseFileAsync(
                    targetPath,
                    cancellationToken)
                .ConfigureAwait(false);

        var edited =
            OmsiTileAttachmentEditor
                .ApplyTransforms(
                    document,
                    [edit]);

        if (edited.AppliedCount != 1)
        {
            throw new InvalidDataException(
                "attachmentEditNotApplied");
        }

        var backupRoot =
            Path.Combine(
                snapshot.Map
                    .DirectoryPath,
                ".mapstudio-backups",
                DateTimeOffset.UtcNow
                    .ToString(
                        "yyyyMMdd-HHmmssfff'Z'",
                        CultureInfo
                            .InvariantCulture) +
                "-attachment-" +
                edit.AttachmentId
                    .ToString(
                        CultureInfo
                            .InvariantCulture));

        var relativeTarget =
            Path.GetRelativePath(
                snapshot.Map
                    .DirectoryPath,
                targetPath);

        await SafeFileTransaction
            .WriteAllAsync(
                [
                    new PendingFileWrite(
                        targetPath,
                        Path.Combine(
                            backupRoot,
                            relativeTarget),
                        edited.Bytes)
                ],
                cancellationToken)
            .ConfigureAwait(false);

        LastBackupDirectory =
            backupRoot;

        var refreshed =
            new List<
                NativeLoadedTile>(
                    snapshot.Tiles.Count);

        OmsiPlacedAttachment?
            updatedAttachment =
                null;

        foreach (var loadedTile in
            snapshot.Tiles)
        {
            if (
                !string.Equals(
                    loadedTile.Reference
                        .RelativeMapPath,
                    tile.RelativeMapPath,
                    StringComparison
                        .OrdinalIgnoreCase))
            {
                refreshed.Add(
                    loadedTile);

                continue;
            }

            var content =
                await _tileReader
                    .ReadContentAsync(
                        targetPath,
                        cancellationToken)
                    .ConfigureAwait(false);

            refreshed.Add(
                new NativeLoadedTile(
                    loadedTile.Reference,
                    content));

            updatedAttachment =
                (
                    content.Attachments ??
                    Array.Empty<
                        OmsiPlacedAttachment>()
                )
                .FirstOrDefault(
                    attachment =>
                        attachment.SourceSectionOrdinal ==
                            edit.SourceSectionOrdinal &&
                        attachment.Kind ==
                            edit.Kind &&
                        attachment.AttachmentId ==
                            edit.AttachmentId);
        }

        if (updatedAttachment is null)
        {
            throw new InvalidDataException(
                "attachmentReloadFailed");
        }

        CurrentMap =
            snapshot with
            {
                Tiles =
                    refreshed.ToArray()
            };

        return new NativeAttachmentUpdateResult(
            CurrentMap,
            updatedAttachment,
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

    public async Task<NativeSceneryPathUpdateResult>
        UpdateSceneryPathAsync(
            string assetPath,
            int pathOrdinal,
            OmsiSceneryPathDefinition path,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            assetPath);

        ArgumentNullException.ThrowIfNull(
            path);

        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        var root =
            OmsiRootPath ??
            throw new InvalidOperationException(
                "Nenhuma fonte de conteúdo ativa.");

        if (
            _pendingTransforms.Count >
                0)
        {
            throw new InvalidOperationException(
                "savePendingBeforeSceneryPathEdit");
        }

        if (
            !OmsiSceneryObjectPathResolver
                .TryResolve(
                    root,
                    assetPath,
                    out var target) ||
            !File.Exists(
                target))
        {
            throw new FileNotFoundException(
                "sceneryPathAssetMissing",
                assetPath);
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
            pathOrdinal < 0 ||
            pathOrdinal >=
                metadata.Paths.Count)
        {
            throw new InvalidDataException(
                "sceneryPathOrdinalInvalid");
        }

        var bytes =
            new OmsiSceneryPathPatcher()
                .Patch(
                    document,
                    pathOrdinal,
                    path);

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
            await new OmsiSceneryObjectReader()
                .ReadMetadataAsync(
                    target,
                    cancellationToken)
                .ConfigureAwait(false);

        if (
            pathOrdinal >=
                reloaded.Paths.Count)
        {
            throw new InvalidDataException(
                "sceneryPathReloadFailed");
        }

        return new NativeSceneryPathUpdateResult(
            reloaded.Paths[
                pathOrdinal],
            target,
            backupPath);
    }

    public async Task<NativeSceneryPathUpdateResult>
        DuplicateSceneryPathAsync(
            string assetPath,
            int sourcePathOrdinal,
            OmsiSceneryPathDefinition path,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            assetPath);

        ArgumentNullException.ThrowIfNull(
            path);

        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        var root =
            OmsiRootPath ??
            throw new InvalidOperationException(
                "Nenhuma fonte de conteúdo ativa.");

        if (
            _pendingTransforms.Count >
                0)
        {
            throw new InvalidOperationException(
                "savePendingBeforeSceneryPathEdit");
        }

        if (
            !OmsiSceneryObjectPathResolver
                .TryResolve(
                    root,
                    assetPath,
                    out var target) ||
            !File.Exists(
                target))
        {
            throw new FileNotFoundException(
                "sceneryPathAssetMissing",
                assetPath);
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
            sourcePathOrdinal < 0 ||
            sourcePathOrdinal >=
                metadata.Paths.Count)
        {
            throw new InvalidDataException(
                "sceneryPathOrdinalInvalid");
        }

        var bytes =
            new OmsiSceneryPathPatcher()
                .AppendDuplicate(
                    document,
                    sourcePathOrdinal,
                    path);

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
            await new OmsiSceneryObjectReader()
                .ReadMetadataAsync(
                    target,
                    cancellationToken)
                .ConfigureAwait(false);

        var duplicatedOrdinal =
            metadata.Paths.Count;

        if (
            duplicatedOrdinal >=
                reloaded.Paths.Count)
        {
            throw new InvalidDataException(
                "sceneryPathReloadFailed");
        }

        return new NativeSceneryPathUpdateResult(
            reloaded.Paths[
                duplicatedOrdinal],
            target,
            backupPath);
    }

    public async Task<NativeSceneryPathUpdateResult>
        AddSceneryPathAsync(
            string assetPath,
            OmsiSceneryPathDefinition path,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            assetPath);

        ArgumentNullException.ThrowIfNull(
            path);

        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        var root =
            OmsiRootPath ??
            throw new InvalidOperationException(
                "Nenhuma fonte de conteúdo ativa.");

        if (
            _pendingTransforms.Count >
                0)
        {
            throw new InvalidOperationException(
                "savePendingBeforeSceneryPathEdit");
        }

        if (
            !OmsiSceneryObjectPathResolver
                .TryResolve(
                    root,
                    assetPath,
                    out var target) ||
            !File.Exists(
                target))
        {
            throw new FileNotFoundException(
                "sceneryPathAssetMissing",
                assetPath);
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

        var newOrdinal =
            metadata.Paths.Count;

        var bytes =
            new OmsiSceneryPathPatcher()
                .AppendNew(
                    document,
                    path);

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
            await new OmsiSceneryObjectReader()
                .ReadMetadataAsync(
                    target,
                    cancellationToken)
                .ConfigureAwait(false);

        if (
            reloaded.Paths.Count !=
                newOrdinal +
                    1)
        {
            throw new InvalidDataException(
                "sceneryPathReloadFailed");
        }

        return new NativeSceneryPathUpdateResult(
            reloaded.Paths[
                newOrdinal],
            target,
            backupPath);
    }

    public async Task<NativeAssetPathDeleteResult>
        DeleteSceneryPathAsync(
            string assetPath,
            int pathOrdinal,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            assetPath);

        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        var root =
            OmsiRootPath ??
            throw new InvalidOperationException(
                "Nenhuma fonte de conteúdo ativa.");

        if (
            _pendingTransforms.Count >
                0)
        {
            throw new InvalidOperationException(
                "savePendingBeforeSceneryPathEdit");
        }

        if (
            !OmsiSceneryObjectPathResolver
                .TryResolve(
                    root,
                    assetPath,
                    out var target) ||
            !File.Exists(
                target))
        {
            throw new FileNotFoundException(
                "sceneryPathAssetMissing",
                assetPath);
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
            pathOrdinal < 0 ||
            pathOrdinal >=
                metadata.Paths.Count)
        {
            throw new InvalidDataException(
                "sceneryPathOrdinalInvalid");
        }

        var bytes =
            new OmsiSceneryPathPatcher()
                .Remove(
                    document,
                    pathOrdinal);

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
            await new OmsiSceneryObjectReader()
                .ReadMetadataAsync(
                    target,
                    cancellationToken)
                .ConfigureAwait(false);

        if (
            reloaded.Paths.Count !=
                metadata.Paths.Count -
                    1)
        {
            throw new InvalidDataException(
                "sceneryPathReloadFailed");
        }

        return new NativeAssetPathDeleteResult(
            target,
            backupPath,
            reloaded.Paths.Count);
    }

    public async Task<NativeSplinePathUpdateResult>
        UpdateSplinePathAsync(
            string assetPath,
            int pathOrdinal,
            OmsiSplinePathDefinition path,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            assetPath);

        ArgumentNullException.ThrowIfNull(
            path);

        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        var root =
            OmsiRootPath ??
            throw new InvalidOperationException(
                "Nenhuma fonte de conteúdo ativa.");

        if (
            _pendingTransforms.Count >
                0)
        {
            throw new InvalidOperationException(
                "savePendingBeforeSplinePathEdit");
        }

        if (
            !OmsiSplinePathResolver
                .TryResolve(
                    root,
                    assetPath,
                    out var target) ||
            !File.Exists(
                target))
        {
            throw new FileNotFoundException(
                "splinePathAssetMissing",
                assetPath);
        }

        var document =
            await OmsiConfigParser
                .ParseFileAsync(
                    target,
                    cancellationToken)
                .ConfigureAwait(false);

        var definition =
            new OmsiSplineDefinitionReader()
                .Read(
                    document);

        if (
            pathOrdinal < 0 ||
            pathOrdinal >=
                definition.Paths.Count)
        {
            throw new InvalidDataException(
                "splinePathOrdinalInvalid");
        }

        var bytes =
            new OmsiSplinePathPatcher()
                .Patch(
                    document,
                    pathOrdinal,
                    path);

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
            await new OmsiSplineDefinitionReader()
                .ReadAsync(
                    target,
                    cancellationToken)
                .ConfigureAwait(false);

        if (
            pathOrdinal >=
                reloaded.Paths.Count)
        {
            throw new InvalidDataException(
                "splinePathReloadFailed");
        }

        return new NativeSplinePathUpdateResult(
            reloaded.Paths[
                pathOrdinal],
            target,
            backupPath);
    }

    public async Task<NativeSplinePathUpdateResult>
        DuplicateSplinePathAsync(
            string assetPath,
            int sourcePathOrdinal,
            OmsiSplinePathDefinition path,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            assetPath);

        ArgumentNullException.ThrowIfNull(
            path);

        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        var root =
            OmsiRootPath ??
            throw new InvalidOperationException(
                "Nenhuma fonte de conteúdo ativa.");

        if (
            _pendingTransforms.Count >
                0)
        {
            throw new InvalidOperationException(
                "savePendingBeforeSplinePathEdit");
        }

        if (
            !OmsiSplinePathResolver
                .TryResolve(
                    root,
                    assetPath,
                    out var target) ||
            !File.Exists(
                target))
        {
            throw new FileNotFoundException(
                "splinePathAssetMissing",
                assetPath);
        }

        var document =
            await OmsiConfigParser
                .ParseFileAsync(
                    target,
                    cancellationToken)
                .ConfigureAwait(false);

        var definition =
            new OmsiSplineDefinitionReader()
                .Read(
                    document);

        if (
            sourcePathOrdinal < 0 ||
            sourcePathOrdinal >=
                definition.Paths.Count)
        {
            throw new InvalidDataException(
                "splinePathOrdinalInvalid");
        }

        var bytes =
            new OmsiSplinePathPatcher()
                .AppendDuplicate(
                    document,
                    sourcePathOrdinal,
                    path);

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
            await new OmsiSplineDefinitionReader()
                .ReadAsync(
                    target,
                    cancellationToken)
                .ConfigureAwait(false);

        var duplicatedOrdinal =
            definition.Paths.Count;

        if (
            duplicatedOrdinal >=
                reloaded.Paths.Count)
        {
            throw new InvalidDataException(
                "splinePathReloadFailed");
        }

        return new NativeSplinePathUpdateResult(
            reloaded.Paths[
                duplicatedOrdinal],
            target,
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
                "Nenhuma fonte de conteúdo ativa.");

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

    public async Task<OmsiTimetableTrack>
        CreateTimetableTrackAsync(
            string name,
            IReadOnlyList<
                OmsiTimetableTrackEntry> entries,
            string comment1 = "Created with OMSI Map Studio",
            string comment2 = "",
            CancellationToken cancellationToken =
                default)
    {
        ArgumentNullException.ThrowIfNull(
            entries);

        if (entries.Count == 0)
        {
            throw new InvalidDataException(
                "trackRequiresEntries");
        }

        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        if (_pendingTransforms.Count > 0)
        {
            throw new InvalidOperationException(
                "savePendingBeforeTimetableEdit");
        }

        var baseName =
            NormalizeTimetableBaseName(
                name,
                ".ttr");

        var ttData =
            Path.Combine(
                snapshot.Map.DirectoryPath,
                "TTData");

        Directory.CreateDirectory(
            ttData);

        var target =
            Path.Combine(
                ttData,
                baseName +
                ".ttr");

        var relative =
            Path.GetRelativePath(
                snapshot.Map.DirectoryPath,
                target);

        var source =
            new OmsiTimetableTrack(
                target,
                relative,
                baseName,
                comment1.Trim(),
                comment2.Trim(),
                entries.ToArray());

        var bytes =
            new OmsiTimetableTrackWriter()
                .Write(
                    source,
                    entries);

        await CreateNewFileAtomicallyAsync(
                target,
                bytes,
                cancellationToken)
            .ConfigureAwait(false);

        return await new OmsiTimetableTrackReader()
            .ReadAsync(
                snapshot.Map.DirectoryPath,
                target,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<OmsiTimetableTrip>
        CreateTimetableTripAsync(
            string name,
            string trackName,
            string destination,
            string line,
            bool trainReverse,
            IReadOnlyList<
                OmsiTimetableTripStation> stations,
            IReadOnlyList<string> profileLines,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentNullException.ThrowIfNull(
            stations);

        ArgumentNullException.ThrowIfNull(
            profileLines);

        if (stations.Count == 0)
        {
            throw new InvalidDataException(
                "tripRequiresStations");
        }

        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        if (_pendingTransforms.Count > 0)
        {
            throw new InvalidOperationException(
                "savePendingBeforeTimetableEdit");
        }

        var baseName =
            NormalizeTimetableBaseName(
                name,
                ".ttp");

        var ttData =
            Path.Combine(
                snapshot.Map.DirectoryPath,
                "TTData");

        Directory.CreateDirectory(
            ttData);

        var target =
            Path.Combine(
                ttData,
                baseName +
                ".ttp");

        var relative =
            Path.GetRelativePath(
                snapshot.Map.DirectoryPath,
                target);

        var source =
            new OmsiTimetableTrip(
                target,
                relative,
                baseName,
                "Created with OMSI Map Studio",
                string.Empty,
                trackName.Trim(),
                destination.Trim(),
                line.Trim(),
                trainReverse,
                stations.ToArray(),
                profileLines
                    .Where(
                        value =>
                            !string.IsNullOrWhiteSpace(
                                value))
                    .Select(
                        value =>
                            value.Trim())
                    .ToArray());

        await CreateNewFileAtomicallyAsync(
                target,
                new OmsiTimetableTripWriter()
                    .Write(source),
                cancellationToken)
            .ConfigureAwait(false);

        return await new OmsiTimetableTripReader()
            .ReadAsync(
                snapshot.Map.DirectoryPath,
                target,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<OmsiTimetableLine>
        CreateTimetableLineAsync(
            string name,
            string priority,
            bool userAllowed,
            IReadOnlyList<
                OmsiTimetableTour> tours,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentNullException.ThrowIfNull(
            tours);

        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        if (_pendingTransforms.Count > 0)
        {
            throw new InvalidOperationException(
                "savePendingBeforeTimetableEdit");
        }

        var baseName =
            NormalizeTimetableBaseName(
                name,
                ".ttl");

        var ttData =
            Path.Combine(
                snapshot.Map.DirectoryPath,
                "TTData");

        Directory.CreateDirectory(
            ttData);

        var target =
            Path.Combine(
                ttData,
                baseName +
                ".ttl");

        var relative =
            Path.GetRelativePath(
                snapshot.Map.DirectoryPath,
                target);

        var source =
            new OmsiTimetableLine(
                target,
                relative,
                baseName,
                "Created with OMSI Map Studio",
                string.Empty,
                userAllowed,
                priority.Trim(),
                tours.ToArray());

        await CreateNewFileAtomicallyAsync(
                target,
                new OmsiTimetableLineWriter()
                    .Write(source),
                cancellationToken)
            .ConfigureAwait(false);

        return await new OmsiTimetableLineReader()
            .ReadAsync(
                snapshot.Map.DirectoryPath,
                target,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<
        OmsiTimetableBusStop>>
        AddBusStopAsync(
            OmsiTimetableBusStop stop,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentNullException.ThrowIfNull(
            stop);

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
            Path.Combine(
                snapshot.Map.DirectoryPath,
                "TTData",
                "Busstops.cfg");

        Directory.CreateDirectory(
            Path.GetDirectoryName(
                target)!);

        var reader =
            new OmsiTimetableBusStopReader();

        var stops =
            File.Exists(target)
                ? (
                    await reader
                        .ReadAsync(
                            target,
                            cancellationToken)
                        .ConfigureAwait(false)
                  ).ToList()
                : new List<
                    OmsiTimetableBusStop>();

        if (
            stops.Any(
                candidate =>
                    candidate.Id ==
                    stop.Id))
        {
            throw new InvalidDataException(
                "duplicateBusStopId");
        }

        stops.Add(stop);

        var bytes =
            new OmsiTimetableBusStopWriter()
                .Write(
                    stops);

        if (File.Exists(target))
        {
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
        }
        else
        {
            await CreateNewFileAtomicallyAsync(
                    target,
                    bytes,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return await reader
            .ReadAsync(
                target,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<
        OmsiStationLink>>
        AddStationLinkAsync(
            OmsiStationLink link,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentNullException.ThrowIfNull(
            link);

        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        if (_pendingTransforms.Count > 0)
        {
            throw new InvalidOperationException(
                "savePendingBeforeTimetableEdit");
        }

        var ttData =
            Path.Combine(
                snapshot.Map.DirectoryPath,
                "TTData");

        Directory.CreateDirectory(
            ttData);

        var stopPath =
            Path.Combine(
                ttData,
                "Busstops.cfg");

        if (File.Exists(stopPath))
        {
            var stops =
                await new OmsiTimetableBusStopReader()
                    .ReadAsync(
                        stopPath,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (
                !stops.Any(
                    stop =>
                        stop.Id ==
                        link.StartBusStopId) ||
                !stops.Any(
                    stop =>
                        stop.Id ==
                        link.EndBusStopId))
            {
                throw new InvalidDataException(
                    "stationLinkStopMissing");
            }
        }

        var target =
            Path.Combine(
                ttData,
                "StnLinks.cfg");

        var reader =
            new OmsiStationLinkReader();

        var links =
            File.Exists(target)
                ? (
                    await reader
                        .ReadAsync(
                            target,
                            cancellationToken)
                        .ConfigureAwait(false)
                  ).ToList()
                : new List<
                    OmsiStationLink>();

        links.Add(link);

        var bytes =
            new OmsiStationLinkWriter()
                .Write(
                    links);

        if (File.Exists(target))
        {
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
        }
        else
        {
            await CreateNewFileAtomicallyAsync(
                    target,
                    bytes,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return await reader
            .ReadAsync(
                target,
                cancellationToken)
            .ConfigureAwait(false);
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
        PaintTerrainTexturePolygonAsync(
            IReadOnlyList<Vector2> worldPolygon,
            int layerIndex,
            byte targetAlpha,
            double edgeFeatherMeters,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentNullException.ThrowIfNull(
            worldPolygon);

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
            worldPolygon.Count < 3 ||
            worldPolygon.Any(
                point =>
                    !float.IsFinite(point.X) ||
                    !float.IsFinite(point.Y)) ||
            !double.IsFinite(
                edgeFeatherMeters) ||
            edgeFeatherMeters < 0)
        {
            throw new InvalidDataException(
                "terrainPaintPolygonInvalid");
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

        var resolution =
            snapshot.Map
                .GroundTextures[
                    layerIndex]
                .MaskResolution;

        var mapRoot =
            Path.GetFullPath(
                snapshot.Map.DirectoryPath)
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);

        var requiredPrefix =
            mapRoot +
            Path.DirectorySeparatorChar;

        var maskDirectory =
            Path.Combine(
                snapshot.Map.DirectoryPath,
                "texture",
                "map");

        var refreshed =
            new Dictionary<
                string,
                OmsiTileContent>(
                    StringComparer.OrdinalIgnoreCase);

        foreach (var loaded in snapshot.Tiles)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            if (
                !OmsiMapPathResolver.TryResolveTilePath(
                    snapshot.Map.DirectoryPath,
                    loaded.Reference.RelativeMapPath,
                    out var tilePath))
            {
                continue;
            }

            var tileOriginX =
                (float)OmsiTileGrid.GetOriginX(
                    loaded.Reference.X);

            var tileOriginY =
                (float)OmsiTileGrid.GetOriginZ(
                    loaded.Reference.Y);

            var localPolygon =
                worldPolygon
                    .Select(
                        point =>
                            new Vector2(
                                point.X -
                                    tileOriginX,
                                point.Y -
                                    tileOriginY))
                    .ToArray();

            var minX =
                localPolygon.Min(
                    point =>
                        point.X);

            var maxX =
                localPolygon.Max(
                    point =>
                        point.X);

            var minY =
                localPolygon.Min(
                    point =>
                        point.Y);

            var maxY =
                localPolygon.Max(
                    point =>
                        point.Y);

            if (
                maxX < 0 ||
                maxY < 0 ||
                minX >
                    OmsiTileGrid.TileSize ||
                minY >
                    OmsiTileGrid.TileSize)
            {
                continue;
            }

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
                    .PaintPolygon(
                        source,
                        localPolygon,
                        targetAlpha,
                        edgeFeatherMeters);

            if (
                result.ChangedPixels ==
                0)
            {
                continue;
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

            refreshed[
                loaded.Reference
                    .RelativeMapPath] =
                await _tileReader
                    .ReadContentAsync(
                        tilePath,
                        cancellationToken)
                    .ConfigureAwait(false);
        }

        if (
            refreshed.Count ==
            0)
        {
            return snapshot;
        }

        CurrentMap =
            snapshot with
            {
                Tiles =
                    snapshot.Tiles
                        .Select(
                            item =>
                                refreshed.TryGetValue(
                                    item.Reference
                                        .RelativeMapPath,
                                    out var content)
                                    ? new NativeLoadedTile(
                                        item.Reference,
                                        content)
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
                default,
            bool repairBrokenLinks =
                false)
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

        var plannerStates =
            states;

        var plannerOriginalPrevious =
            originalPrevious;

        var plannerOriginalNext =
            originalNext;

        if (repairBrokenLinks)
        {
            var repairedStates =
                states.ToDictionary(
                    pair =>
                        pair.Key,
                    pair =>
                        pair.Value);

            var sourceState =
                repairedStates[
                    source.Spline.SplineId];

            if (
                plannerOriginalPrevious !=
                    -1 &&
                !states.ContainsKey(
                    plannerOriginalPrevious))
            {
                plannerOriginalPrevious =
                    -1;

                sourceState =
                    sourceState with
                    {
                        PreviousSplineId =
                            -1
                    };
            }

            if (
                plannerOriginalNext !=
                    -1 &&
                !states.ContainsKey(
                    plannerOriginalNext))
            {
                plannerOriginalNext =
                    -1;

                sourceState =
                    sourceState with
                    {
                        NextSplineId =
                            -1
                    };
            }

            repairedStates[
                source.Spline.SplineId] =
                sourceState;

            if (
                desiredPreviousSplineId !=
                    -1 &&
                repairedStates.TryGetValue(
                    desiredPreviousSplineId,
                    out var previousTarget) &&
                previousTarget.NextSplineId !=
                    -1 &&
                !states.ContainsKey(
                    previousTarget.NextSplineId))
            {
                repairedStates[
                    desiredPreviousSplineId] =
                    previousTarget with
                    {
                        NextSplineId =
                            -1
                    };
            }

            if (
                desiredNextSplineId !=
                    -1 &&
                repairedStates.TryGetValue(
                    desiredNextSplineId,
                    out var nextTarget) &&
                nextTarget.PreviousSplineId !=
                    -1 &&
                !states.ContainsKey(
                    nextTarget.PreviousSplineId))
            {
                repairedStates[
                    desiredNextSplineId] =
                    nextTarget with
                    {
                        PreviousSplineId =
                            -1
                    };
            }

            plannerStates =
                repairedStates;
        }

        var plan =
            OmsiSplineLinkPlanner
                .Plan(
                    plannerStates,
                    source.Spline.SplineId,
                    plannerOriginalPrevious,
                    plannerOriginalNext,
                    desiredPreviousSplineId,
                    desiredNextSplineId)
                .ToDictionary(
                    pair =>
                        pair.Key,
                    pair =>
                        pair.Value);

        if (
            repairBrokenLinks &&
            (
                source.Spline.PreviousSplineId !=
                    desiredPreviousSplineId ||
                source.Spline.NextSplineId !=
                    desiredNextSplineId
            ))
        {
            plan[
                source.Spline.SplineId] =
                new OmsiSplineLinkTarget(
                    desiredPreviousSplineId,
                    desiredNextSplineId);
        }

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

        LastDeleteBackupEntries =
            writes
                .Select(
                    write =>
                        new NativeDeleteBackupEntry(
                            write.TargetPath,
                            write.BackupPath))
                .ToArray();

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

    public async Task<NativeSplineFlowToggleResult>
        TogglePlacedSplineVehicleFlowAsync(
            NativeSelectionInfo selection,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentNullException.ThrowIfNull(
            selection);

        if (
            selection.Kind !=
                PickingKind.Spline)
        {
            throw new InvalidDataException(
                "splineFlowSelectionInvalid");
        }

        var root =
            OmsiRootPath ??
            throw new InvalidOperationException(
                "Nenhuma fonte de conteúdo ativa.");

        const string suffix =
            "__mapstudio_flowrev";

        if (
            !OmsiSplinePathResolver
                .TryResolve(
                    root,
                    selection.AssetPath,
                    out var sourceFullPath) ||
            !File.Exists(
                sourceFullPath))
        {
            throw new FileNotFoundException(
                "SLI selecionada não encontrada.",
                selection.AssetPath);
        }

        var sourceDirectory =
            Path.GetDirectoryName(
                sourceFullPath) ??
            throw new InvalidOperationException(
                "splineDirectoryMissing");

        var sourceName =
            Path.GetFileNameWithoutExtension(
                sourceFullPath);

        var extension =
            Path.GetExtension(
                sourceFullPath);

        string targetFullPath;
        bool reversed;
        int reversedPathCount =
            0;

        if (
            sourceName.EndsWith(
                suffix,
                StringComparison.OrdinalIgnoreCase))
        {
            var originalName =
                sourceName[
                    ..^suffix.Length] +
                extension;

            targetFullPath =
                Path.Combine(
                    sourceDirectory,
                    originalName);

            if (
                !File.Exists(
                    targetFullPath))
            {
                throw new FileNotFoundException(
                    "SLI original da variante invertida não foi encontrada.",
                    targetFullPath);
            }

            reversed =
                false;
        }
        else
        {
            targetFullPath =
                Path.Combine(
                    sourceDirectory,
                    sourceName +
                    suffix +
                    extension);

            var sourceDocument =
                await OmsiConfigParser
                    .ParseFileAsync(
                        sourceFullPath,
                        cancellationToken)
                    .ConfigureAwait(false);

            var reversedDefinition =
                OmsiSplineTrafficFlowReverser
                    .ReverseVehiclePaths(
                        sourceDocument);

            if (
                reversedDefinition
                    .ReversedPathCount ==
                0)
            {
                throw new InvalidDataException(
                    "splineHasNoOneWayVehiclePaths");
            }

            reversedPathCount =
                reversedDefinition
                    .ReversedPathCount;

            if (
                File.Exists(
                    targetFullPath))
            {
                var backupRoot =
                    Path.Combine(
                        root,
                        ".mapstudio-backups",
                        DateTimeOffset.UtcNow
                            .ToString(
                                "yyyyMMdd-HHmmssfff'Z'",
                                CultureInfo.InvariantCulture) +
                        "-flow-reverse-" +
                        Guid.NewGuid()
                            .ToString("N"));

                await SafeFileTransaction
                    .WriteAllAsync(
                        [
                            new PendingFileWrite(
                                targetFullPath,
                                Path.Combine(
                                    backupRoot,
                                    Path.GetFileName(
                                        targetFullPath)),
                                reversedDefinition
                                    .Bytes)
                        ],
                        cancellationToken)
                    .ConfigureAwait(false);

                LastBackupDirectory =
                    backupRoot;
            }
            else
            {
                var temp =
                    Path.Combine(
                        sourceDirectory,
                        "." +
                        Path.GetFileName(
                            targetFullPath) +
                        ".mapstudio-" +
                        Guid.NewGuid()
                            .ToString("N") +
                        ".tmp");

                try
                {
                    await File
                        .WriteAllBytesAsync(
                            temp,
                            reversedDefinition
                                .Bytes,
                            cancellationToken)
                        .ConfigureAwait(false);

                    File.Move(
                        temp,
                        targetFullPath,
                        overwrite:
                            false);
                }
                finally
                {
                    if (
                        File.Exists(
                            temp))
                    {
                        try
                        {
                            File.Delete(
                                temp);
                        }
                        catch
                        {
                        }
                    }
                }
            }

            reversed =
                true;
        }

        var relative =
            Path.GetRelativePath(
                    root,
                    targetFullPath)
                .Replace(
                    Path.DirectorySeparatorChar,
                    '\\');

        var snapshot =
            await ReplacePlacedSplinePathAsync(
                    selection,
                    relative,
                    cancellationToken)
                .ConfigureAwait(false);

        return new NativeSplineFlowToggleResult(
            snapshot,
            relative,
            reversed,
            reversedPathCount);
    }

    public async Task<NativeMapSnapshot>
        ReplacePlacedSplinePathAsync(
            NativeSelectionInfo selection,
            string replacementPath,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentNullException.ThrowIfNull(
            selection);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            replacementPath);

        if (
            selection.Kind !=
                PickingKind.Spline ||
            selection.PreviousSplineId is
                not int previousSplineId ||
            selection.NextSplineId is
                not int nextSplineId ||
            selection.IsHeightSpline is
                not bool isHeightSpline)
        {
            throw new InvalidDataException(
                "splinePathSelectionInvalid");
        }

        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        if (_pendingTransforms.Count > 0)
        {
            throw new InvalidOperationException(
                "savePendingBeforeSplinePathReplace");
        }

        var loaded =
            snapshot.Tiles
                .FirstOrDefault(
                    tile =>
                        tile.Reference.X ==
                            selection.TileX &&
                        tile.Reference.Y ==
                            selection.TileY)
            ?? throw new InvalidDataException(
                "splinePathTileNotLoaded");

        var source =
            loaded.Content.Splines
                .FirstOrDefault(
                    spline =>
                        spline.SplineId ==
                            selection.EntityId &&
                        string.Equals(
                            spline.SplinePath,
                            selection.AssetPath,
                            StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException(
                "splineSourceChanged");

        if (
            source.IsHeightSpline !=
                isHeightSpline)
        {
            throw new InvalidDataException(
                "splineSourceChanged");
        }

        if (
            !OmsiMapPathResolver
                .TryResolveTilePath(
                    snapshot.Map.DirectoryPath,
                    loaded.Reference
                        .RelativeMapPath,
                    out var tilePath) ||
            !File.Exists(
                tilePath))
        {
            throw new InvalidDataException(
                "splinePathTilePathInvalid");
        }

        var document =
            await OmsiConfigParser
                .ParseFileAsync(
                    tilePath,
                    cancellationToken)
                .ConfigureAwait(false);

        var result =
            OmsiPlacedSplinePathEditor
                .ReplacePath(
                    document,
                    source.SourceSectionOrdinal,
                    source.SplinePath,
                    replacementPath,
                    source.SplineId,
                    previousSplineId,
                    nextSplineId,
                    isHeightSpline);

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
                                string.Equals(
                                    tile.Reference.RelativeMapPath,
                                    loaded.Reference.RelativeMapPath,
                                    StringComparison.OrdinalIgnoreCase)
                                    ? new NativeLoadedTile(
                                        tile.Reference,
                                        refreshed)
                                    : tile)
                        .ToArray()
            };

        return CurrentMap;
    }

    public async Task<NativeSplineAdvancedUpdateResult>
        UpdateSplineAdvancedAsync(
            NativeSelectionInfo selection,
            double cantStart,
            double cantEnd,
            bool isMirrored,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentNullException.ThrowIfNull(
            selection);

        if (
            selection.Kind !=
                PickingKind.Spline ||
            !double.IsFinite(
                cantStart) ||
            !double.IsFinite(
                cantEnd))
        {
            throw new InvalidDataException(
                "invalidSplineAdvancedEdit");
        }

        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        if (_pendingTransforms.Count > 0)
        {
            throw new InvalidOperationException(
                "savePendingBeforeSplineAdvancedEdit");
        }

        var loaded =
            snapshot.Tiles
                .FirstOrDefault(
                    tile =>
                        tile.Reference.X ==
                            selection.TileX &&
                        tile.Reference.Y ==
                            selection.TileY)
            ?? throw new InvalidDataException(
                "splineAdvancedTileNotLoaded");

        var source =
            loaded.Content.Splines
                .FirstOrDefault(
                    spline =>
                        spline.SplineId ==
                            selection.EntityId &&
                        string.Equals(
                            spline.SplinePath,
                            selection.AssetPath,
                            StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException(
                "splineSourceChanged");

        if (
            !OmsiMapPathResolver
                .TryResolveTilePath(
                    snapshot.Map.DirectoryPath,
                    loaded.Reference
                        .RelativeMapPath,
                    out var tilePath) ||
            !File.Exists(
                tilePath))
        {
            throw new InvalidDataException(
                "splineAdvancedTilePathInvalid");
        }

        var document =
            await OmsiConfigParser
                .ParseFileAsync(
                    tilePath,
                    cancellationToken)
                .ConfigureAwait(false);

        var result =
            OmsiTileSplineAdvancedEditor
                .Apply(
                    document,
                    [
                        new OmsiSplineAdvancedEdit(
                            source.SourceSectionOrdinal,
                            source.SplinePath,
                            source.SplineId,
                            source.PreviousSplineId,
                            source.NextSplineId,
                            source.IsHeightSpline,
                            cantStart,
                            cantEnd,
                            isMirrored)
                    ]);

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
                        result.Bytes)
                ],
                cancellationToken)
            .ConfigureAwait(false);

        var refreshed =
            await _tileReader
                .ReadContentAsync(
                    tilePath,
                    cancellationToken)
                .ConfigureAwait(false);

        var updatedSpline =
            refreshed.Splines
                .FirstOrDefault(
                    spline =>
                        spline.SplineId ==
                            source.SplineId &&
                        string.Equals(
                            spline.SplinePath,
                            source.SplinePath,
                            StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException(
                "splineAdvancedReloadFailed");

        CurrentMap =
            snapshot with
            {
                Tiles =
                    snapshot.Tiles
                        .Select(
                            tile =>
                                tile.Reference.X ==
                                    loaded.Reference.X &&
                                tile.Reference.Y ==
                                    loaded.Reference.Y
                                    ? new NativeLoadedTile(
                                        tile.Reference,
                                        refreshed)
                                    : tile)
                        .ToArray()
            };

        return new NativeSplineAdvancedUpdateResult(
            CurrentMap,
            updatedSpline,
            backupPath);
    }

    public async Task<NativeMapSnapshot>
        DeleteSelectionAsync(
            NativeSelectionInfo selection,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentNullException.ThrowIfNull(
            selection);

        LastDeleteBackupEntries =
            Array.Empty<NativeDeleteBackupEntry>();

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

    public async Task<NativeMapSnapshot>
        RestoreDeleteBackupsAsync(
            IReadOnlyList<NativeDeleteBackupEntry> backups,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentNullException.ThrowIfNull(
            backups);

        if (backups.Count == 0)
        {
            throw new ArgumentException(
                "deleteBackupEmpty",
                nameof(backups));
        }

        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        if (_pendingTransforms.Count > 0)
        {
            throw new InvalidOperationException(
                "savePendingBeforeDeleteRestore");
        }

        var mapRoot =
            Path.GetFullPath(
                snapshot.Map.DirectoryPath);

        var backupsRoot =
            Path.GetFullPath(
                Path.Combine(
                    mapRoot,
                    ".mapstudio-backups"));

        var normalizedTargets =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        var writes =
            new List<PendingFileWrite>(
                backups.Count);

        foreach (
            var entry in
                backups)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            var target =
                Path.GetFullPath(
                    entry.TargetPath);

            var backup =
                Path.GetFullPath(
                    entry.BackupPath);

            var relativeTarget =
                Path.GetRelativePath(
                    mapRoot,
                    target);

            var relativeBackup =
                Path.GetRelativePath(
                    backupsRoot,
                    backup);

            if (
                !IsSafeRelativePath(
                    relativeTarget) ||
                !IsSafeRelativePath(
                    relativeBackup) ||
                !normalizedTargets.Add(
                    target) ||
                !File.Exists(
                    target) ||
                !File.Exists(
                    backup))
            {
                throw new InvalidDataException(
                    "invalidDeleteBackup");
            }

            writes.Add(
                new PendingFileWrite(
                    target,
                    CreateNativeBackupPath(
                        mapRoot,
                        target),
                    await File
                        .ReadAllBytesAsync(
                            backup,
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

        LastDeleteBackupEntries =
            Array.Empty<NativeDeleteBackupEntry>();

        return
            CurrentMap ??
            throw new InvalidOperationException(
                "deleteRestoreReloadFailed");
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
                        result.Bytes)
                ],
                cancellationToken)
            .ConfigureAwait(false);

        LastDeleteBackupEntries =
            new[]
            {
                new NativeDeleteBackupEntry(
                    tilePath,
                    backupPath)
            };

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
        SelectStandaloneWorkspaceAsync(
            string? rootPath = null,
            CancellationToken cancellationToken =
                default)
    {
        var requestedRoot =
            string.IsNullOrWhiteSpace(
                rootPath)
                ? MapStudioWorkspaceBootstrapper
                    .GetDefaultWorkspacePath()
                : rootPath;

        var info =
            await _workspaceBootstrapper
                .EnsureAsync(
                    requestedRoot!,
                    seedStarterAssets:
                        true,
                    cancellationToken)
                .ConfigureAwait(false);

        return await SelectContentRootAsync(
                info.RootPath,
                NativeContentRootKind
                    .StandaloneWorkspace,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<NativeMapSnapshot>
        CreateStandaloneMapAsync(
            string directoryName,
            string displayName,
            CancellationToken cancellationToken =
                default)
    {
        if (!IsStandaloneWorkspace)
        {
            await SelectStandaloneWorkspaceAsync(
                    cancellationToken:
                        cancellationToken)
                .ConfigureAwait(false);
        }

        var root =
            OmsiRootPath ??
            throw new InvalidOperationException(
                "Workspace Map Studio não disponível.");

        var mapDirectory =
            await _workspaceBootstrapper
                .CreateBlankMapAsync(
                    root,
                    directoryName,
                    displayName,
                    cancellationToken)
                .ConfigureAwait(false);

        Maps =
            await new OmsiMapCatalog()
                .DiscoverAsync(
                    root,
                    cancellationToken)
                .ConfigureAwait(false);

        return await OpenMapAsync(
                mapDirectory,
                loadFullMap:
                    true,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<NativeMapSnapshot>
        ImportStandaloneMapFolderAsync(
            string sourcePath,
            CancellationToken cancellationToken =
                default)
    {
        if (_pendingTransforms.Count > 0)
        {
            throw new InvalidOperationException(
                "savePendingBeforeMapImport");
        }

        if (!IsStandaloneWorkspace)
        {
            await SelectStandaloneWorkspaceAsync(
                    cancellationToken:
                        cancellationToken)
                .ConfigureAwait(false);
        }

        var root =
            OmsiRootPath ??
            throw new InvalidOperationException(
                "Workspace Map Studio não disponível.");

        var imported =
            await _workspaceBootstrapper
                .ImportMapFolderAsync(
                    root,
                    sourcePath,
                    cancellationToken)
                .ConfigureAwait(false);

        Maps =
            await new OmsiMapCatalog()
                .DiscoverAsync(
                    root,
                    cancellationToken)
                .ConfigureAwait(false);

        return await OpenMapAsync(
                imported.MapDirectory,
                loadFullMap:
                    true,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<MapStudioWorkspaceImportResult>
        ImportWorkspaceAssetFolderAsync(
            string sourcePath,
            CancellationToken cancellationToken =
                default)
    {
        if (!IsStandaloneWorkspace)
        {
            await SelectStandaloneWorkspaceAsync(
                    cancellationToken:
                        cancellationToken)
                .ConfigureAwait(false);
        }

        var root =
            OmsiRootPath ??
            throw new InvalidOperationException(
                "Workspace Map Studio não disponível.");

        var result =
            await _workspaceBootstrapper
                .ImportAssetFolderAsync(
                    root,
                    sourcePath,
                    cancellationToken)
                .ConfigureAwait(false);

        await RefreshAssetLibraryAsync(
                cancellationToken:
                    cancellationToken)
            .ConfigureAwait(false);

        return result;
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
                @"A pasta selecionada não contém a estrutura OMSI 2\maps.");
        }

        return await SelectContentRootAsync(
                normalized,
                NativeContentRootKind
                    .OmsiInstallation,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<
        IReadOnlyList<OmsiMapDescriptor>>
        SelectContentRootAsync(
            string rootPath,
            NativeContentRootKind kind,
            CancellationToken cancellationToken)
    {
        var normalized =
            Path.GetFullPath(
                rootPath)
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);

        Directory.CreateDirectory(
            Path.Combine(
                normalized,
                "maps"));

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

        OmsiRootPath =
            normalized;

        ContentRootKind =
            kind;

        Maps =
            maps;

        CurrentMap =
            null;

        _pendingTransforms
            .Clear();

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
                "Selecione primeiro o Workspace Map Studio ou uma instalação do OMSI 2.");

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
                "Selecione primeiro o Workspace Map Studio ou uma instalação do OMSI 2.");

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

    private static string NormalizeTimetableBaseName(
        string name,
        string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            name);

        var trimmed =
            name.Trim();

        if (
            trimmed.IndexOfAny(
                Path.GetInvalidFileNameChars()) >=
                    0 ||
            trimmed.Contains(
                Path.DirectorySeparatorChar) ||
            trimmed.Contains(
                Path.AltDirectorySeparatorChar))
        {
            throw new InvalidDataException(
                "invalidTimetableFileName");
        }

        if (
            trimmed.EndsWith(
                extension,
                StringComparison.OrdinalIgnoreCase))
        {
            trimmed =
                trimmed[
                    ..^extension.Length];
        }

        if (
            string.IsNullOrWhiteSpace(
                trimmed) ||
            !string.Equals(
                Path.GetFileName(
                    trimmed),
                trimmed,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "invalidTimetableFileName");
        }

        return trimmed;
    }

    public async Task<NativeSplinePathUpdateResult>
        AddSplinePathAsync(
            string assetPath,
            OmsiSplinePathDefinition path,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            assetPath);

        ArgumentNullException.ThrowIfNull(
            path);

        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        var root =
            OmsiRootPath ??
            throw new InvalidOperationException(
                "Nenhuma fonte de conteúdo ativa.");

        if (
            _pendingTransforms.Count >
                0)
        {
            throw new InvalidOperationException(
                "savePendingBeforeSplinePathEdit");
        }

        if (
            !OmsiSplinePathResolver
                .TryResolve(
                    root,
                    assetPath,
                    out var target) ||
            !File.Exists(
                target))
        {
            throw new FileNotFoundException(
                "splinePathAssetMissing",
                assetPath);
        }

        var document =
            await OmsiConfigParser
                .ParseFileAsync(
                    target,
                    cancellationToken)
                .ConfigureAwait(false);

        var definition =
            new OmsiSplineDefinitionReader()
                .Read(
                    document);

        var newOrdinal =
            definition.Paths.Count;

        var bytes =
            new OmsiSplinePathPatcher()
                .AppendNew(
                    document,
                    path);

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
            new OmsiSplineDefinitionReader()
                .Read(
                    await OmsiConfigParser
                        .ParseFileAsync(
                            target,
                            cancellationToken)
                        .ConfigureAwait(false));

        if (
            reloaded.Paths.Count !=
                newOrdinal +
                    1)
        {
            throw new InvalidDataException(
                "splinePathReloadFailed");
        }

        return new NativeSplinePathUpdateResult(
            reloaded.Paths[
                newOrdinal],
            target,
            backupPath);
    }

    public async Task<NativeAssetPathDeleteResult>
        DeleteSplinePathAsync(
            string assetPath,
            int pathOrdinal,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            assetPath);

        var snapshot =
            CurrentMap ??
            throw new InvalidOperationException(
                "Nenhum mapa OMSI está aberto.");

        var root =
            OmsiRootPath ??
            throw new InvalidOperationException(
                "Nenhuma fonte de conteúdo ativa.");

        if (
            _pendingTransforms.Count >
                0)
        {
            throw new InvalidOperationException(
                "savePendingBeforeSplinePathEdit");
        }

        if (
            !OmsiSplinePathResolver
                .TryResolve(
                    root,
                    assetPath,
                    out var target) ||
            !File.Exists(
                target))
        {
            throw new FileNotFoundException(
                "splinePathAssetMissing",
                assetPath);
        }

        var document =
            await OmsiConfigParser
                .ParseFileAsync(
                    target,
                    cancellationToken)
                .ConfigureAwait(false);

        var definition =
            new OmsiSplineDefinitionReader()
                .Read(
                    document);

        if (
            pathOrdinal < 0 ||
            pathOrdinal >=
                definition.Paths.Count)
        {
            throw new InvalidDataException(
                "splinePathOrdinalInvalid");
        }

        var bytes =
            new OmsiSplinePathPatcher()
                .Remove(
                    document,
                    pathOrdinal);

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
            new OmsiSplineDefinitionReader()
                .Read(
                    await OmsiConfigParser
                        .ParseFileAsync(
                            target,
                            cancellationToken)
                        .ConfigureAwait(false));

        if (
            reloaded.Paths.Count !=
                definition.Paths.Count -
                    1)
        {
            throw new InvalidDataException(
                "splinePathReloadFailed");
        }

        return new NativeAssetPathDeleteResult(
            target,
            backupPath,
            reloaded.Paths.Count);
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
            await _tileContentCache
                .ReadContentAsync(
                    tilePath,
                    cancellationToken)
                .ConfigureAwait(false);

        return new NativeLoadedTile(
            tile,
            content);
    }
}
