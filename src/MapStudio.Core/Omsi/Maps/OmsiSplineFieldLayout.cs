using System.Globalization;
using MapStudio.Core.Omsi.Config;

namespace MapStudio.Core.Omsi.Maps;

internal sealed record OmsiSplineFieldLayout(
    int Version,
    int HeaderIndex,
    int PathIndex,
    int IdIndex,
    int PreviousIndex,
    int? NextIndex,
    int XIndex,
    int ZIndex,
    int YIndex,
    int RotationIndex,
    int LengthIndex,
    int RadiusIndex,
    int GradientStartIndex,
    int GradientEndIndex,
    int ExtraStartIndex)
{
    internal static int ReadVersion(
        OmsiConfigDocument document)
    {
        var value =
            document
                .FindFirstSection("version")
                ?.DataLines
                .FirstOrDefault();

        return int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var version) &&
            version > 0
                ? version
                : 14;
    }

    internal static bool IsSplineSection(
        OmsiConfigSection section) =>
        string.Equals(
            section.Keyword,
            "spline",
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            section.Keyword,
            "spline_h",
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            section.Keyword,
            "splineAbschnitt",
            StringComparison.OrdinalIgnoreCase);

    internal static bool TryCreate(
        int version,
        int valueCount,
        out OmsiSplineFieldLayout layout)
    {
        var hasComplexity =
            version >= 9;

        var headerIndex =
            hasComplexity ? 0 : -1;

        var pathIndex =
            hasComplexity ? 1 : 0;

        var idIndex =
            pathIndex + 1;

        var previousIndex =
            idIndex + 1;

        int? nextIndex =
            version >= 11
                ? previousIndex + 1
                : null;

        var xIndex =
            (nextIndex ?? previousIndex) + 1;

        var zIndex = xIndex + 1;
        var yIndex = xIndex + 2;
        var rotationIndex = xIndex + 3;
        var lengthIndex = xIndex + 4;
        var radiusIndex = xIndex + 5;
        var gradientStartIndex = xIndex + 6;
        var gradientEndIndex = xIndex + 7;
        var extraStartIndex = gradientEndIndex + 1;

        layout =
            new OmsiSplineFieldLayout(
                version,
                headerIndex,
                pathIndex,
                idIndex,
                previousIndex,
                nextIndex,
                xIndex,
                zIndex,
                yIndex,
                rotationIndex,
                lengthIndex,
                radiusIndex,
                gradientStartIndex,
                gradientEndIndex,
                extraStartIndex);

        return valueCount >=
            extraStartIndex;
    }

    internal static List<int>
        GetDataLineIndices(
            OmsiConfigSection section)
    {
        var result =
            new List<int>();

        for (
            var offset = 0;
            offset <
                section.RawBodyLines.Count;
            offset++)
        {
            var value =
                section.RawBodyLines[
                    offset].Trim();

            if (
                value.Length == 0 ||
                value.StartsWith('#'))
            {
                continue;
            }

            result.Add(
                section.KeywordLineIndex +
                1 +
                offset);
        }

        return result;
    }
}
