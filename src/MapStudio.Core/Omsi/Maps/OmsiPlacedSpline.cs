namespace MapStudio.Core.Omsi.Maps;

public sealed record OmsiPlacedSpline(
    string HeaderValue,
    string SplinePath,
    int SplineId,
    int PreviousSplineId,
    int NextSplineId,
    double X,
    double Z,
    double Y,
    double Rotation,
    double Length,
    double Radius,
    double GradientStart,
    double GradientEnd,
    bool IsHeightSpline,
    IReadOnlyList<string> ExtraValues)
{
    public int SourceSectionOrdinal
    {
        get;
        init;
    } = -1;

    public double CantStart =>
        ReadExtraDouble(
            IsHeightSpline
                ? 1
                : 0);

    public double CantEnd =>
        ReadExtraDouble(
            IsHeightSpline
                ? 2
                : 1);

    public double SkewStart =>
        ReadExtraDouble(
            IsHeightSpline
                ? 3
                : 2);

    public double SkewEnd =>
        ReadExtraDouble(
            IsHeightSpline
                ? 4
                : 3);

    public bool IsMirrored
    {
        get
        {
            var index =
                IsHeightSpline
                    ? 6
                    : 5;

            return
                index <
                    ExtraValues.Count &&
                string.Equals(
                    ExtraValues[index]
                        .Trim(),
                    "mirror",
                    StringComparison.OrdinalIgnoreCase);
        }
    }

    private double ReadExtraDouble(
        int index)
    {
        if (
            index < 0 ||
            index >=
                ExtraValues.Count)
        {
            return 0;
        }

        return double.TryParse(
            ExtraValues[index],
            System.Globalization
                .NumberStyles.Float,
            System.Globalization
                .CultureInfo.InvariantCulture,
            out var value) &&
            double.IsFinite(value)
                ? value
                : 0;
    }

    public IReadOnlyList<OmsiTrafficRule>
        TrafficRules { get; init; } =
            Array.Empty<OmsiTrafficRule>();
}
