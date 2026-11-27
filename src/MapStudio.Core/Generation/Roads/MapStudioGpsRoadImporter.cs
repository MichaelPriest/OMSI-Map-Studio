using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace MapStudio.Core.Generation.Roads;

/// <summary>Reads route geometry only; no external references or altitude assumptions.</summary>
public sealed class MapStudioGpsRoadImporter
{
    public const int MaxInputBytes = 16 * 1024 * 1024;
    public const int MaxTraces = 5000;
    public const int MaxPoints = 100000;

    public MapStudioGeoJsonRoadImportResult Parse(string content, string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        if (content.Length > MaxInputBytes) throw new InvalidDataException("gpsInputTooLarge");
        var format = extension.ToLowerInvariant();
        if (format is ".json" or ".geojson")
        {
            var geo = new MapStudioGeoJsonRoadImporter().Parse(content);
            if (geo.Traces.Count > MaxTraces || geo.Traces.Sum(t => (long)t.Points.Count) > MaxPoints)
                throw new InvalidDataException("gpsLimitExceeded");
            return geo;
        }
        if (format is not (".gpx" or ".kml")) throw new InvalidDataException("gpsFormatUnsupported");
        using var source = new StringReader(content);
        using var reader = XmlReader.Create(source, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
            MaxCharactersInDocument = MaxInputBytes
        });
        var root = XDocument.Load(reader).Root ?? throw new InvalidDataException("gpsEmpty");
        var traces = new List<MapStudioGeoRoadTrace>();
        int count = 0, ignored = 0;
        if (format == ".gpx" && root.Name.LocalName == "gpx")
        {
            foreach (var track in root.Elements().Where(x => x.Name.LocalName == "trk"))
            {
                foreach (var segment in track.Elements().Where(x => x.Name.LocalName == "trkseg"))
                    Add(segment.Elements().Where(x => x.Name.LocalName == "trkpt")
                        .Select(p => Point((string?)p.Attribute("lat"), (string?)p.Attribute("lon"))), Name(track));
            }
            foreach (var route in root.Elements().Where(x => x.Name.LocalName == "rte"))
                Add(route.Elements().Where(x => x.Name.LocalName == "rtept")
                    .Select(p => Point((string?)p.Attribute("lat"), (string?)p.Attribute("lon"))), Name(route));
        }
        else if (format == ".kml" && root.Name.LocalName == "kml")
        {
            foreach (var line in root.Descendants().Where(x => x.Name.LocalName == "LineString"))
            {
                var coordinates = line.Elements().FirstOrDefault(x => x.Name.LocalName == "coordinates")?.Value;
                if (string.IsNullOrWhiteSpace(coordinates)) { ignored++; continue; }
                var placemark = line.Ancestors().FirstOrDefault(x => x.Name.LocalName == "Placemark");
                Add(coordinates.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(value =>
                {
                    var fields = value.Split(',');
                    if (fields.Length < 2) throw new InvalidDataException("gpsInvalidCoordinate");
                    return Point(fields[1], fields[0]);
                }), placemark is null ? null : Name(placemark));
            }
        }
        else throw new InvalidDataException("gpsRootUnsupported");

        return new MapStudioGeoJsonRoadImportResult(traces, ignored);

        void Add(IEnumerable<MapStudioGeoRoadPoint> input, string? name)
        {
            var points = new List<MapStudioGeoRoadPoint>();
            foreach (var point in input)
            {
                if (++count > MaxPoints) throw new InvalidDataException("gpsLimitExceeded");
                if (points.Count == 0 || points[^1] != point) points.Add(point);
            }
            if (points.Count < 2) { ignored++; return; }
            if (traces.Count >= MaxTraces) throw new InvalidDataException("gpsLimitExceeded");
            traces.Add(new MapStudioGeoRoadTrace($"gps-{traces.Count + 1}", points, null, null, null, null, name));
        }
    }

    private static string? Name(XElement item) =>
        item.Elements().FirstOrDefault(x => x.Name.LocalName == "name")?.Value.Trim();

    private static MapStudioGeoRoadPoint Point(string? latitude, string? longitude)
    {
        if (!double.TryParse(latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) ||
            !double.TryParse(longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var lon) ||
            !double.IsFinite(lat) || !double.IsFinite(lon) || lat is < -90 or > 90 || lon is < -180 or > 180)
            throw new InvalidDataException("gpsInvalidCoordinate");
        return new MapStudioGeoRoadPoint(lat, lon);
    }
}
