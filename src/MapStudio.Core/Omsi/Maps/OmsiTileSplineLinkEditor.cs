using System.Globalization;
using MapStudio.Core.Omsi.Config;

namespace MapStudio.Core.Omsi.Maps;

public static class OmsiTileSplineLinkEditor
{
    public static OmsiTileSplineLinkEditResult ApplyLinks(
        OmsiConfigDocument document,
        IReadOnlyList<OmsiSplineLinkEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(edits);

        if (edits.Count == 0)
        {
            return new OmsiTileSplineLinkEditResult(
                document.ToBytes(),
                0);
        }

        var lines =
            document.Lines.ToArray();

        var sections =
            document.Sections
                .Where(IsSplineSection)
                .ToArray();

        var applied = 0;
        var usedOrdinals =
            new HashSet<int>();

        foreach (var edit in edits)
        {
            if (
                edit.SourceSectionOrdinal < 0 ||
                edit.SourceSectionOrdinal >=
                    sections.Length ||
                !usedOrdinals.Add(
                    edit.SourceSectionOrdinal) ||
                edit.PreviousSplineId ==
                    edit.SplineId ||
                edit.NextSplineId ==
                    edit.SplineId ||
                (
                    edit.PreviousSplineId != -1 &&
                    edit.PreviousSplineId ==
                        edit.NextSplineId
                ))
            {
                throw new InvalidDataException(
                    "invalidSplineLinkEdit");
            }

            var section =
                sections[
                    edit.SourceSectionOrdinal];

            var indices =
                GetDataLineIndices(section);

            if (indices.Count < 13)
            {
                throw new InvalidDataException(
                    "malformedSplineSection");
            }

            var values =
                indices
                    .Select(index =>
                        lines[index].Trim())
                    .ToArray();

            var isHeightSpline =
                string.Equals(
                    section.Keyword,
                    "spline_h",
                    StringComparison.OrdinalIgnoreCase);

            if (
                isHeightSpline !=
                    edit.IsHeightSpline ||
                !int.TryParse(
                    values[2],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var sourceId) ||
                !int.TryParse(
                    values[3],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var sourcePreviousId) ||
                !int.TryParse(
                    values[4],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var sourceNextId) ||
                sourceId !=
                    edit.SplineId ||
                sourcePreviousId !=
                    edit.OriginalPreviousSplineId ||
                sourceNextId !=
                    edit.OriginalNextSplineId ||
                !string.Equals(
                    values[1],
                    edit.SplinePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "splineLinkSourceChanged");
            }

            lines[indices[3]] =
                edit.PreviousSplineId
                    .ToString(
                        CultureInfo.InvariantCulture);

            lines[indices[4]] =
                edit.NextSplineId
                    .ToString(
                        CultureInfo.InvariantCulture);

            applied++;
        }

        return new OmsiTileSplineLinkEditResult(
            Encode(
                document,
                lines),
            applied);
    }

    private static bool IsSplineSection(
        OmsiConfigSection section) =>
        string.Equals(
            section.Keyword,
            "spline",
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            section.Keyword,
            "spline_h",
            StringComparison.OrdinalIgnoreCase);

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
