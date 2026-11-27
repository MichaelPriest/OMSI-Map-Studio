using System.Buffers.Binary;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
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
        void Put(int x, int y, (byte R, byte G, byte B) color)
        {
            var i = (y * width + x) * 3;
            rgb[i] = color.R;
            rgb[i + 1] = color.G;
            rgb[i + 2] = color.B;
        }

        void FillArea(IEnumerable<IReadOnlyList<MapStudioRoadPoint>> contours, (byte, byte, byte) color)
        {
            var rings = contours.Where(ring => ring.Count >= 3)
                .Select(ring => ring.Select(Pixel).ToArray()).ToArray();
            if (rings.Length == 0) return;
            var top = (int)Math.Clamp(Math.Floor(rings.Min(ring => ring.Min(p => p.Y))), 0, height);
            var bottom = (int)Math.Clamp(Math.Ceiling(rings.Max(ring => ring.Max(p => p.Y))), 0, height);
            var crossings = new List<double>();
            for (var y = top; y < bottom; y++)
            {
                crossings.Clear();
                var scanY = y + 0.5;
                foreach (var ring in rings)
                {
                    var a = ring[^1];
                    foreach (var b in ring)
                    {
                        if ((a.Y > scanY) != (b.Y > scanY))
                            crossings.Add(a.X + (scanY - a.Y) * (b.X - a.X) / (b.Y - a.Y));
                        a = b;
                    }
                }
                crossings.Sort();
                for (var i = 0; i + 1 < crossings.Count; i += 2)
                {
                    var left = (int)Math.Clamp(Math.Ceiling(crossings[i] - 0.5), 0, width);
                    var right = (int)Math.Clamp(Math.Ceiling(crossings[i + 1] - 0.5), 0, width);
                    for (var x = left; x < right; x++) Put(x, y, color);
                }
            }
        }

        void DrawLine((double X, double Y)[] points, double radius, (byte, byte, byte) color)
        {
            for (var i = 1; i < points.Length; i++)
            {
                var a = points[i - 1];
                var b = points[i];
                var left = (int)Math.Clamp(Math.Floor(Math.Min(a.X, b.X) - radius), 0, width);
                var right = (int)Math.Clamp(Math.Ceiling(Math.Max(a.X, b.X) + radius), 0, width);
                var top = (int)Math.Clamp(Math.Floor(Math.Min(a.Y, b.Y) - radius), 0, height);
                var bottom = (int)Math.Clamp(Math.Ceiling(Math.Max(a.Y, b.Y) + radius), 0, height);
                var dx = b.X - a.X;
                var dy = b.Y - a.Y;
                var lengthSquared = dx * dx + dy * dy;
                for (var y = top; y < bottom; y++)
                for (var x = left; x < right; x++)
                {
                    var t = lengthSquared == 0 ? 0 : Math.Clamp(
                        ((x + 0.5 - a.X) * dx + (y + 0.5 - a.Y) * dy) / lengthSquared, 0, 1);
                    var ex = x + 0.5 - a.X - t * dx;
                    var ey = y + 0.5 - a.Y - t * dy;
                    if (ex * ex + ey * ey <= radius * radius) Put(x, y, color);
                }
            }
        }

        using var text = new StringReader(osmXml);
        using var reader = XmlReader.Create(text, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 128L * 1024 * 1024
        });
        var document = XDocument.Load(reader);
        var buildings = new MapStudioOsmBuildingProjector().Project(
            new MapStudioOsmBuildingImporter().Parse(document).Buildings, anchor);
        var water = MapStudioOsmHydrography.Read(document.Root!, anchor);
        (byte, byte, byte) waterColor = (150, 199, 219);
        foreach (var area in water.Areas) FillArea(area, waterColor);
        foreach (var waterway in water.Waterways)
            DrawLine(waterway.Points.Select(Pixel).ToArray(),
                Math.Max(0.75, waterway.WidthMeters / (2 * metersPerPixel)), waterColor);
        foreach (var building in buildings) FillArea([building.Points], (186, 177, 168));

        var roads = new MapStudioOsmRoadImporter().Parse(document).Traces
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
                (byte, byte, byte) color = (pass, main) switch
                {
                    (0, _) => (143, 137, 126),
                    (_, true) => (252, 223, 161),
                    _ => (255, 255, 255)
                };
                DrawLine(item.Points, radius, color);
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
