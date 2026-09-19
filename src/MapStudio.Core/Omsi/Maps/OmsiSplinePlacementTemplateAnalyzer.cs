using System.Globalization;

namespace MapStudio.Core.Omsi.Maps;

public static class OmsiSplinePlacementTemplateAnalyzer
{
    private const int NormalSplineExtraValueCount = 5;
    private const int HeightSplineExtraValueCount = 6;
    private const double ZeroTolerance = 1e-12;

    public static OmsiPlacedSpline? FindNeutralTemplate(
        IReadOnlyList<OmsiTileContent> contents,
        bool isHeightSpline)
    {
        ArgumentNullException.ThrowIfNull(contents);

        var expectedExtraCount =
            isHeightSpline
                ? HeightSplineExtraValueCount
                : NormalSplineExtraValueCount;

        foreach (var spline in
            contents.SelectMany(
                content => content.Splines))
        {
            if (
                spline.IsHeightSpline !=
                    isHeightSpline ||
                string.IsNullOrWhiteSpace(
                    spline.HeaderValue) ||
                spline.ExtraValues.Count !=
                    expectedExtraCount)
            {
                continue;
            }

            var allNeutral = true;

            foreach (var value in
                spline.ExtraValues)
            {
                if (
                    !double.TryParse(
                        value,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var numeric) ||
                    !double.IsFinite(numeric) ||
                    Math.Abs(numeric) >
                        ZeroTolerance)
                {
                    allNeutral = false;
                    break;
                }
            }

            if (allNeutral)
            {
                return spline;
            }
        }

        return null;
    }

    public static OmsiPlacedSpline? FindNeutralNormalTemplate(
        IReadOnlyList<OmsiTileContent> contents) =>
        FindNeutralTemplate(
            contents,
            isHeightSpline: false);

    public static OmsiPlacedSpline? FindNeutralHeightTemplate(
        IReadOnlyList<OmsiTileContent> contents) =>
        FindNeutralTemplate(
            contents,
            isHeightSpline: true);
}
