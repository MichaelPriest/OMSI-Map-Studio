namespace MapStudio.Core.Generation.Terrain;

public sealed record MapStudioElevationGrid(
    int Rows,
    int Columns,
    IReadOnlyList<double> Elevations,
    double MinimumElevation,
    double MaximumElevation,
    string SourceFormat)
{
    public int SampleCount =>
        checked(
            Rows *
            Columns);
}
