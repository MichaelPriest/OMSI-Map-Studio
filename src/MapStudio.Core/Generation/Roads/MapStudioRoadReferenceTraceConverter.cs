using MapStudio.Core.AI;

namespace MapStudio.Core.Generation.Roads;

public sealed record MapStudioRoadReferenceProjection(
    int ImageWidth,
    int ImageHeight,
    double MetersPerPixel,
    double AnchorWorldX,
    double AnchorWorldZ)
{
    public double WidthMeters =>
        ImageWidth *
        MetersPerPixel;

    public double HeightMeters =>
        ImageHeight *
        MetersPerPixel;
}

public sealed record MapStudioRoadReferenceConversionResult(
    IReadOnlyList<MapStudioRoadTrace> Traces,
    int SkippedRoads,
    int SkippedPoints);

public sealed class MapStudioRoadReferenceTraceConverter
{
    public MapStudioRoadReferenceConversionResult Convert(
        MapStudioRoadReferenceAnalysis analysis,
        MapStudioRoadReferenceProjection projection,
        Func<MapStudioRoadReferencePolyline, string>
            profileResolver)
    {
        ArgumentNullException.ThrowIfNull(
            analysis);

        ArgumentNullException.ThrowIfNull(
            projection);

        ArgumentNullException.ThrowIfNull(
            profileResolver);

        if (
            projection.ImageWidth <=
                0 ||
            projection.ImageHeight <=
                0 ||
            !double.IsFinite(
                projection.MetersPerPixel) ||
            projection.MetersPerPixel <=
                0 ||
            !double.IsFinite(
                projection.AnchorWorldX) ||
            !double.IsFinite(
                projection.AnchorWorldZ))
        {
            throw new InvalidDataException(
                "roadReferenceProjectionInvalid");
        }

        var traces =
            new List<MapStudioRoadTrace>();

        var skippedRoads =
            0;

        var skippedPoints =
            0;

        var roadIndex =
            0;

        foreach (
            var road in
                analysis.Roads ??
                Array.Empty<
                    MapStudioRoadReferencePolyline>())
        {
            roadIndex++;

            if (
                road.Points is null ||
                road.Points.Count <
                    2)
            {
                skippedRoads++;
                continue;
            }

            var points =
                new List<MapStudioRoadPoint>(
                    road.Points.Count);

            foreach (
                var point in
                    road.Points)
            {
                if (
                    !TryConvertPoint(
                        point,
                        analysis,
                        projection,
                        out var world))
                {
                    skippedPoints++;
                    continue;
                }

                if (
                    points.Count >
                        0 &&
                    points[^1]
                        .DistanceTo(
                            world) <
                        0.05)
                {
                    continue;
                }

                points.Add(
                    world);
            }

            if (
                points.Count <
                2)
            {
                skippedRoads++;
                continue;
            }

            var profileId =
                profileResolver(
                    road);

            if (
                string.IsNullOrWhiteSpace(
                    profileId))
            {
                skippedRoads++;
                continue;
            }

            traces.Add(
                new MapStudioRoadTrace(
                    $"reference-road-{roadIndex:0000}",
                    points,
                    profileId,
                    road.LaneCount,
                    road.OneWay,
                    road.WidthMeters));
        }

        return new MapStudioRoadReferenceConversionResult(
            traces,
            skippedRoads,
            skippedPoints);
    }

    private static bool TryConvertPoint(
        MapStudioRoadPolylinePoint point,
        MapStudioRoadReferenceAnalysis analysis,
        MapStudioRoadReferenceProjection projection,
        out MapStudioRoadPoint world)
    {
        world =
            default;

        if (
            !double.IsFinite(
                point.X) ||
            !double.IsFinite(
                point.Y))
        {
            return false;
        }

        switch (
            analysis.CoordinateSpace)
        {
            case MapStudioRoadReferenceCoordinateSpace
                .NormalizedImage:
            {
                if (
                    point.X is
                        < 0 or > 1 ||
                    point.Y is
                        < 0 or > 1)
                {
                    return false;
                }

                world =
                    new MapStudioRoadPoint(
                        projection.AnchorWorldX +
                        (
                            point.X -
                            0.5
                        ) *
                        projection.WidthMeters,
                        projection.AnchorWorldZ +
                        (
                            point.Y -
                            0.5
                        ) *
                        projection.HeightMeters);

                return true;
            }

            case MapStudioRoadReferenceCoordinateSpace
                .ImagePixels:
            {
                var width =
                    analysis.ImageWidth ??
                    projection.ImageWidth;

                var height =
                    analysis.ImageHeight ??
                    projection.ImageHeight;

                if (
                    width <= 0 ||
                    height <= 0 ||
                    point.X <
                        0 ||
                    point.Y <
                        0 ||
                    point.X >
                        width ||
                    point.Y >
                        height)
                {
                    return false;
                }

                world =
                    new MapStudioRoadPoint(
                        projection.AnchorWorldX +
                        (
                            point.X /
                            width -
                            0.5
                        ) *
                        projection.WidthMeters,
                        projection.AnchorWorldZ +
                        (
                            point.Y /
                            height -
                            0.5
                        ) *
                        projection.HeightMeters);

                return true;
            }

            case MapStudioRoadReferenceCoordinateSpace
                .WorldMeters:
                world =
                    new MapStudioRoadPoint(
                        point.X,
                        point.Y);

                return true;

            default:
                return false;
        }
    }
}
