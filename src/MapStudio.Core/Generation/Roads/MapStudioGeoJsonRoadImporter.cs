using System.Globalization;
using System.Text.Json;

namespace MapStudio.Core.Generation.Roads;

public sealed record MapStudioGeoRoadPoint(
    double Latitude,
    double Longitude);

public sealed record MapStudioGeoRoadTrace(
    string Id,
    IReadOnlyList<MapStudioGeoRoadPoint> Points,
    string? Highway,
    int? LaneCount,
    bool? OneWay,
    double? WidthMeters,
    string? Name,
    int? ForwardLaneCount = null,
    int? BackwardLaneCount = null,
    int? Layer = null,
    bool Bridge = false,
    bool Tunnel = false);

public sealed record MapStudioGeoJsonRoadImportResult(
    IReadOnlyList<MapStudioGeoRoadTrace> Traces,
    int IgnoredFeatureCount);

public sealed class MapStudioGeoJsonRoadImporter
{
    public MapStudioGeoJsonRoadImportResult Parse(
        string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            json);

        using var document =
            JsonDocument.Parse(
                json);

        return Parse(
            document.RootElement);
    }

    public MapStudioGeoJsonRoadImportResult Parse(
        JsonElement root)
    {
        var traces =
            new List<MapStudioGeoRoadTrace>();

        var ignored =
            0;

        if (
            root.ValueKind ==
                JsonValueKind.Object &&
            root.TryGetProperty(
                "type",
                out var type) &&
            type.ValueKind ==
                JsonValueKind.String)
        {
            var rootType =
                type.GetString();

            if (
                string.Equals(
                    rootType,
                    "FeatureCollection",
                    StringComparison.OrdinalIgnoreCase) &&
                root.TryGetProperty(
                    "features",
                    out var features) &&
                features.ValueKind ==
                    JsonValueKind.Array)
            {
                var index =
                    0;

                foreach (
                    var feature in
                        features.EnumerateArray())
                {
                    if (
                        !TryReadFeature(
                            feature,
                            index++,
                            traces))
                    {
                        ignored++;
                    }
                }

                return new MapStudioGeoJsonRoadImportResult(
                    traces,
                    ignored);
            }

            if (
                string.Equals(
                    rootType,
                    "Feature",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (
                    !TryReadFeature(
                        root,
                        0,
                        traces))
                {
                    ignored++;
                }

                return new MapStudioGeoJsonRoadImportResult(
                    traces,
                    ignored);
            }

            var geometryType =
                rootType;

            if (
                string.Equals(
                    geometryType,
                    "LineString",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (
                    TryReadLineString(
                        root,
                        "geojson-0",
                        null,
                        null,
                        null,
                        null,
                        null,
                        traces))
                {
                    return new MapStudioGeoJsonRoadImportResult(
                        traces,
                        0);
                }
            }

            if (
                string.Equals(
                    geometryType,
                    "MultiLineString",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (
                    TryReadMultiLineString(
                        root,
                        "geojson-0",
                        null,
                        null,
                        null,
                        null,
                        null,
                        traces))
                {
                    return new MapStudioGeoJsonRoadImportResult(
                        traces,
                        0);
                }
            }
        }

        throw new InvalidDataException(
            "geoJsonRoadGeometryUnsupported");
    }

    private static bool TryReadFeature(
        JsonElement feature,
        int featureIndex,
        ICollection<MapStudioGeoRoadTrace>
            output)
    {
        if (
            feature.ValueKind !=
                JsonValueKind.Object ||
            !feature.TryGetProperty(
                "geometry",
                out var geometry) ||
            geometry.ValueKind !=
                JsonValueKind.Object ||
            !geometry.TryGetProperty(
                "type",
                out var type) ||
            type.ValueKind !=
                JsonValueKind.String)
        {
            return false;
        }

        feature.TryGetProperty(
            "properties",
            out var properties);

        var highway =
            GetStringProperty(
                properties,
                "highway");

        var name =
            GetStringProperty(
                properties,
                "name");

        var laneCount =
            GetIntProperty(
                properties,
                "lanes");

        var oneWay =
            GetBoolProperty(
                properties,
                "oneway");

        var width =
            GetDoubleProperty(
                properties,
                "width");

        var featureId =
            GetFeatureId(
                feature,
                featureIndex);

        var geometryType =
            type.GetString();

        if (
            string.Equals(
                geometryType,
                "LineString",
                StringComparison.OrdinalIgnoreCase))
        {
            return TryReadLineString(
                geometry,
                featureId,
                highway,
                laneCount,
                oneWay,
                width,
                name,
                output);
        }

        if (
            string.Equals(
                geometryType,
                "MultiLineString",
                StringComparison.OrdinalIgnoreCase))
        {
            return TryReadMultiLineString(
                geometry,
                featureId,
                highway,
                laneCount,
                oneWay,
                width,
                name,
                output);
        }

        return false;
    }

    private static bool TryReadLineString(
        JsonElement geometry,
        string id,
        string? highway,
        int? laneCount,
        bool? oneWay,
        double? width,
        string? name,
        ICollection<MapStudioGeoRoadTrace>
            output)
    {
        if (
            !geometry.TryGetProperty(
                "coordinates",
                out var coordinates) ||
            coordinates.ValueKind !=
                JsonValueKind.Array ||
            !TryReadPoints(
                coordinates,
                out var points))
        {
            return false;
        }

        output.Add(
            new MapStudioGeoRoadTrace(
                id,
                points,
                highway,
                laneCount,
                oneWay,
                width,
                name));

        return true;
    }

    private static bool TryReadMultiLineString(
        JsonElement geometry,
        string id,
        string? highway,
        int? laneCount,
        bool? oneWay,
        double? width,
        string? name,
        ICollection<MapStudioGeoRoadTrace>
            output)
    {
        if (
            !geometry.TryGetProperty(
                "coordinates",
                out var coordinates) ||
            coordinates.ValueKind !=
                JsonValueKind.Array)
        {
            return false;
        }

        var part =
            0;

        var any =
            false;

        foreach (
            var line in
                coordinates.EnumerateArray())
        {
            if (
                !TryReadPoints(
                    line,
                    out var points))
            {
                continue;
            }

            output.Add(
                new MapStudioGeoRoadTrace(
                    id +
                    "-part-" +
                    part++,
                    points,
                    highway,
                    laneCount,
                    oneWay,
                    width,
                    name));

            any =
                true;
        }

        return any;
    }

    private static bool TryReadPoints(
        JsonElement coordinates,
        out IReadOnlyList<
            MapStudioGeoRoadPoint>
            points)
    {
        var result =
            new List<
                MapStudioGeoRoadPoint>();

        foreach (
            var coordinate in
                coordinates.EnumerateArray())
        {
            if (
                coordinate.ValueKind !=
                    JsonValueKind.Array)
            {
                continue;
            }

            using var enumerator =
                coordinate
                    .EnumerateArray();

            if (
                !enumerator.MoveNext() ||
                !TryGetFiniteDouble(
                    enumerator.Current,
                    out var longitude) ||
                !enumerator.MoveNext() ||
                !TryGetFiniteDouble(
                    enumerator.Current,
                    out var latitude) ||
                latitude is
                    < -90 or > 90 ||
                longitude is
                    < -180 or > 180)
            {
                continue;
            }

            var point =
                new MapStudioGeoRoadPoint(
                    latitude,
                    longitude);

            if (
                result.Count ==
                    0 ||
                Math.Abs(
                    result[^1]
                        .Latitude -
                    point.Latitude) >
                    1e-12 ||
                Math.Abs(
                    result[^1]
                        .Longitude -
                    point.Longitude) >
                    1e-12)
            {
                result.Add(
                    point);
            }
        }

        points =
            result;

        return result.Count >=
            2;
    }

    private static string GetFeatureId(
        JsonElement feature,
        int fallbackIndex)
    {
        if (
            feature.TryGetProperty(
                "id",
                out var id))
        {
            if (
                id.ValueKind ==
                    JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(
                    id.GetString()))
            {
                return id.GetString()!;
            }

            if (
                id.ValueKind ==
                    JsonValueKind.Number)
            {
                return id
                    .GetRawText();
            }
        }

        return "feature-" +
            fallbackIndex;
    }

    private static string?
        GetStringProperty(
            JsonElement properties,
            string name)
    {
        if (
            properties.ValueKind !=
                JsonValueKind.Object ||
            !properties.TryGetProperty(
                name,
                out var value))
        {
            return null;
        }

        return value.ValueKind ==
            JsonValueKind.String
                ? value.GetString()
                : null;
    }

    private static int?
        GetIntProperty(
            JsonElement properties,
            string name)
    {
        if (
            properties.ValueKind !=
                JsonValueKind.Object ||
            !properties.TryGetProperty(
                name,
                out var value))
        {
            return null;
        }

        if (
            value.ValueKind ==
                JsonValueKind.Number &&
            value.TryGetInt32(
                out var numeric) &&
            numeric is
                > 0 and <= 32)
        {
            return numeric;
        }

        if (
            value.ValueKind ==
                JsonValueKind.String &&
            int.TryParse(
                value.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out numeric) &&
            numeric is
                > 0 and <= 32)
        {
            return numeric;
        }

        return null;
    }

    private static double?
        GetDoubleProperty(
            JsonElement properties,
            string name)
    {
        if (
            properties.ValueKind !=
                JsonValueKind.Object ||
            !properties.TryGetProperty(
                name,
                out var value))
        {
            return null;
        }

        if (
            value.ValueKind ==
                JsonValueKind.Number &&
            value.TryGetDouble(
                out var numeric) &&
            double.IsFinite(
                numeric) &&
            numeric >
                0)
        {
            return numeric;
        }

        if (
            value.ValueKind ==
                JsonValueKind.String &&
            double.TryParse(
                value.GetString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out numeric) &&
            double.IsFinite(
                numeric) &&
            numeric >
                0)
        {
            return numeric;
        }

        return null;
    }

    private static bool?
        GetBoolProperty(
            JsonElement properties,
            string name)
    {
        if (
            properties.ValueKind !=
                JsonValueKind.Object ||
            !properties.TryGetProperty(
                name,
                out var value))
        {
            return null;
        }

        if (
            value.ValueKind ==
                JsonValueKind.True)
        {
            return true;
        }

        if (
            value.ValueKind ==
                JsonValueKind.False)
        {
            return false;
        }

        if (
            value.ValueKind ==
                JsonValueKind.String)
        {
            var text =
                value
                    .GetString()
                    ?.Trim();

            if (
                string.Equals(
                    text,
                    "yes",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    text,
                    "true",
                    StringComparison.OrdinalIgnoreCase) ||
                text ==
                    "1")
            {
                return true;
            }

            if (
                string.Equals(
                    text,
                    "no",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    text,
                    "false",
                    StringComparison.OrdinalIgnoreCase) ||
                text ==
                    "0")
            {
                return false;
            }
        }

        return null;
    }

    private static bool TryGetFiniteDouble(
        JsonElement value,
        out double number)
    {
        number =
            0;

        return
            value.ValueKind ==
                JsonValueKind.Number &&
            value.TryGetDouble(
                out number) &&
            double.IsFinite(
                number);
    }
}
