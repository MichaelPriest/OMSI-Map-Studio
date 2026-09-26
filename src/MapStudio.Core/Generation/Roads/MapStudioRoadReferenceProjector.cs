using MapStudio.Core.AI;

namespace MapStudio.Core.Generation.Roads;

public sealed record MapStudioRoadReferenceImageProjection(
    int ImageWidth,
    int ImageHeight,
    double MetersPerPixel,
    double CenterWorldX,
    double CenterWorldZ)
{
    public MapStudioRoadReferenceImageProjection Normalize()
    {
        if (
            ImageWidth <= 0 ||
            ImageHeight <= 0 ||
            !double.IsFinite(
                MetersPerPixel) ||
            MetersPerPixel <= 0 ||
            !double.IsFinite(
                CenterWorldX) ||
            !double.IsFinite(
                CenterWorldZ))
        {
            throw new InvalidDataException(
                "roadReferenceProjectionInvalid");
        }

        return this;
    }
}

public sealed record MapStudioProjectedRoadReference(
    string Kind,
    IReadOnlyList<MapStudioRoadPoint> Points,
    int? LaneCount,
    bool? OneWay,
    double? WidthMeters);

public static class MapStudioRoadReferenceProjector
{
    public static IReadOnlyList<
        MapStudioProjectedRoadReference>
        Project(
            MapStudioRoadReferenceAnalysis analysis,
            MapStudioRoadReferenceImageProjection projection)
    {
        ArgumentNullException.ThrowIfNull(
            analysis);

        ArgumentNullException.ThrowIfNull(
            projection);

        var normalizedProjection =
            projection.Normalize();

        var result =
            new List<
                MapStudioProjectedRoadReference>();

        foreach (
            var road in
                analysis.Roads ??
                Array.Empty<
                    MapStudioRoadReferencePolyline>())
        {
            if (
                road.Points is null ||
                road.Points.Count <
                    2)
            {
                continue;
            }

            var points =
                new List<
                    MapStudioRoadPoint>(
                        road.Points.Count);

            foreach (
                var point in
                    road.Points)
            {
                if (
                    !double.IsFinite(
                        point.X) ||
                    !double.IsFinite(
                        point.Y))
                {
                    continue;
                }

                MapStudioRoadPoint
                    world;

                switch (
                    analysis
                        .CoordinateSpace)
                {
                    case
                        MapStudioRoadReferenceCoordinateSpace
                            .NormalizedImage:
                    {
                        var pixelX =
                            point.X *
                            normalizedProjection
                                .ImageWidth;

                        var pixelY =
                            point.Y *
                            normalizedProjection
                                .ImageHeight;

                        world =
                            FromPixels(
                                normalizedProjection,
                                pixelX,
                                pixelY);

                        break;
                    }

                    case
                        MapStudioRoadReferenceCoordinateSpace
                            .ImagePixels:
                    {
                        var sourceWidth =
                            analysis.ImageWidth ??
                            normalizedProjection
                                .ImageWidth;

                        var sourceHeight =
                            analysis.ImageHeight ??
                            normalizedProjection
                                .ImageHeight;

                        if (
                            sourceWidth <=
                                0 ||
                            sourceHeight <=
                                0)
                        {
                            continue;
                        }

                        var pixelX =
                            point.X *
                            normalizedProjection
                                .ImageWidth /
                            sourceWidth;

                        var pixelY =
                            point.Y *
                            normalizedProjection
                                .ImageHeight /
                            sourceHeight;

                        world =
                            FromPixels(
                                normalizedProjection,
                                pixelX,
                                pixelY);

                        break;
                    }

                    case
                        MapStudioRoadReferenceCoordinateSpace
                            .WorldMeters:
                    {
                        world =
                            new MapStudioRoadPoint(
                                point.X,
                                point.Y);

                        break;
                    }

                    default:
                        continue;
                }

                if (
                    points.Count ==
                        0 ||
                    points[^1]
                        .DistanceTo(
                            world) >=
                        0.05)
                {
                    points.Add(
                        world);
                }
            }

            if (points.Count < 2)
            {
                continue;
            }

            result.Add(
                new MapStudioProjectedRoadReference(
                    string.IsNullOrWhiteSpace(
                        road.Kind)
                        ? "road"
                        : road.Kind.Trim(),
                    points,
                    road.LaneCount,
                    road.OneWay,
                    road.WidthMeters));
        }

        return result;
    }

    private static MapStudioRoadPoint
        FromPixels(
            MapStudioRoadReferenceImageProjection projection,
            double pixelX,
            double pixelY)
    {
        var centerX =
            projection.ImageWidth /
            2.0;

        var centerY =
            projection.ImageHeight /
            2.0;

        var eastMeters =
            (
                pixelX -
                centerX
            ) *
            projection
                .MetersPerPixel;

        var southMeters =
            (
                pixelY -
                centerY
            ) *
            projection
                .MetersPerPixel;

        return new MapStudioRoadPoint(
            projection.CenterWorldX +
                eastMeters,
            projection.CenterWorldZ +
                southMeters);
    }
}
