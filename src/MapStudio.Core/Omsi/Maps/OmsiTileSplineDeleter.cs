using System.Globalization;
using MapStudio.Core.Omsi.Config;

namespace MapStudio.Core.Omsi.Maps;

public static class OmsiTileSplineDeleter
{
    public static OmsiTileSplineDeleteResult Remove(
        OmsiConfigDocument document,
        int sourceSectionOrdinal,
        string splinePath,
        int splineId,
        int previousSplineId,
        int nextSplineId,
        bool isHeightSpline)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            splinePath);

        var splineSections =
            document.Sections
                .Where(
                    section =>
                        string.Equals(
                            section.Keyword,
                            "spline",
                            StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(
                            section.Keyword,
                            "spline_h",
                            StringComparison.OrdinalIgnoreCase))
                .ToArray();

        if (
            sourceSectionOrdinal < 0 ||
            sourceSectionOrdinal >=
                splineSections.Length)
        {
            throw new InvalidDataException(
                "invalidSplineSection");
        }

        var section =
            splineSections[
                sourceSectionOrdinal];

        var dataLineIndices =
            GetDataLineIndices(section);

        if (dataLineIndices.Count < 13)
        {
            throw new InvalidDataException(
                "malformedSplineSection");
        }

        var lines =
            document.Lines;

        var dataValues =
            dataLineIndices
                .Select(index =>
                    lines[index].Trim())
                .ToArray();

        var sourceIsHeightSpline =
            string.Equals(
                section.Keyword,
                "spline_h",
                StringComparison.OrdinalIgnoreCase);

        if (
            sourceIsHeightSpline !=
                isHeightSpline ||
            !int.TryParse(
                dataValues[2],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var sourceSplineId) ||
            !int.TryParse(
                dataValues[3],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var sourcePreviousSplineId) ||
            !int.TryParse(
                dataValues[4],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var sourceNextSplineId) ||
            sourceSplineId != splineId ||
            sourcePreviousSplineId !=
                previousSplineId ||
            sourceNextSplineId !=
                nextSplineId ||
            !string.Equals(
                dataValues[1],
                splinePath,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "splineSourceChanged");
        }

        var removedLineIndices =
            dataLineIndices.ToHashSet();

        removedLineIndices.Add(
            section.KeywordLineIndex);

        var remainingLines =
            document.Lines
                .Where((_, index) =>
                    !removedLineIndices
                        .Contains(index))
                .ToArray();

        return new OmsiTileSplineDeleteResult(
            Encode(
                document,
                remainingLines),
            1);
    }

    private static List<int> GetDataLineIndices(
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

    private static byte[] Encode(
        OmsiConfigDocument document,
        IReadOnlyList<string> lines)
    {
        var text =
            string.Join(
                document.NewLine,
                lines);

        if (
            document.HasTrailingNewLine &&
            lines.Count > 0)
        {
            text += document.NewLine;
        }

        var body =
            document.TextEncoding
                .GetBytes(text);

        if (!document.HasByteOrderMark)
        {
            return body;
        }

        var preamble =
            document.TextEncoding
                .GetPreamble();

        if (preamble.Length == 0)
        {
            return body;
        }

        var result =
            new byte[
                preamble.Length +
                body.Length];

        Buffer.BlockCopy(
            preamble,
            0,
            result,
            0,
            preamble.Length);

        Buffer.BlockCopy(
            body,
            0,
            result,
            preamble.Length,
            body.Length);

        return result;
    }
}
