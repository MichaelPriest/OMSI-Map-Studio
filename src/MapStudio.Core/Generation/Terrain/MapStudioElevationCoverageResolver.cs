using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Omsi.Maps;

namespace MapStudio.Core.Generation.Terrain;

public readonly record struct MapStudioElevationCoverageBounds(
    double South,
    double West,
    double North,
    double East);

public static class MapStudioElevationCoverageResolver
{
    public static MapStudioElevationCoverageBounds Resolve(
        IReadOnlyList<OmsiTileReference> tiles,
        MapStudioGeographicAnchor anchor,
        double marginMeters = 1.0)
    {
        ArgumentNullException.ThrowIfNull(
            tiles);

        if (
            tiles.Count == 0 ||
            !double.IsFinite(
                marginMeters) ||
            marginMeters < 0)
        {
            throw new InvalidDataException(
                "invalidElevationCoverageTiles");
        }

        var minTileX =
            tiles.Min(
                tile =>
                    tile.X);

        var maxTileX =
            tiles.Max(
                tile =>
                    tile.X);

        var minTileY =
            tiles.Min(
                tile =>
                    tile.Y);

        var maxTileY =
            tiles.Max(
                tile =>
                    tile.Y);

        var minWorldX =
            OmsiTileGrid.GetOriginX(
                minTileX) -
            marginMeters;

        var maxWorldX =
            OmsiTileGrid.GetOriginX(
                checked(
                    maxTileX +
                    1)) +
            marginMeters;

        var minWorldZ =
            OmsiTileGrid.GetOriginZ(
                minTileY) -
            marginMeters;

        var maxWorldZ =
            OmsiTileGrid.GetOriginZ(
                checked(
                    maxTileY +
                    1)) +
            marginMeters;

        var corners =
            new[]
            {
                new MapStudioRoadPoint(
                    minWorldX,
                    minWorldZ),
                new MapStudioRoadPoint(
                    maxWorldX,
                    minWorldZ),
                new MapStudioRoadPoint(
                    minWorldX,
                    maxWorldZ),
                new MapStudioRoadPoint(
                    maxWorldX,
                    maxWorldZ)
            }
            .Select(
                point =>
                    MapStudioGeographicProjection
                        .Unproject(
                            anchor,
                            point))
            .ToArray();

        var south =
            corners.Min(
                point =>
                    point.Latitude);

        var north =
            corners.Max(
                point =>
                    point.Latitude);

        var west =
            corners.Min(
                point =>
                    point.Longitude);

        var east =
            corners.Max(
                point =>
                    point.Longitude);

        if (
            !double.IsFinite(
                south) ||
            !double.IsFinite(
                west) ||
            !double.IsFinite(
                north) ||
            !double.IsFinite(
                east) ||
            south >= north ||
            west >= east)
        {
            throw new InvalidDataException(
                "invalidElevationCoverageBounds");
        }

        return new MapStudioElevationCoverageBounds(
            south,
            west,
            north,
            east);
    }

    public static MapStudioElevationCoverageBounds Union(
        MapStudioElevationCoverageBounds left,
        double south,
        double west,
        double north,
        double east)
    {
        if (
            !double.IsFinite(
                south) ||
            !double.IsFinite(
                west) ||
            !double.IsFinite(
                north) ||
            !double.IsFinite(
                east) ||
            south >= north ||
            west >= east)
        {
            throw new InvalidDataException(
                "invalidElevationCoverageBounds");
        }

        return new MapStudioElevationCoverageBounds(
            Math.Min(
                left.South,
                south),
            Math.Min(
                left.West,
                west),
            Math.Max(
                left.North,
                north),
            Math.Max(
                left.East,
                east));
    }
}
