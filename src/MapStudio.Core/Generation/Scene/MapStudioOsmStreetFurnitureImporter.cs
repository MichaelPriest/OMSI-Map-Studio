using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace MapStudio.Core.Generation.Scene;

public enum MapStudioOsmStreetFurnitureKind
{
    UtilityPole,
    StreetLight,
    TrafficSign,
    BusShelter,
    Bench,
    WasteBasket,
    Bollard,
    FireHydrant
}

public sealed record MapStudioGeoStreetFurniturePoint(
    string Id,
    double Latitude,
    double Longitude,
    MapStudioOsmStreetFurnitureKind Kind,
    string? Name,
    string? Operator,
    string? Reference,
    string? ShelterType,
    double? DirectionDegrees = null);

public sealed record MapStudioOsmStreetFurnitureImportResult(
    IReadOnlyList<MapStudioGeoStreetFurniturePoint> Points,
    int IgnoredNodeCount);

public sealed class MapStudioOsmStreetFurnitureImporter
{
    public MapStudioOsmStreetFurnitureImportResult Parse(
        string xml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);

        using var textReader =
            new StringReader(xml);

        using var reader =
            XmlReader.Create(
                textReader,
                new XmlReaderSettings
                {
                    DtdProcessing =
                        DtdProcessing.Prohibit,
                    XmlResolver =
                        null,
                    MaxCharactersInDocument =
                        128L *
                        1024 *
                        1024
                });

