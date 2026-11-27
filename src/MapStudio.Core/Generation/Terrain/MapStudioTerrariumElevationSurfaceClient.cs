using MapStudio.Core.IO;
namespace MapStudio.Core.Generation.Terrain;
public sealed class MapStudioTerrariumElevationSurfaceClient
{
    public const string Attribution = "Mapzen Terrain Tiles on AWS Open Data";
    private static readonly HttpClient Shared = new() { Timeout = TimeSpan.FromSeconds(40) };
    private readonly HttpClient _http;
    private readonly string _cacheRoot;
    public MapStudioTerrariumElevationSurfaceClient() : this(Shared, Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OMSI Map Studio", "reference-cache", "terrarium")) { }
    public MapStudioTerrariumElevationSurfaceClient(HttpClient http, string cacheRoot)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _cacheRoot = cacheRoot;
    }
    public static double DecodeHeight(byte r, byte g, byte b) =>
        r * 256.0 + g + b / 256.0 - 32768.0;

    public async Task<MapStudioGeoreferencedElevationSurface> DownloadAsync(
        double south, double west, double north, double east,
        int rows = 65, int columns = 65,
        IProgress<MapStudioElevationDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!double.IsFinite(south) || !double.IsFinite(west) || !double.IsFinite(north) ||
            !double.IsFinite(east) || south < -85.05112878 || north > 85.05112878 ||
            west < -180 || east > 180 || south >= north || west >= east ||
            rows is < 2 or > 129 || columns is < 2 or > 129)
            throw new InvalidDataException("invalidTerrariumElevationRequest");

        int zoom = 15;
        double PixelX(double lon) => (lon + 180) / 360 * (256L << zoom) - .5;
        double PixelY(double lat) => (1 - Math.Asinh(Math.Tan(lat * Math.PI / 180)) / Math.PI) / 2 *
            (256L << zoom) - .5;
        while (zoom > 0 &&
            (Math.Ceiling((PixelX(east) - PixelX(west)) / 256) + 2) *
            (Math.Ceiling((PixelY(south) - PixelY(north)) / 256) + 2) > 100)
            zoom--;
        var side = 256L << zoom;
        var tiles = new Dictionary<(long X, long Y), MapStudioDecodedPng>();
        async Task<double> GetHeight(long px, long py)
        {
            px = (px % side + side) % side;
            py = Math.Clamp(py, 0, side - 1);
            var key = (px / 256, py / 256);
            if (!tiles.TryGetValue(key, out var tile))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var directory = Path.Combine(_cacheRoot, zoom.ToString(), key.Item1.ToString());
                var path = Path.Combine(directory, key.Item2 + ".png");
                tile = MapStudioPngDecoder.TryDecodeFile(path);
                if (tile is null || tile.Width != 256 || tile.Height != 256)
                {
                    using var response = await _http.GetAsync(
                        $"https://s3.amazonaws.com/elevation-tiles-prod/terrarium/{zoom}/{key.Item1}/{key.Item2}.png",
                        cancellationToken).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                    if (bytes.Length > 2 * 1024 * 1024 ||
                        MapStudioPngDecoder.TryDecode(bytes) is not { Width: 256, Height: 256 } decoded)
                        throw new InvalidDataException("invalidTerrariumPng");
                    tile = decoded;
                    Directory.CreateDirectory(directory);
                    var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    try
                    {
                        await File.WriteAllBytesAsync(temp, bytes, cancellationToken).ConfigureAwait(false);
                        File.Move(temp, path, overwrite: true);
                    }
                    finally { if (File.Exists(temp)) File.Delete(temp); }
                }
                tiles.Add(key, tile);
            }
            var i = checked((int)((py % 256 * 256 + px % 256) * 4));
            if (tile.Rgba[i + 3] != 255) throw new InvalidDataException("missingTerrariumPixel");
            return DecodeHeight(tile.Rgba[i], tile.Rgba[i + 1], tile.Rgba[i + 2]);
        }
        var heights = new List<double>(rows * columns);
        for (var row = 0; row < rows; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var y = PixelY(north + (south - north) * row / (rows - 1));
            var iy = (long)Math.Floor(y);
            for (var col = 0; col < columns; col++)
            {
                var x = PixelX(west + (east - west) * col / (columns - 1));
                var ix = (long)Math.Floor(x);
                var a = await GetHeight(ix, iy).ConfigureAwait(false);
                var b = await GetHeight(ix + 1, iy).ConfigureAwait(false);
                var d = await GetHeight(ix, iy + 1).ConfigureAwait(false);
                var e = await GetHeight(ix + 1, iy + 1).ConfigureAwait(false);
                var fx = x - ix; var fy = y - iy;
                heights.Add((a + (b - a) * fx) * (1 - fy) + (d + (e - d) * fx) * fy);
            }
            progress?.Report(new MapStudioElevationDownloadProgress(heights.Count, rows * columns));
        }
        var min = heights.Min();
        return new MapStudioGeoreferencedElevationSurface(
            new MapStudioElevationGrid(rows, columns, heights, min, heights.Max(), "aws-terrarium"),
            south, west, north, east, min);
    }
}
