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
                .Where(
                    OmsiSplineFieldLayout
                        .IsSplineSection)
                .ToArray();

        var version =
            OmsiSplineFieldLayout
                .ReadVersion(document);

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
                OmsiSplineFieldLayout
                    .GetDataLineIndices(
                        section);

            if (
                !OmsiSplineFieldLayout.TryCreate(
                    version,
                    indices.Count,
                    out var layout))
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

            var sourceNextId = -1;

            if (
                layout.NextIndex is int nextIndex &&
                !int.TryParse(
                    values[nextIndex],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out sourceNextId))
            {
                throw new InvalidDataException(
                    "splineLinkSourceChanged");
            }

            if (
                layout.NextIndex is null &&
                (
                    edit.OriginalNextSplineId != -1 ||
                    edit.NextSplineId != -1
                ))
            {
                throw new InvalidDataException(
                    "splineLinkUnsupportedByVersion");
            }

            if (
                isHeightSpline !=
                    edit.IsHeightSpline ||
                !int.TryParse(
                    values[layout.IdIndex],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var sourceId) ||
                !int.TryParse(
                    values[layout.PreviousIndex],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var sourcePreviousId) ||
                sourceId !=
                    edit.SplineId ||
                sourcePreviousId !=
                    edit.OriginalPreviousSplineId ||
                sourceNextId !=
                    edit.OriginalNextSplineId ||
                !string.Equals(
                    values[layout.PathIndex],
                    edit.SplinePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "splineLinkSourceChanged");
            }

            lines[indices[layout.PreviousIndex]] =
                edit.PreviousSplineId
                    .ToString(
                        CultureInfo.InvariantCulture);

            if (layout.NextIndex is int writeNextIndex)
            {
                lines[indices[writeNextIndex]] =
                    edit.NextSplineId
                        .ToString(
                            CultureInfo.InvariantCulture);
            }

            applied++;
        }

        return new OmsiTileSplineLinkEditResult(
            Encode(
                document,
                lines),
            applied);
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