        return Parse(
            XDocument.Load(
                reader,
                LoadOptions.None));
    }

    public MapStudioOsmStreetFurnitureImportResult Parse(
        XDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var root =
            document.Root;

        if (
            root is null ||
            !string.Equals(
                root.Name.LocalName,
                "osm",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "osmRootInvalid");
        }

        var points =
            new List<MapStudioGeoStreetFurniturePoint>();

        var ignored =
            0;

        foreach (
            var node in
                root.Elements()
                    .Where(
                        item =>
                            item.Name.LocalName ==
                            "node"))
        {
            var tags =
                ReadTags(node);

            if (!TryGetKind(tags, out var kind))
            {
                ignored++;
                continue;
            }

            if (
                !TryDouble(
                    node.Attribute("lat")?.Value,
                    out var latitude) ||
                !TryDouble(
                    node.Attribute("lon")?.Value,
                    out var longitude) ||
                latitude is < -90 or > 90 ||
                longitude is < -180 or > 180)
            {
                ignored++;
                continue;
            }

            var id =
                node.Attribute("id")?.Value;

            points.Add(
                new MapStudioGeoStreetFurniturePoint(
                    string.IsNullOrWhiteSpace(id)
                        ? "osm-street-furniture-" +
                          points.Count
                        : "osm-street-furniture-" +
                          id,
                    latitude,
                    longitude,
                    kind,
                    Clean(
                        tags.GetValueOrDefault("name")),
                    Clean(
                        tags.GetValueOrDefault("operator")),
                    Clean(
                        tags.GetValueOrDefault("ref")),
                    Clean(
                        tags.GetValueOrDefault("shelter_type")),
                    ParseDirection(
                        tags.GetValueOrDefault("direction"))));
        }

        return new MapStudioOsmStreetFurnitureImportResult(
            points,
            ignored);
    }

    private static bool TryGetKind(
        IReadOnlyDictionary<string, string?> tags,
        out MapStudioOsmStreetFurnitureKind kind)
    {
        var highway =
            Clean(
                tags.GetValueOrDefault("highway"));

        var power =
            Clean(
                tags.GetValueOrDefault("power"));

        var trafficSign =
            Clean(
                tags.GetValueOrDefault("traffic_sign"));

        var amenity =
            Clean(
                tags.GetValueOrDefault("amenity"));

        var barrier =
            Clean(
                tags.GetValueOrDefault("barrier"));

        var emergency =
            Clean(
                tags.GetValueOrDefault("emergency"));

        var shelter =
            Clean(
                tags.GetValueOrDefault("shelter"));

        var publicTransport =
            Clean(
                tags.GetValueOrDefault("public_transport"));

        if (
            string.Equals(
                highway,
                "street_lamp",
                StringComparison.OrdinalIgnoreCase))
        {
            kind =
                MapStudioOsmStreetFurnitureKind.StreetLight;
            return true;
        }

        if (
            string.Equals(
                power,
                "pole",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                power,
                "tower",
                StringComparison.OrdinalIgnoreCase))
        {
            kind =
                MapStudioOsmStreetFurnitureKind.UtilityPole;
            return true;
        }

        if (
            !string.IsNullOrWhiteSpace(trafficSign) ||
            string.Equals(
                highway,
                "traffic_sign",
                StringComparison.OrdinalIgnoreCase))
        {
            kind =
                MapStudioOsmStreetFurnitureKind.TrafficSign;
            return true;
        }

        if (
            string.Equals(
                amenity,
                "shelter",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                shelter,
                "yes",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                highway,
                "bus_stop",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                publicTransport,
                "platform",
                StringComparison.OrdinalIgnoreCase))
        {
            kind =
                MapStudioOsmStreetFurnitureKind.BusShelter;
            return true;
        }

        if (
            string.Equals(
                amenity,
                "bench",
                StringComparison.OrdinalIgnoreCase))
        {
            kind =
                MapStudioOsmStreetFurnitureKind.Bench;
            return true;
        }

        if (
            string.Equals(
                amenity,
                "waste_basket",
                StringComparison.OrdinalIgnoreCase))
        {
            kind =
                MapStudioOsmStreetFurnitureKind.WasteBasket;
            return true;
        }

        if (
            string.Equals(
                barrier,
                "bollard",
                StringComparison.OrdinalIgnoreCase))
        {
            kind =
                MapStudioOsmStreetFurnitureKind.Bollard;
            return true;
        }

        if (
            string.Equals(
                emergency,
                "fire_hydrant",
                StringComparison.OrdinalIgnoreCase))
        {
            kind =
                MapStudioOsmStreetFurnitureKind.FireHydrant;
            return true;
        }

        kind =
            default;

        return false;
    }

    private static Dictionary<string, string?> ReadTags(
        XElement element) =>
        element.Elements()
            .Where(
                item =>
                    item.Name.LocalName ==
                    "tag")
            .Select(
                item =>
                    (
                        Key:
                            item.Attribute("k")?.Value,
                        Value:
                            item.Attribute("v")?.Value
                    ))
            .Where(
                item =>
                    !string.IsNullOrWhiteSpace(
                        item.Key))
            .GroupBy(
                item =>
                    item.Key!,
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group =>
                    group.Key,
                group =>
                    group.Last().Value,
                StringComparer.OrdinalIgnoreCase);

    private static double? ParseDirection(
        string? value)
    {
        var normalized =
            Clean(
                value)
                ?.ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(
                normalized))
        {
            return null;
        }

        if (
            double.TryParse(
                normalized,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var numeric) &&
            double.IsFinite(
                numeric))
        {
            return NormalizeDegrees(
                numeric);
        }

        var bearing =
            normalized switch
            {
                "n" or "north" =>
                    0.0,
                "nne" =>
                    22.5,
                "ne" or "northeast" =>
                    45.0,
                "ene" =>
                    67.5,
                "e" or "east" =>
                    90.0,
                "ese" =>
                    112.5,
                "se" or "southeast" =>
                    135.0,
                "sse" =>
                    157.5,
                "s" or "south" =>
                    180.0,
                "ssw" =>
                    202.5,
                "sw" or "southwest" =>
                    225.0,
                "wsw" =>
                    247.5,
                "w" or "west" =>
                    270.0,
                "wnw" =>
                    292.5,
                "nw" or "northwest" =>
                    315.0,
                "nnw" =>
                    337.5,
                _ =>
                    double.NaN
            };

        return double.IsFinite(
                bearing)
            ? bearing
            : null;
    }

    private static double NormalizeDegrees(
        double degrees)
    {
        var normalized =
            degrees %
            360.0;

        return normalized <
            0
                ? normalized +
                    360.0
                : normalized;
    }

    private static string? Clean(
        string? value)
    {
        var normalized =
            value?.Trim();

        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : normalized;
    }

    private static bool TryDouble(
        string? value,
        out double result) =>
        double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out result) &&
        double.IsFinite(result);
}

