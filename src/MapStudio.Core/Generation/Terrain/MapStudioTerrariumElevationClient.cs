using MapStudio.Core.Generation.Roads;

namespace MapStudio.Core.Generation.Terrain;

public sealed class MapStudioTerrariumElevationClient(HttpClient http, Func<byte[], byte[]?> decodeRgba)
{
    public const string Attribution = "AWS Terrain Tiles / Mapzen (Tilezen)";
    public const int TileSize = 256;
    private const int Zoom = 15;
    private const int MaximumTileBytes = 2 * 1024 * 1024;
    private const int MaximumTiles = 256;

    public static double DecodeHeight(byte r, byte g, byte b) =>
        r * 256.0 + g + b / 256.0 - 32768.0;

    public static (double X, double Y) PixelCoordinates(double lat, double lon, int zoom)
    {
        if (!double.IsFinite(lat) || !double.IsFinite(lon) || Math.Abs(lat) > 85.05112878 ||
            Math.Abs(lon) > 180 || zoom is < 0 or > Zoom)
            throw new InvalidDataException("terrariumCoordinateInvalid");

        var size = (double)TileSize * (1 << zoom);
        var phi = lat * Math.PI / 180;
        return (Math.Clamp((lon + 180) / 360 * size - 0.5, 0, size - 1),
            Math.Clamp((1 - Math.Asinh(Math.Tan(phi)) / Math.PI) / 2 * size - 0.5, 0, size - 1));
    }

    public Task<MapStudioGeoreferencedElevationSurface> DownloadForCoverageAsync(
        MapStudioElevationCoverageBounds bounds, int? rows = null, int? columns = null,
        IProgress<MapStudioElevationDownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var center = new MapStudioGeographicAnchor((bounds.North + bounds.South) / 2,
            (bounds.West + bounds.East) / 2, 0, 0);
        var corner = MapStudioGeographicProjection.Project(center, new MapStudioGeoRoadPoint(bounds.North, bounds.East));
        // Aim for 20 m spacing over the whole map, within the supported grid size.
        static int Samples(double halfSpan) => (int)Math.Clamp(Math.Ceiling(Math.Abs(halfSpan) * 2 / 20) + 1, 17, 65);
        return DownloadAsync(bounds.South, bounds.West, bounds.North, bounds.East,
            rows ?? Samples(corner.Z), columns ?? Samples(corner.X), progress, cancellationToken);
    }

    public async Task<MapStudioGeoreferencedElevationSurface> DownloadAsync(
        double south, double west, double north, double east, int rows = 17, int columns = 17,
        IProgress<MapStudioElevationDownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (south >= north || west >= east || rows is < 2 or > 65 || columns is < 2 or > 65)
            throw new InvalidDataException("terrariumBoundsInvalid");

        var nw = PixelCoordinates(north, west, Zoom);
        var se = PixelCoordinates(south, east, Zoom);
        var tilesAcross = (int)Math.Ceiling(se.X) / TileSize - (int)nw.X / TileSize + 1;
        var tilesDown = (int)Math.Ceiling(se.Y) / TileSize - (int)nw.Y / TileSize + 1;
        if ((long)tilesAcross * tilesDown > MaximumTiles)
            throw new InvalidDataException("terrariumAreaTooLarge");

        var cache = new Dictionary<(int X, int Y), byte[]>();
        async ValueTask<double> ReadPixelAsync(int x, int y)
        {
            var tile = (X: x / TileSize, Y: y / TileSize);
            if (!cache.TryGetValue(tile, out var rgba))
            {
                rgba = await ReadTileAsync(tile.X, tile.Y, cancellationToken).ConfigureAwait(false);
                cache.Add(tile, rgba);
            }
            var index = (y % TileSize * TileSize + x % TileSize) * 4;
            if (rgba[index + 3] != 255) throw new InvalidDataException("terrariumMissingData");
            return DecodeHeight(rgba[index], rgba[index + 1], rgba[index + 2]);
        }

        var elevations = new List<double>(rows * columns);
        for (var row = 0; row < rows; row++)
        {
            var latitude = north + (south - north) * row / (rows - 1);
            for (var column = 0; column < columns; column++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var longitude = west + (east - west) * column / (columns - 1);
                var (x, y) = PixelCoordinates(latitude, longitude, Zoom);
                int x0 = (int)Math.Floor(x), x1 = (int)Math.Ceiling(x);
                int y0 = (int)Math.Floor(y), y1 = (int)Math.Ceiling(y);
                var a = await ReadPixelAsync(x0, y0).ConfigureAwait(false);
                var b = await ReadPixelAsync(x1, y0).ConfigureAwait(false);
                var c = await ReadPixelAsync(x0, y1).ConfigureAwait(false);
                var d = await ReadPixelAsync(x1, y1).ConfigureAwait(false);
                var top = a + (b - a) * (x - x0);
                var bottom = c + (d - c) * (x - x0);
                elevations.Add(top + (bottom - top) * (y - y0));
            }
            progress?.Report(new MapStudioElevationDownloadProgress(elevations.Count, rows * columns));
        }

        var minimum = elevations.Min();
        var grid = new MapStudioElevationGrid(rows, columns, elevations, minimum, elevations.Max(), "aws-terrarium");
        return new MapStudioGeoreferencedElevationSurface(grid, south, west, north, east, minimum);
    }

    private async Task<byte[]> ReadTileAsync(int x, int y, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(http.Timeout);
        try
        {
            var url = $"https://s3.amazonaws.com/elevation-tiles-prod/terrarium/{Zoom}/{x}/{y}.png";
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaximumTileBytes)
                throw new InvalidDataException("terrariumTileTooLarge");

            await using var input = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            int read;
            while ((read = await input.ReadAsync(chunk.AsMemory(), timeout.Token).ConfigureAwait(false)) > 0)
            {
                if (buffer.Length + read > MaximumTileBytes) throw new InvalidDataException("terrariumTileTooLarge");
                buffer.Write(chunk, 0, read);
            }

            var rgba = decodeRgba(buffer.ToArray()) ?? throw new InvalidDataException("terrariumPngInvalid");
            if (rgba.Length != TileSize * TileSize * 4) throw new InvalidDataException("terrariumPngDimensions");
            return rgba;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new HttpRequestException($"Tempo limite ao baixar o tile Terrarium {Zoom}/{x}/{y}.", ex);
        }
    }
}
