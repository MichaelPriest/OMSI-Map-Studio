using System.Buffers.Binary;
using System.IO.Compression;
using MapStudio.Core.Generation.Buildings;
using MapStudio.Core.Generation.Roads;

namespace MapStudio.Core.Generation.Scene;

/// <summary>Draws a local, north-up reference from OSM vector data, without a raster tile service.</summary>
public static class MapStudioOsmVectorReferenceRaster
{
    public static byte[] RenderPng(string osmXml, MapStudioGeographicAnchor anchor,
        double minX, double minZ, int width, int height, double metersPerPixel)
    {
        var rgb = RenderRgb(osmXml, anchor, minX, minZ, width, height, metersPerPixel);
        using var output = new MemoryStream();
        output.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8;
        header[9] = 2; // RGB
        WriteChunk(output, "IHDR", header);
        using var compressed = new MemoryStream();
        using (var z = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            for (var y = 0; y < height; y++)
            {
                z.WriteByte(0);
                z.Write(rgb, y * width * 3, width * 3);
            }
        }
        WriteChunk(output, "IDAT", compressed.ToArray());
        WriteChunk(output, "IEND", Array.Empty<byte>());
        return output.ToArray();
    }

    public static byte[] RenderRgb(string osmXml, MapStudioGeographicAnchor anchor,
        double minX, double minZ, int width, int height, double metersPerPixel)
    {
        if (width is < 1 or > 2048 || height is < 1 or > 2048 ||
            !double.IsFinite(minX) || !double.IsFinite(minZ) ||
            !double.IsFinite(metersPerPixel) || metersPerPixel < 0.01)
            throw new ArgumentOutOfRangeException(nameof(width));

        var rgb = new byte[checked(width * height * 3)];
        for (var i = 0; i < rgb.Length; i += 3)
        {
            rgb[i] = 235; rgb[i + 1] = 233; rgb[i + 2] = 224;
        }
        (double X, double Y) Pixel(MapStudioRoadPoint p) =>
            ((p.X - minX) / metersPerPixel, (p.Z - minZ) / metersPerPixel);
        void Put(int x, int y, byte r, byte g, byte b)
        {
            var i = (y * width + x) * 3;
            rgb[i] = r; rgb[i + 1] = g; rgb[i + 2] = b;
        }

        var buildings = new MapStudioOsmBuildingProjector().Project(
            new MapStudioOsmBuildingImporter().Parse(osmXml).Buildings, anchor);
        foreach (var building in buildings)
        {
            var p = building.Points.Select(Pixel).ToArray();
            if (p.Length < 3) continue;
            var top = Math.Max(0, Math.Min(height, (int)Math.Floor(p.Min(v => v.Y))));
            var bottom = Math.Max(0, Math.Min(height, (int)Math.Ceiling(p.Max(v => v.Y))));
            for (var y = top; y < bottom; y++)
            {
                var crossings = new List<double>();
                for (var i = 0; i < p.Length; i++)
                {
                    var a = p[i]; var b = p[(i + 1) % p.Length];
                    if ((a.Y > y + .5) == (b.Y > y + .5)) continue;
                    crossings.Add(a.X + (y + .5 - a.Y) * (b.X - a.X) / (b.Y - a.Y));
                }
                crossings.Sort();
                for (var i = 0; i + 1 < crossings.Count; i += 2)
                {
                    var left = Math.Max(0, Math.Min(width, (int)Math.Ceiling(crossings[i] - .5)));
                    var right = Math.Max(0, Math.Min(width, (int)Math.Ceiling(crossings[i + 1] - .5)));
                    for (var x = left; x < right; x++) Put(x, y, 186, 177, 168);
                }
            }
        }

        var roads = new MapStudioOsmRoadImporter().Parse(osmXml).Traces
            .Where(t => !t.Tunnel).Select(t => (Road: t, Points: t.Points
                .Select(p => Pixel(MapStudioGeographicProjection.Project(anchor, p))).ToArray()))
            .ToArray();
        // Draw casings first, then the road surface, keeping connected streets continuous.
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var item in roads)
            {
                var roadWidth = item.Road.WidthMeters ?? Math.Clamp((item.Road.LaneCount ?? 2) * 3.2, 3, 28);
                if (!double.IsFinite(roadWidth)) roadWidth = 6.4;
                var radius = Math.Clamp(roadWidth / metersPerPixel / 2, 1, 100) + (pass == 0 ? 1 : 0);
                var main = item.Road.Highway is "motorway" or "trunk" or "primary" or "secondary";
                for (var i = 1; i < item.Points.Length; i++)
                {
                    var a = item.Points[i - 1]; var b = item.Points[i];
                    var left = Math.Max(0, Math.Min(width, (int)Math.Floor(Math.Min(a.X, b.X) - radius)));
                    var right = Math.Max(0, Math.Min(width, (int)Math.Ceiling(Math.Max(a.X, b.X) + radius)));
                    var top = Math.Max(0, Math.Min(height, (int)Math.Floor(Math.Min(a.Y, b.Y) - radius)));
                    var bottom = Math.Max(0, Math.Min(height, (int)Math.Ceiling(Math.Max(a.Y, b.Y) + radius)));
                    var dx = b.X - a.X; var dy = b.Y - a.Y; var d2 = dx * dx + dy * dy;
                    for (var y = top; y < bottom; y++)
                    for (var x = left; x < right; x++)
                    {
                        var t = d2 > 0 ? Math.Clamp(((x + .5 - a.X) * dx + (y + .5 - a.Y) * dy) / d2, 0, 1) : 0;
                        var ex = x + .5 - a.X - t * dx;
                        var ey = y + .5 - a.Y - t * dy;
                        if (ex * ex + ey * ey <= radius * radius)
                        {
                            if (pass == 0) Put(x, y, 143, 137, 126);
                            else if (main) Put(x, y, 252, 223, 161);
                            else Put(x, y, 255, 255, 255);
                        }
                    }
                }
            }
        }
        return rgb;
    }

    private static void WriteChunk(Stream output, string name, byte[] data)
    {
        Span<byte> size = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(size, data.Length);
        output.Write(size);
        var type = System.Text.Encoding.ASCII.GetBytes(name);
        output.Write(type);
        output.Write(data);
        uint crc = 0xffffffff;
        foreach (var value in type.Concat(data))
        {
            crc ^= value;
            for (var i = 0; i < 8; i++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xedb88320u : crc >> 1;
        }
        BinaryPrimitives.WriteUInt32BigEndian(size, ~crc);
        output.Write(size);
    }
}