public sealed class MapStudioOsmStreetFurnitureReconstructionAdapter
{
    public IReadOnlyList<MapStudioSceneFeatureCandidate>
        BuildCandidates(
            IReadOnlyList<MapStudioGeoStreetFurniturePoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        return points
            .Select(
                point =>
                    new MapStudioSceneFeatureCandidate(
                        point.Id,
                        MapKind(point.Kind),
                        [
                            new MapStudioSceneEvidence(
                                MapStudioSceneEvidenceSource.Osm,
                                ResolveConfidence(point),
                                point.Id,
                                BuildNotes(point))
                        ]))
            .ToArray();
    }

    private static MapStudioSceneFeatureKind MapKind(
        MapStudioOsmStreetFurnitureKind kind) =>
        kind switch
        {
            MapStudioOsmStreetFurnitureKind.UtilityPole =>
                MapStudioSceneFeatureKind.UtilityPole,
            MapStudioOsmStreetFurnitureKind.StreetLight =>
                MapStudioSceneFeatureKind.StreetLight,
            MapStudioOsmStreetFurnitureKind.TrafficSign =>
                MapStudioSceneFeatureKind.TrafficSign,
            MapStudioOsmStreetFurnitureKind.BusShelter =>
                MapStudioSceneFeatureKind.BusShelter,
            MapStudioOsmStreetFurnitureKind.Bench =>
                MapStudioSceneFeatureKind.Bench,
            MapStudioOsmStreetFurnitureKind.WasteBasket =>
                MapStudioSceneFeatureKind.WasteBasket,
            MapStudioOsmStreetFurnitureKind.Bollard =>
                MapStudioSceneFeatureKind.Bollard,
            MapStudioOsmStreetFurnitureKind.FireHydrant =>
                MapStudioSceneFeatureKind.FireHydrant,
            _ =>
                MapStudioSceneFeatureKind.Unknown
        };

    private static double ResolveConfidence(
        MapStudioGeoStreetFurniturePoint point)
    {
        var confidence =
            point.Kind switch
            {
                MapStudioOsmStreetFurnitureKind.StreetLight =>
                    0.88,
                MapStudioOsmStreetFurnitureKind.UtilityPole =>
                    0.86,
                MapStudioOsmStreetFurnitureKind.TrafficSign =>
                    0.84,
                MapStudioOsmStreetFurnitureKind.BusShelter =>
                    0.82,
                MapStudioOsmStreetFurnitureKind.Bench =>
                    0.86,
                MapStudioOsmStreetFurnitureKind.WasteBasket =>
                    0.84,
                MapStudioOsmStreetFurnitureKind.Bollard =>
                    0.88,
                MapStudioOsmStreetFurnitureKind.FireHydrant =>
                    0.90,
                _ =>
                    0.60
            };

        if (!string.IsNullOrWhiteSpace(point.Name))
        {
            confidence += 0.03;
        }

        if (!string.IsNullOrWhiteSpace(point.Operator))
        {
            confidence += 0.02;
        }

        if (!string.IsNullOrWhiteSpace(point.Reference))
        {
            confidence += 0.02;
        }

        if (!string.IsNullOrWhiteSpace(point.ShelterType))
        {
            confidence += 0.03;
        }

        return Math.Clamp(
            confidence,
            0.0,
            0.97);
    }

    private static string BuildNotes(
        MapStudioGeoStreetFurniturePoint point)
    {
        var parts =
            new List<string>
            {
                point.Kind
                    .ToString()
                    .ToLowerInvariant()
            };

        if (!string.IsNullOrWhiteSpace(point.Operator))
        {
            parts.Add("operator");
        }

        if (!string.IsNullOrWhiteSpace(point.Reference))
        {
            parts.Add("ref");
        }

        if (!string.IsNullOrWhiteSpace(point.ShelterType))
        {
            parts.Add("shelter_type");
        }

        return string.Join(",", parts);
    }
}
