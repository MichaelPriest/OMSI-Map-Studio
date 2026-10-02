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
    FireHydrant,
    TrafficSignal,
    Crosswalk
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

        var nodeCoordinates =
            BuildNodeCoordinates(
                root);

        var roadWays =
            BuildRoadWays(
                root);

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
                    ResolveReference(
                        tags,
                        kind),
                    Clean(
                        tags.GetValueOrDefault("shelter_type")),
                    ResolveDirection(
                        tags,
                        id,
                        nodeCoordinates,
                        roadWays)));
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
                highway,
                "traffic_signals",
                StringComparison.OrdinalIgnoreCase))
        {
            kind =
                MapStudioOsmStreetFurnitureKind.TrafficSignal;
            return true;
        }

        if (
            string.Equals(
                highway,
                "crossing",
                StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrWhiteSpace(
                Clean(
                    tags.GetValueOrDefault(
                        "crossing"))))
        {
            kind =
                MapStudioOsmStreetFurnitureKind.Crosswalk;
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
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                highway,
                "stop",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                highway,
                "give_way",
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

    private static string? ResolveReference(
        IReadOnlyDictionary<string, string?> tags,
        MapStudioOsmStreetFurnitureKind kind)
    {
        var reference =
            Clean(
                tags.GetValueOrDefault(
                    "ref"));

        if (
            kind !=
                MapStudioOsmStreetFurnitureKind
                    .TrafficSign)
        {
            return reference;
        }

        var trafficSign =
            Clean(
                tags.GetValueOrDefault(
                    "traffic_sign"));

        if (!string.IsNullOrWhiteSpace(
                trafficSign))
        {
            if (
                TryResolveExplicitMaxSpeedReference(
                    tags,
                    trafficSign,
                    out var maxSpeedReference))
            {
                return maxSpeedReference;
            }

            if (
                IsMaxSpeedTrafficSign(
                    trafficSign))
            {
                return null;
            }

            return trafficSign;
        }

        var highway =
            Clean(
                tags.GetValueOrDefault(
                    "highway"));

        if (
            string.Equals(
                highway,
                "stop",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                highway,
                "give_way",
                StringComparison.OrdinalIgnoreCase))
        {
            return highway;
        }

        return reference;
    }

    private static bool
        TryResolveExplicitMaxSpeedReference(
            IReadOnlyDictionary<string, string?> tags,
            string trafficSign,
            out string reference)
    {
        reference =
            string.Empty;

        if (
            !IsMaxSpeedTrafficSign(
                trafficSign))
        {
            return false;
        }

        if (
            !TryExtractBracketedMaxSpeed(
                trafficSign,
                out var speedKph) &&
            !TryParseExplicitMaxSpeedKph(
                tags.GetValueOrDefault(
                    "maxspeed"),
                out speedKph))
        {
            return false;
        }

        reference =
            "maxspeed " +
            speedKph.ToString(
                CultureInfo.InvariantCulture);

        return true;
    }

    private static bool IsMaxSpeedTrafficSign(
        string trafficSign)
    {
        var normalized =
            Clean(
                trafficSign);

        if (
            string.IsNullOrWhiteSpace(
                normalized))
        {
            return false;
        }

        return
            string.Equals(
                normalized,
                "maxspeed",
                StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith(
                "BR:R-19",
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryExtractBracketedMaxSpeed(
        string trafficSign,
        out int speedKph)
    {
        speedKph =
            0;

        var open =
            trafficSign.IndexOf(
                '[');

        if (open < 0)
        {
            return false;
        }

        var close =
            trafficSign.IndexOf(
                ']',
                open + 1);

        if (
            close <=
                open + 1)
        {
            return false;
        }

        return TryParseExplicitMaxSpeedKph(
            trafficSign[
                (open + 1)..close],
            out speedKph);
    }

    private static bool TryParseExplicitMaxSpeedKph(
        string? value,
        out int speedKph)
    {
        speedKph =
            0;

        if (
            string.IsNullOrWhiteSpace(
                value))
        {
            return false;
        }

        var normalized =
            value
                .Trim()
                .ToLowerInvariant();

        foreach (
            var suffix in
                new[]
                {
                    "km/h",
                    "kmh",
                    "kph"
                })
        {
            if (
                normalized.EndsWith(
                    suffix,
                    StringComparison.Ordinal))
            {
                normalized =
                    normalized[
                        ..^suffix.Length]
                        .Trim();

                break;
            }
        }

        if (
            normalized.Any(
                char.IsLetter) ||
            normalized.Contains(
                ";",
                StringComparison.Ordinal) ||
            normalized.Contains(
                "|",
                StringComparison.Ordinal))
        {
            return false;
        }

        if (
            !double.TryParse(
                normalized,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed) ||
            !double.IsFinite(
                parsed) ||
            parsed <
                5 ||
            parsed >
                200)
        {
            return false;
        }

        var rounded =
            (int)Math.Round(
                parsed,
                MidpointRounding.AwayFromZero);

        if (
            Math.Abs(
                parsed -
                rounded) >
            0.01)
        {
            return false;
        }

        speedKph =
            rounded;

        return true;
    }

    private static IReadOnlyDictionary<
        long,
        GeoCoordinate>
        BuildNodeCoordinates(
            XElement root)
    {
        var result =
            new Dictionary<
                long,
                GeoCoordinate>();

        foreach (
            var node in
                root.Elements()
                    .Where(
                        item =>
                            item.Name.LocalName ==
                            "node"))
        {
            if (
                long.TryParse(
                    node.Attribute("id")?.Value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var id) &&
                TryDouble(
                    node.Attribute("lat")?.Value,
                    out var latitude) &&
                TryDouble(
                    node.Attribute("lon")?.Value,
                    out var longitude) &&
                latitude is >= -90 and <= 90 &&
                longitude is >= -180 and <= 180)
            {
                result[id] =
                    new GeoCoordinate(
                        latitude,
                        longitude);
            }
        }

        return result;
    }

    private static IReadOnlyList<RoadWay>
        BuildRoadWays(
            XElement root)
    {
        var result =
            new List<RoadWay>();

        foreach (
            var way in
                root.Elements()
                    .Where(
                        item =>
                            item.Name.LocalName ==
                            "way"))
        {
            var tags =
                ReadTags(
                    way);

            if (
                string.IsNullOrWhiteSpace(
                    Clean(
                        tags.GetValueOrDefault(
                            "highway"))))
            {
                continue;
            }

            var nodeIds =
                way.Elements()
                    .Where(
                        item =>
                            item.Name.LocalName ==
                            "nd")
                    .Select(
                        item =>
                            item.Attribute(
                                "ref")
                                ?.Value)
                    .Where(
                        value =>
                            long.TryParse(
                                value,
                                NumberStyles.Integer,
                                CultureInfo.InvariantCulture,
                                out _))
                    .Select(
                        value =>
                            long.Parse(
                                value!,
                                NumberStyles.Integer,
                                CultureInfo.InvariantCulture))
                    .ToArray();

            if (nodeIds.Length >= 2)
            {
                result.Add(
                    new RoadWay(
                        nodeIds));
            }
        }

        return result;
    }

    private static double? ResolveDirection(
        IReadOnlyDictionary<string, string?> tags,
        string? nodeIdValue,
        IReadOnlyDictionary<long, GeoCoordinate>
            nodeCoordinates,
        IReadOnlyList<RoadWay> roadWays)
    {
        var highway =
            Clean(
                tags.GetValueOrDefault(
                    "highway"))
                ?.ToLowerInvariant();

        var directionValue =
            highway switch
            {
                "stop" =>
                    Clean(
                        tags.GetValueOrDefault(
                            "stop:direction")) ??
                    Clean(
                        tags.GetValueOrDefault(
                            "direction")),
                "give_way" =>
                    Clean(
                        tags.GetValueOrDefault(
                            "give_way:direction")) ??
                    Clean(
                        tags.GetValueOrDefault(
                            "direction")),
                _ =>
                    Clean(
                        tags.GetValueOrDefault(
                            "direction"))
            };

        var absolute =
            ParseDirection(
                directionValue);

        if (absolute is not null)
        {
            return absolute;
        }

        var normalized =
            directionValue
                ?.Trim()
                .ToLowerInvariant();

        if (
            normalized is not
                ("forward" or "backward") ||
            !long.TryParse(
                nodeIdValue,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var nodeId))
        {
            return null;
        }

        var candidates =
            roadWays
                .Select(
                    way =>
                        ResolveWayForwardBearing(
                            way,
                            nodeId,
                            nodeCoordinates))
                .Where(
                    bearing =>
                        bearing is not null)
                .Select(
                    bearing =>
                        normalized ==
                            "backward"
                            ? NormalizeDegrees(
                                bearing!.Value +
                                180.0)
                            : bearing!.Value)
                .ToArray();

        if (candidates.Length == 0)
        {
            return null;
        }

        var mean =
            CircularMeanDegrees(
                candidates);

        const double
            MaximumAmbiguousDirectionSpreadDegrees =
                25.0;

        if (
            candidates.Any(
                bearing =>
                    CircularAngularDistanceDegrees(
                        bearing,
                        mean) >
                    MaximumAmbiguousDirectionSpreadDegrees))
        {
            return null;
        }

        return mean;
    }

    private static double? ResolveWayForwardBearing(
        RoadWay way,
        long nodeId,
        IReadOnlyDictionary<long, GeoCoordinate>
            nodeCoordinates)
    {
        for (
            var index = 0;
            index < way.NodeIds.Count;
            index++)
        {
            if (
                way.NodeIds[index] !=
                    nodeId)
            {
                continue;
            }

            long fromId;
            long toId;

            if (
                index + 1 <
                    way.NodeIds.Count)
            {
                fromId =
                    nodeId;
                toId =
                    way.NodeIds[
                        index +
                        1];
            }
            else if (index > 0)
            {
                fromId =
                    way.NodeIds[
                        index -
                        1];
                toId =
                    nodeId;
            }
            else
            {
                return null;
            }

            if (
                !nodeCoordinates.TryGetValue(
                    fromId,
                    out var from) ||
                !nodeCoordinates.TryGetValue(
                    toId,
                    out var to))
            {
                return null;
            }

            return ResolveBearingDegrees(
                from,
                to);
        }

        return null;
    }

    private static double ResolveBearingDegrees(
        GeoCoordinate from,
        GeoCoordinate to)
    {
        var latitude1 =
            from.Latitude *
            Math.PI /
            180.0;

        var latitude2 =
            to.Latitude *
            Math.PI /
            180.0;

        var longitudeDelta =
            (
                to.Longitude -
                from.Longitude
            ) *
            Math.PI /
            180.0;

        var y =
            Math.Sin(
                longitudeDelta) *
            Math.Cos(
                latitude2);

        var x =
            Math.Cos(
                latitude1) *
            Math.Sin(
                latitude2) -
            Math.Sin(
                latitude1) *
            Math.Cos(
                latitude2) *
            Math.Cos(
                longitudeDelta);

        return NormalizeDegrees(
            Math.Atan2(
                y,
                x) *
            180.0 /
            Math.PI);
    }

    private static double CircularMeanDegrees(
        IReadOnlyList<double> values)
    {
        var x =
            values.Sum(
                value =>
                    Math.Cos(
                        value *
                        Math.PI /
                        180.0));

        var y =
            values.Sum(
                value =>
                    Math.Sin(
                        value *
                        Math.PI /
                        180.0));

        return NormalizeDegrees(
            Math.Atan2(
                y,
                x) *
            180.0 /
            Math.PI);
    }

    private static double
        CircularAngularDistanceDegrees(
            double first,
            double second)
    {
        var delta =
            Math.Abs(
                NormalizeDegrees(
                    first) -
                NormalizeDegrees(
                    second));

        return Math.Min(
            delta,
            360.0 -
                delta);
    }

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

    private readonly record struct GeoCoordinate(
        double Latitude,
        double Longitude);

    private sealed record RoadWay(
        IReadOnlyList<long> NodeIds);
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
            MapStudioOsmStreetFurnitureKind.TrafficSignal =>
                MapStudioSceneFeatureKind.TrafficSignal,
            MapStudioOsmStreetFurnitureKind.Crosswalk =>
                MapStudioSceneFeatureKind.Crosswalk,
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
                    string.IsNullOrWhiteSpace(
                        point.Reference) ||
                    point.DirectionDegrees is null
                        ? 0.78
                        : 0.94,
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
                MapStudioOsmStreetFurnitureKind.TrafficSignal =>
                    0.94,
                MapStudioOsmStreetFurnitureKind.Crosswalk =>
                    point.DirectionDegrees is null
                        ? 0.72
                        : 0.84,
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
