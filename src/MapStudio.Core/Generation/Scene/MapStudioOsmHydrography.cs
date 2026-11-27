using System.Globalization;
using System.Xml.Linq;
using MapStudio.Core.Generation.Roads;

namespace MapStudio.Core.Generation.Scene;

internal sealed record MapStudioWaterway(IReadOnlyList<MapStudioRoadPoint> Points, double WidthMeters);

internal sealed class MapStudioOsmHydrography
{
    internal List<IReadOnlyList<IReadOnlyList<MapStudioRoadPoint>>> Areas { get; } = [];
    internal List<MapStudioWaterway> Waterways { get; } = [];

    internal static MapStudioOsmHydrography Read(XElement root, MapStudioGeographicAnchor anchor)
    {
        var result = new MapStudioOsmHydrography();
        var nodes = new Dictionary<long, MapStudioRoadPoint>();
        foreach (var node in root.Elements("node"))
        {
            if (TryId(node, "id", out var id) &&
                TryNumber(node.Attribute("lat")?.Value, out var lat) && lat is >= -90 and <= 90 &&
                TryNumber(node.Attribute("lon")?.Value, out var lon) && lon is >= -180 and <= 180)
            {
                nodes[id] = MapStudioGeographicProjection.Project(anchor, new MapStudioGeoRoadPoint(lat, lon));
            }
        }

        var ways = new Dictionary<long, long[]>();
        foreach (var way in root.Elements("way"))
        {
            if (!TryId(way, "id", out var id)) continue;
            var refs = new List<long>();
            foreach (var nd in way.Elements("nd"))
            {
                if (!TryId(nd, "ref", out var reference) || !nodes.ContainsKey(reference))
                {
                    refs.Clear();
                    break;
                }
                refs.Add(reference);
            }
            if (refs.Count >= 2) ways[id] = refs.ToArray();
        }

        var areaMembers = new HashSet<long>();
        foreach (var relation in root.Elements("relation"))
        {
            if (Tag(relation, "type") != "multipolygon" || !IsWaterArea(relation)) continue;
            var outer = new List<long>();
            var inner = new List<long>();
            var complete = true;
            foreach (var member in relation.Elements("member"))
            {
                var role = (string?)member.Attribute("role");
                if (role is not (null or "" or "outer" or "inner")) continue;
                if ((string?)member.Attribute("type") != "way" || !TryId(member, "ref", out var id))
                {
                    complete = false;
                    continue;
                }
                areaMembers.Add(id);
                if (!ways.ContainsKey(id)) complete = false;
                (role == "inner" ? inner : outer).Add(id);
            }
            if (!complete || !MapStudioOsmRingAssembler.TryAssemble(outer, ways, out var outlines)) continue;
            var rings = outlines.ToList();
            if (inner.Count != 0)
            {
                if (!MapStudioOsmRingAssembler.TryAssemble(inner, ways, out var holes)) continue;
                rings.AddRange(holes);
            }
            result.Areas.Add(rings.Select(ring =>
                (IReadOnlyList<MapStudioRoadPoint>)ring.Select(id => nodes[id]).ToArray()).ToArray());
        }

        foreach (var way in root.Elements("way"))
        {
            if (!TryId(way, "id", out var id) || !ways.TryGetValue(id, out var refs)) continue;
            if (IsWaterArea(way))
            {
                if (!areaMembers.Contains(id) && refs.Length >= 4 && refs[0] == refs[^1])
                    result.Areas.Add([refs[..^1].Select(reference => nodes[reference]).ToArray()]);
                continue;
            }

            var kind = Tag(way, "waterway");
            if (kind is not ("river" or "stream" or "canal") ||
                Tag(way, "tunnel") is not (null or "no") || Tag(way, "covered") == "yes") continue;
            var widthText = Tag(way, "width");
            if (widthText?.EndsWith(" m", StringComparison.Ordinal) == true)
                widthText = widthText[..^2];
            var width = TryNumber(widthText, out var meters) && meters > 0
                ? meters : kind switch { "river" => 12.0, "canal" => 6.0, _ => 2.0 };
            result.Waterways.Add(new MapStudioWaterway(refs.Select(reference => nodes[reference]).ToArray(), width));
        }
        return result;
    }

    private static bool IsWaterArea(XElement element) =>
        Tag(element, "natural") == "water" || Tag(element, "waterway") == "riverbank" ||
        Tag(element, "landuse") == "reservoir";

    private static string? Tag(XElement element, string key) =>
        (string?)element.Elements("tag").FirstOrDefault(tag => (string?)tag.Attribute("k") == key)?.Attribute("v");

    private static bool TryId(XElement element, string attribute, out long value) =>
        long.TryParse((string?)element.Attribute(attribute), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    private static bool TryNumber(string? text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
}
