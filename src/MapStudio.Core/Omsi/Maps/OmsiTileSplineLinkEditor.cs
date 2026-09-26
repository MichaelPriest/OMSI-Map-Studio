using System.Globalization;
using MapStudio.Core.Omsi.Config;

namespace MapStudio.Core.Omsi.Maps;

public static class OmsiTileSplineLinkEditor
{
    public static OmsiTileSplineEditResult ApplyLinks(
        OmsiConfigDocument document,
        IReadOnlyList<OmsiSplineLinkEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(edits);

        if (edits.Count == 0)
        {
            return new OmsiTileSplineEditResult(
                document.ToBytes(),
                0);
        }

        var lines =
            document.Lines.ToArray();

        var splineSections =
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
                    splineSections.Length ||
                !usedOrdinals.Add(
                    edit.SourceSectionOrdinal))
            {
                throw new InvalidDataException(
                    "invalidSplineLinkSection");
            }

            var section =
                splineSections[
                    edit.SourceSectionOrdinal];

            var dataLineIndices =
                OmsiSplineFieldLayout
                    .GetDataLineIndices(
                        section);

            if (
                !OmsiSplineFieldLayout.TryCreate(
                    version,
                    dataLineIndices.Count,
                    out var layout) ||
                layout.NextIndex is not
                    { } nextIndex)
            {
                throw new InvalidDataException(
                    "splineLinksUnsupported");
            }

            var dataValues =
                dataLineIndices
                    .Select(
                        index =>
                            lines[index]
                                .Trim())
                    .ToArray();

            var isHeightSpline =
                string.Equals(
                    section.Keyword,
                    "spline_h",
                    StringComparison
                        .OrdinalIgnoreCase);

            if (
                isHeightSpline !=
                    edit.IsHeightSpline ||
                !int.TryParse(
                    dataValues[
                        layout.IdIndex],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var sourceId) ||
                !int.TryParse(
                    dataValues[
                        layout.PreviousIndex],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var sourcePrevious) ||
                !int.TryParse(
                    dataValues[
                        nextIndex],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var sourceNext) ||
                sourceId !=
                    edit.SplineId ||
                sourcePrevious !=
                    edit.PreviousSplineId ||
                sourceNext !=
                    edit.NextSplineId ||
                !string.Equals(
                    dataValues[
                        layout.PathIndex],
                    edit.SplinePath,
                    StringComparison
                        .OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "splineSourceChanged");
            }

            lines[
                dataLineIndices[
                    layout.PreviousIndex]] =
                edit.NewPreviousSplineId
                    .ToString(
                        CultureInfo
                            .InvariantCulture);

            lines[
                dataLineIndices[
                    nextIndex]] =
                edit.NewNextSplineId
                    .ToString(
                        CultureInfo
                            .InvariantCulture);

            applied++;
        }

        return new OmsiTileSplineEditResult(
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
            text +=
                document.NewLine;
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
