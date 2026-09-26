using MapStudio.Core.Generation.Roads;

namespace MapStudio.Core.Generation.Vegetation;

public sealed class MapStudioVegetationLineSampler
{
    public IReadOnlyList<
        MapStudioProjectedVegetationPoint>
        ProjectAndSample(
            IReadOnlyList<
                MapStudioGeoVegetationLine>
                lines,
            MapStudioGeographicAnchor anchor,
            double spacingMeters = 4.0,
            int maxPoints = 10_000)
    {
        ArgumentNullException.ThrowIfNull(
            lines);

        if (
            !double.IsFinite(
                spacingMeters) ||
            spacingMeters <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(
                    spacingMeters));
        }

        if (maxPoints <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(
                    maxPoints));
        }

        var result =
            new List<
                MapStudioProjectedVegetationPoint>();

        foreach (
            var line in lines)
        {
            if (
                line.Points.Count <
                    2)
            {
                continue;
            }

            var projected =
                line.Points
                    .Select(
                        point =>
                            MapStudioGeographicProjection
                                .Project(
                                    anchor,
                                    new MapStudioGeoRoadPoint(
                                        point.Latitude,
                                        point.Longitude)))
                    .ToArray();

            var totalLength =
                0.0;

            for (
                var index = 1;
                index <
                    projected.Length;
                index++)
            {
                totalLength +=
                    projected[
                        index - 1]
                        .DistanceTo(
                            projected[
                                index]);
            }

            if (
                totalLength <=
                    1e-6)
            {
                continue;
            }

            var nextDistance =
                0.0;

            var cumulative =
                0.0;

            var sampleIndex =
                0;

            for (
                var segmentIndex = 1;
                segmentIndex <
                    projected.Length;
                segmentIndex++)
            {
                var from =
                    projected[
                        segmentIndex - 1];

                var to =
                    projected[
                        segmentIndex];

                var segmentLength =
                    from.DistanceTo(
                        to);

                if (
                    segmentLength <=
                        1e-6)
                {
                    continue;
                }

                var segmentEnd =
                    cumulative +
                    segmentLength;

                while (
                    nextDistance <=
                        segmentEnd +
                        1e-8)
                {
                    if (
                        nextDistance +
                            1e-8 <
                        cumulative)
                    {
                        nextDistance +=
                            spacingMeters;

                        continue;
                    }

                    var t =
                        Math.Clamp(
                            (
                                nextDistance -
                                cumulative
                            ) /
                            segmentLength,
                            0.0,
                            1.0);

                    result.Add(
                        CreatePoint(
                            line,
                            MapStudioRoadPoint.Lerp(
                                from,
                                to,
                                t),
                            sampleIndex++));

                    if (
                        result.Count >=
                            maxPoints)
                    {
                        return result;
                    }

                    nextDistance +=
                        spacingMeters;
                }

                cumulative =
                    segmentEnd;
            }

            var last =
                projected[^1];

            if (
                result.Count <
                    maxPoints &&
                (
                    result.Count ==
                        0 ||
                    result[^1]
                        .Position
                        .DistanceTo(
                            last) >
                        Math.Min(
                            0.25,
                            spacingMeters *
                            0.10)
                ))
            {
                result.Add(
                    CreatePoint(
                        line,
                        last,
                        sampleIndex));
            }

            if (
                result.Count >=
                    maxPoints)
            {
                return result;
            }
        }

        return result;
    }

    private static MapStudioProjectedVegetationPoint
        CreatePoint(
            MapStudioGeoVegetationLine line,
            MapStudioRoadPoint position,
            int sampleIndex) =>
        new(
            line.Id +
            "-sample-" +
            sampleIndex,
            position,
            line.Kind ==
                MapStudioOsmVegetationLineKind
                    .TreeRow
                ? MapStudioOsmVegetationKind
                    .Tree
                : MapStudioOsmVegetationKind
                    .Shrub,
            line.Species,
            line.Genus,
            line.LeafType,
            line.Name);
}
