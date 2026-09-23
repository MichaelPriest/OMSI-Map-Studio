using System.Globalization;
using MapStudio.Core.Omsi.Config;

namespace MapStudio.Core.Omsi.Maps;

public static class OmsiPlacedSplinePathEditor
{
    public static OmsiTileSplineEditResult ReplacePath(
        OmsiConfigDocument document,
        int sourceSectionOrdinal,
        string sourcePath,
        string replacementPath,
        int splineId,
        int previousSplineId,
        int nextSplineId,
        bool isHeightSpline)
    {
        ArgumentNullException.ThrowIfNull(
            document);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            sourcePath);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            replacementPath);

        var sections =
            document.Sections
                .Where(
                    OmsiSplineFieldLayout
                        .IsSplineSection)
                .ToArray();

        if (
            sourceSectionOrdinal < 0 ||
            sourceSectionOrdinal >=
                sections.Length)
        {
            throw new InvalidDataException(
                "invalidSplineSection");
        }

        var section =
            sections[
                sourceSectionOrdinal];

        var indices =
            OmsiSplineFieldLayout
                .GetDataLineIndices(
                    section);

        var version =
            OmsiSplineFieldLayout
                .ReadVersion(
                    document);

        if (
            !OmsiSplineFieldLayout
                .TryCreate(
                    version,
                    indices.Count,
                    out var layout))
        {
            throw new InvalidDataException(
                "malformedSplineSection");
        }

        var lines =
            document.Lines
                .ToList();

        var values =
            indices
                .Select(
                    index =>
                        lines[index]
                            .Trim())
                .ToArray();

        var sourceNext =
            -1;

        if (
            layout.NextIndex is
                int nextIndex &&
            !int.TryParse(
                values[nextIndex],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out sourceNext))
        {
            throw new InvalidDataException(
                "splineSourceChanged");
        }

        var sourceIsHeight =
            string.Equals(
                section.Keyword,
                "spline_h",
                StringComparison.OrdinalIgnoreCase);

        if (
            sourceIsHeight !=
                isHeightSpline ||
            !int.TryParse(
                values[
                    layout.IdIndex],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var sourceId) ||
            !int.TryParse(
                values[
                    layout.PreviousIndex],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var sourcePrevious) ||
            sourceId !=
                splineId ||
            sourcePrevious !=
                previousSplineId ||
            sourceNext !=
                nextSplineId ||
            !string.Equals(
                values[
                    layout.PathIndex],
                sourcePath,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "splineSourceChanged");
        }

        lines[
            indices[
                layout.PathIndex]] =
            replacementPath.Trim();

        return new OmsiTileSplineEditResult(
            Encode(
                document,
                lines),
            1);
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
                .GetBytes(
                    text);

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
