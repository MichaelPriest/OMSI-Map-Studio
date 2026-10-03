namespace MapStudio.Core.Omsi.Junctions;

public static class MapStudioJunctionGeometrySizing
{
    public static double ResolveSurfaceExtentMeters(
        IEnumerable<double> physicalWidths)
    {
        ArgumentNullException.ThrowIfNull(
            physicalWidths);

        var maximumWidth =
            physicalWidths
                .Where(
                    width =>
                        double.IsFinite(
                            width) &&
                        width >
                            0)
                .DefaultIfEmpty(
                    7.0)
                .Max();

        return Math.Clamp(
            maximumWidth *
                0.50 +
            0.75,
            2.75,
            14.0);
    }

    public static double ResolvePathRadiusMeters(
        IEnumerable<double> physicalWidths) =>
        Math.Max(
            2.25,
            ResolveSurfaceExtentMeters(
                physicalWidths) -
            0.35);
}
