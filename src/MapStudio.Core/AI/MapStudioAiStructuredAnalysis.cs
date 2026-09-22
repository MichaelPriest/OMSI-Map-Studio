using System.Text.Json;

namespace MapStudio.Core.AI;

internal static class MapStudioAiStructuredAnalysis
{
    public static string BuildBuildingPrompt(
        MapStudioBuildingReferenceRequest request)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        var prompt =
            "Analyze the building image. Return ONLY one JSON object. " +
            "Use meters. Fields: widthMeters, heightMeters, depthMeters, " +
            "floorCount, roofType, roofHeightMeters, windowsPerFloor, " +
            "doorCount, typicalWindowWidthMeters, typicalWindowHeightMeters, " +
            "facadeMaterial, roofMaterial, architecturalStyle, notes, confidence. " +
            "roofType: unknown|flat|gable|hip|shed|mansard|dome|custom. " +
            "confidence must be 0..1.";

        if (
            !string.IsNullOrWhiteSpace(
                request.UserNotes))
        {
            prompt +=
                " User notes: " +
                request.UserNotes.Trim();
        }

        return prompt;
    }

    public static string BuildRoadPrompt(
        MapStudioRoadReferenceRequest request)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        var prompt =
            "Analyze the map/reference image and trace visible road centerlines. " +
            "Return ONLY one JSON object. Coordinates must be normalized image coordinates: " +
            "x=0 left, x=1 right, y=0 top, y=1 bottom. " +
            "Schema: roads array; each road has kind, laneCount, oneWay, widthMeters, " +
            "and points array with x,y. Include at least two points per road. " +
            "Also return notes and confidence from 0 to 1.";

        if (
            !string.IsNullOrWhiteSpace(
                request.UserNotes))
        {
            prompt +=
                " User notes: " +
                request.UserNotes.Trim();
        }

        return prompt;
    }

    public static MapStudioBuildingReferenceAnalysis
        ParseBuilding(
            string json)
    {
        using var document =
            JsonDocument.Parse(
                ExtractJsonObject(
                    json,
                    "aiBuildingJsonMissing"));

        var root =
            document.RootElement;

        return new MapStudioBuildingReferenceAnalysis(
            GetDouble(
                root,
                "widthMeters"),
            GetDouble(
                root,
                "heightMeters"),
            GetDouble(
                root,
                "depthMeters"),
            GetInt(
                root,
                "floorCount"),
            ParseRoofType(
                GetString(
                    root,
                    "roofType")),
            GetDouble(
                root,
                "roofHeightMeters"),
            BuildOpenings(
                root),
            GetString(
                root,
                "facadeMaterial"),
            GetString(
                root,
                "roofMaterial"),
            GetString(
                root,
                "architecturalStyle"),
            GetString(
                root,
                "notes"),
            GetDouble(
                root,
                "confidence") ??
            0)
            .Normalize();
    }

    public static MapStudioRoadReferenceAnalysis
        ParseRoads(
            string json)
    {
        using var document =
            JsonDocument.Parse(
                ExtractJsonObject(
                    json,
                    "aiRoadJsonMissing"));

        var root =
            document.RootElement;

        var roads =
            new List<
                MapStudioRoadReferencePolyline>();

        if (
            root.TryGetProperty(
                "roads",
                out var roadArray) &&
            roadArray.ValueKind ==
                JsonValueKind.Array)
        {
            foreach (
                var road in
                    roadArray.EnumerateArray())
            {
                if (
                    !road.TryGetProperty(
                        "points",
                        out var pointsElement) ||
                    pointsElement.ValueKind !=
                        JsonValueKind.Array)
                {
                    continue;
                }

                var points =
                    new List<
                        MapStudioRoadPolylinePoint>();

                foreach (
                    var point in
                        pointsElement.EnumerateArray())
                {
                    var x =
                        GetDouble(
                            point,
                            "x");

                    var y =
                        GetDouble(
                            point,
                            "y");

                    if (
                        x is null ||
                        y is null)
                    {
                        continue;
                    }

                    points.Add(
                        new MapStudioRoadPolylinePoint(
                            Math.Clamp(
                                x.Value,
                                0,
                                1),
                            Math.Clamp(
                                y.Value,
                                0,
                                1)));
                }

                if (
                    points.Count <
                    2)
                {
                    continue;
                }

                var laneCount =
                    GetInt(
                        road,
                        "laneCount");

                var width =
                    GetDouble(
                        road,
                        "widthMeters");

                bool? oneWay =
                    null;

                if (
                    road.TryGetProperty(
                        "oneWay",
                        out var oneWayElement))
                {
                    if (
                        oneWayElement.ValueKind ==
                            JsonValueKind.True)
                    {
                        oneWay =
                            true;
                    }
                    else if (
                        oneWayElement.ValueKind ==
                            JsonValueKind.False)
                    {
                        oneWay =
                            false;
                    }
                }

                roads.Add(
                    new MapStudioRoadReferencePolyline(
                        GetString(
                            road,
                            "kind") ??
                        "road",
                        points,
                        laneCount is > 0
                            ? laneCount
                            : null,
                        oneWay,
                        width is > 0 &&
                        double.IsFinite(
                            width.Value)
                            ? width
                            : null));
            }
        }

        return new MapStudioRoadReferenceAnalysis(
            roads,
            GetString(
                root,
                "notes"),
            Math.Clamp(
                GetDouble(
                    root,
                    "confidence") ??
                0,
                0,
                1),
            MapStudioRoadReferenceCoordinateSpace
                .NormalizedImage);
    }

    public static IReadOnlyList<
        MapStudioAiImageReference>
        GetUsableImages(
            IReadOnlyList<
                MapStudioAiImageReference>?
                images,
            int maximum,
            string errorCode)
    {
        var result =
            (images ??
                Array.Empty<
                    MapStudioAiImageReference>())
            .Where(
                image =>
                    image.IsUsable)
            .Take(
                maximum)
            .ToArray();

        if (
            result.Length ==
            0)
        {
            throw new InvalidDataException(
                errorCode);
        }

        return result;
    }

    public static string ExtractJsonObject(
        string text,
        string errorCode)
    {
        if (
            string.IsNullOrWhiteSpace(
                text))
        {
            throw new InvalidDataException(
                errorCode);
        }

        var start =
            text.IndexOf(
                '{');

        var end =
            text.LastIndexOf(
                '}');

        if (
            start < 0 ||
            end <=
                start)
        {
            throw new InvalidDataException(
                errorCode);
        }

        return text[
            start..(
                end +
                1)];
    }

    private static MapStudioBuildingOpeningEstimate?
        BuildOpenings(
            JsonElement root)
    {
        var windows =
            GetInt(
                root,
                "windowsPerFloor");

        var doors =
            GetInt(
                root,
                "doorCount");

        var width =
            GetDouble(
                root,
                "typicalWindowWidthMeters");

        var height =
            GetDouble(
                root,
                "typicalWindowHeightMeters");

        if (
            windows is null &&
            doors is null &&
            width is null &&
            height is null)
        {
            return null;
        }

        return new MapStudioBuildingOpeningEstimate(
            Math.Max(
                0,
                windows ??
                0),
            Math.Max(
                0,
                doors ??
                0),
            width,
            height);
    }

    private static double? GetDouble(
        JsonElement root,
        string name) =>
        root.TryGetProperty(
            name,
            out var value) &&
        value.ValueKind ==
            JsonValueKind.Number &&
        value.TryGetDouble(
            out var number)
            ? number
            : null;

    private static int? GetInt(
        JsonElement root,
        string name) =>
        root.TryGetProperty(
            name,
            out var value) &&
        value.ValueKind ==
            JsonValueKind.Number &&
        value.TryGetInt32(
            out var number)
            ? number
            : null;

    private static string? GetString(
        JsonElement root,
        string name)
    {
        if (
            !root.TryGetProperty(
                name,
                out var value) ||
            value.ValueKind !=
                JsonValueKind.String)
        {
            return null;
        }

        var text =
            value.GetString();

        return string.IsNullOrWhiteSpace(
                text)
            ? null
            : text.Trim();
    }

    private static MapStudioBuildingRoofType
        ParseRoofType(
            string? value) =>
        value?
            .Trim()
            .ToLowerInvariant() switch
        {
            "flat" =>
                MapStudioBuildingRoofType
                    .Flat,
            "gable" =>
                MapStudioBuildingRoofType
                    .Gable,
            "hip" =>
                MapStudioBuildingRoofType
                    .Hip,
            "shed" =>
                MapStudioBuildingRoofType
                    .Shed,
            "mansard" =>
                MapStudioBuildingRoofType
                    .Mansard,
            "dome" =>
                MapStudioBuildingRoofType
                    .Dome,
            "custom" =>
                MapStudioBuildingRoofType
                    .Custom,
            _ =>
                MapStudioBuildingRoofType
                    .Unknown
        };
}
