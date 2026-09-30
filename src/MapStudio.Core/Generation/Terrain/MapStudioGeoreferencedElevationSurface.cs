using MapStudio.Core.Generation.Roads;

namespace MapStudio.Core.Generation.Terrain;

public sealed class MapStudioGeoreferencedElevationSurface
{
    public MapStudioGeoreferencedElevationSurface(
        MapStudioElevationGrid grid,
        double south,
        double west,
        double north,
        double east,
        double? baseElevationMeters = null)
    {
        ArgumentNullException.ThrowIfNull(grid);

        if (
            grid.Rows < 2 ||
            grid.Columns < 2 ||
            grid.Elevations.Count !=
                checked(
                    grid.Rows *
                    grid.Columns) ||
            grid.Elevations.Any(
                value =>
                    !double.IsFinite(value)) ||
            !double.IsFinite(south) ||
            !double.IsFinite(west) ||
            !double.IsFinite(north) ||
            !double.IsFinite(east) ||
            south is < -90 or > 90 ||
            north is < -90 or > 90 ||
            west is < -180 or > 180 ||
            east is < -180 or > 180 ||
            south >= north ||
            west >= east)
        {
            throw new InvalidDataException(
                "invalidGeoreferencedElevationSurface");
        }

        var baseline =
            baseElevationMeters ??
            grid.MinimumElevation;

        if (!double.IsFinite(baseline))
        {
            throw new InvalidDataException(
                "invalidElevationBaseline");
        }

        Grid = grid;
        South = south;
        West = west;
        North = north;
        East = east;
        BaseElevationMeters = baseline;
    }

    public MapStudioElevationGrid Grid { get; }

    public double South { get; }

    public double West { get; }

    public double North { get; }

    public double East { get; }

    public double BaseElevationMeters { get; }

    public bool Contains(
        double latitude,
        double longitude) =>
        double.IsFinite(latitude) &&
        double.IsFinite(longitude) &&
        latitude >= South &&
        latitude <= North &&
        longitude >= West &&
        longitude <= East;

    public bool TrySampleAbsoluteHeight(
        double latitude,
        double longitude,
        out double height)
    {
        if (!Contains(latitude, longitude))
        {
            height = 0;
            return false;
        }

        var rowPosition =
            (
                North -
                latitude
            ) /
            (
                North -
                South
            ) *
            (
                Grid.Rows -
                1
            );

        var columnPosition =
            (
                longitude -
                West
            ) /
            (
                East -
                West
            ) *
            (
                Grid.Columns -
                1
            );

        var row0 =
            Math.Clamp(
                (int)Math.Floor(rowPosition),
                0,
                Grid.Rows - 1);

        var row1 =
            Math.Min(
                Grid.Rows - 1,
                row0 + 1);

        var column0 =
            Math.Clamp(
                (int)Math.Floor(columnPosition),
                0,
                Grid.Columns - 1);

        var column1 =
            Math.Min(
                Grid.Columns - 1,
                column0 + 1);

        var rowFraction =
            Math.Clamp(
                rowPosition - row0,
                0,
                1);

        var columnFraction =
            Math.Clamp(
                columnPosition - column0,
                0,
                1);

        var northWest =
            Read(
                row0,
                column0);

        var northEast =
            Read(
                row0,
                column1);

        var southWest =
            Read(
                row1,
                column0);

        var southEast =
            Read(
                row1,
                column1);

        var northValue =
            Lerp(
                northWest,
                northEast,
                columnFraction);

        var southValue =
            Lerp(
                southWest,
                southEast,
                columnFraction);

        height =
            Lerp(
                northValue,
                southValue,
                rowFraction);

        return true;
    }

    public bool TrySampleRelativeHeight(
        double latitude,
        double longitude,
        out double height)
    {
        if (
            !TrySampleAbsoluteHeight(
                latitude,
                longitude,
                out var absolute))
        {
            height = 0;
            return false;
        }

        height =
            absolute -
            BaseElevationMeters;

        return true;
    }

    public bool TrySampleRelativeHeight(
        MapStudioGeographicAnchor anchor,
        MapStudioRoadPoint worldPoint,
        out double height)
    {
        var geographic =
            MapStudioGeographicProjection
                .Unproject(
                    anchor,
                    worldPoint);

        return TrySampleRelativeHeight(
            geographic.Latitude,
            geographic.Longitude,
            out height);
    }

    public double SampleRelativeHeightOrDefault(
        MapStudioGeographicAnchor anchor,
        MapStudioRoadPoint worldPoint,
        double fallback = 0) =>
        TrySampleRelativeHeight(
            anchor,
            worldPoint,
            out var height)
                ? height
                : fallback;

    private double Read(
        int row,
        int column) =>
        Grid.Elevations[
            checked(
                row *
                Grid.Columns +
                column)];

    private static double Lerp(
        double start,
        double end,
        double amount) =>
        start +
        (
            end -
            start
        ) *
        amount;
}
