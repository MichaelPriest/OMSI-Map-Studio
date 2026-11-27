namespace MapStudio.Core.Generation.Terrain;

public sealed class MapStudioTerrariumElevationClient(HttpClient http, Func<byte[], byte[]?> decodeRgba)
{
    public const string Attribution = "AWS Terrain Tiles / Mapzen (Tilezen)";
    public static double DecodeHeight(byte r, byte g, byte b) => r * 256.0 + g + b / 256.0 - 32768.0;
    public static (double X, double Y) PixelCoordinates(double lat, double lon, int zoom)
    {
        if (!double.IsFinite(lat) || !double.IsFinite(lon) || Math.Abs(lat) > 85.05112878 || Math.Abs(lon) > 180 || zoom is < 0 or > 15)
            throw new InvalidDataException("terrariumCoordinateInvalid");
        var size = 256.0 * (1 << zoom);
        var phi = lat * Math.PI / 180;
        return (Math.Clamp((lon + 180) / 360 * size - 0.5, 0, size - 1),
            Math.Clamp((1 - Math.Asinh(Math.Tan(phi)) / Math.PI) / 2 * size - 0.5, 0, size - 1));
    }

    public async Task<MapStudioGeoreferencedElevationSurface> DownloadAsync(double south, double west, double north, double east,
        int rows = 17, int columns = 17, IProgress<MapStudioElevationDownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (south >= north || west >= east || rows is < 2 or > 65 || columns is < 2 or > 65)
            throw new InvalidDataException("terrariumBoundsInvalid");
        const int zoom = 15;
        var nw = PixelCoordinates(north, west, zoom);
        var se = PixelCoordinates(south, east, zoom);
        var minX = (int)nw.X / 256;
        var minY = (int)nw.Y / 256;
        var maxX = (int)Math.Ceiling(se.X) / 256;
        var maxY = (int)Math.Ceiling(se.Y) / 256;
        if ((long)(maxX-minX+1)*(maxY-minY+1)>256) throw new InvalidDataException("terrariumAreaTooLarge");
        var cache = new Dictionary<(int,int), byte[]>();
        async Task<double> Pixel(int x, int y)
        {
            var key = (x/256,y/256);
            if (!cache.TryGetValue(key,out var rgba))
            {
                using var response = await http.GetAsync($"https://s3.amazonaws.com/elevation-tiles-prod/terrarium/{zoom}/{key.Item1}/{key.Item2}.png", HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength > 2*1024*1024) throw new InvalidDataException("terrariumTileTooLarge");
                await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                using var buffer = new MemoryStream();
                var chunk = new byte[8192];
                int read;
                while ((read = await input.ReadAsync(chunk.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
                {
                    if (buffer.Length + read > 2*1024*1024) throw new InvalidDataException("terrariumTileTooLarge");
                    buffer.Write(chunk,0,read);
                }
                rgba = decodeRgba(buffer.ToArray()) ?? throw new InvalidDataException("terrariumPngInvalid");
                if (rgba.Length != 256*256*4) throw new InvalidDataException("terrariumPngDimensions");
                cache[key] = rgba;
            }
            var index = ((y%256)*256+x%256)*4;
            if (rgba[index+3] != 255) throw new InvalidDataException("terrariumMissingData");
            return DecodeHeight(rgba[index],rgba[index+1],rgba[index+2]);
        }
        var elevations = new List<double>(rows*columns);
        for (int row=0;row<rows;row++)
        {
            for (int col=0;col<columns;col++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var (x,y)=PixelCoordinates(north+(south-north)*row/(rows-1),west+(east-west)*col/(columns-1),zoom);
                int x0=(int)Math.Floor(x), y0=(int)Math.Floor(y), x1=(int)Math.Ceiling(x), y1=(int)Math.Ceiling(y);
                var a=await Pixel(x0,y0).ConfigureAwait(false);
                var b=await Pixel(x1,y0).ConfigureAwait(false);
                var c=await Pixel(x0,y1).ConfigureAwait(false);
                var d=await Pixel(x1,y1).ConfigureAwait(false);
                var top=a+(b-a)*(x-x0); var bottom=c+(d-c)*(x-x0);
                elevations.Add(top+(bottom-top)*(y-y0));
            }
            progress?.Report(new MapStudioElevationDownloadProgress(elevations.Count,rows*columns));
        }
        return new MapStudioGeoreferencedElevationSurface(new MapStudioElevationGrid(rows,columns,elevations,elevations.Min(),elevations.Max(),"aws-terrarium"),south,west,north,east,elevations.Min());
    }
}
