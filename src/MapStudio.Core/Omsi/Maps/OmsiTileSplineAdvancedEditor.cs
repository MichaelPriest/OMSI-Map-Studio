using System.Globalization;
using MapStudio.Core.Omsi.Config;

namespace MapStudio.Core.Omsi.Maps;

public static class OmsiTileSplineAdvancedEditor
{
    public static OmsiTileSplineEditResult Apply(
        OmsiConfigDocument document,
        IReadOnlyList<OmsiSplineAdvancedEdit> edits)
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
            document.Lines.ToList();

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

        foreach (
            var edit in edits
                .OrderByDescending(
                    item =>
                        item.SourceSectionOrdinal))
        {
            if (
                edit.SourceSectionOrdinal < 0 ||
                edit.SourceSectionOrdinal >=
                    sections.Length ||
                !double.IsFinite(
                    edit.CantStart) ||
                !double.IsFinite(
                    edit.CantEnd))
            {
                throw new InvalidDataException(
                    "invalidSplineAdvancedEdit");
            }

            var section =
                sections[
                    edit.SourceSectionOrdinal];

            var dataIndices =
                OmsiSplineFieldLayout
                    .GetDataLineIndices(
                        section);

            if (
                !OmsiSplineFieldLayout
                    .TryCreate(
                        version,
                        dataIndices.Count,
                        out var layout))
            {
                throw new InvalidDataException(
                    "malformedSplineSection");
            }

            var dataValues =
                dataIndices
                    .Select(
                        index =>
                            lines[index]
                                .Trim())
                    .ToArray();

            var sourceNext = -1;

            if (
                layout.NextIndex is
                    int nextIndex &&
                !int.TryParse(
                    dataValues[nextIndex],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out sourceNext))
            {
                throw new InvalidDataException(
                    "splineSourceChanged");
            }

            var height =
                string.Equals(
                    section.Keyword,
                    "spline_h",
                    StringComparison.OrdinalIgnoreCase);

            if (
                height !=
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
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "splineSourceChanged");
            }

            var cantStartIndex =
                layout.ExtraStartIndex +
                (
                    height
                        ? 1
                        : 0
                );

            var cantEndIndex =
                cantStartIndex + 1;

            var mirrorIndex =
                layout.ExtraStartIndex +
                (
                    height
                        ? 6
                        : 5
                );

            var requiredWithoutMirror =
                mirrorIndex;

            var insertionIndex =
                section.KeywordLineIndex +
                1 +
                section.RawBodyLines.Count;

            while (
                dataIndices.Count <
                requiredWithoutMirror)
            {
                lines.Insert(
                    insertionIndex,
                    "0");

                dataIndices.Add(
                    insertionIndex);

                insertionIndex++;
            }

            lines[
                dataIndices[
                    cantStartIndex]] =
                Format(
                    edit.CantStart);

            lines[
                dataIndices[
                    cantEndIndex]] =
                Format(
                    edit.CantEnd);

            if (edit.IsMirrored)
            {
                if (
                    dataIndices.Count <=
                    mirrorIndex)
                {
                    lines.Insert(
                        insertionIndex,
                        "mirror");

                    dataIndices.Add(
                        insertionIndex);
                }
                else
                {
                    var current =
                        lines[
                            dataIndices[
                                mirrorIndex]]
                            .Trim();

                    if (
                        current.Length > 0 &&
                        !string.Equals(
                            current,
                            "mirror",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException(
                            "splineAdvancedLayoutUnknown");
                    }

                    lines[
                        dataIndices[
                            mirrorIndex]] =
                        "mirror";
                }
            }
            else if (
                dataIndices.Count >
                    mirrorIndex &&
                string.Equals(
                    lines[
                        dataIndices[
                            mirrorIndex]]
                        .Trim(),
                    "mirror",
                    StringComparison.OrdinalIgnoreCase))
            {
                lines[
                    dataIndices[
                        mirrorIndex]] =
                    string.Empty;
            }

            applied++;
        }

        return new OmsiTileSplineEditResult(
            Encode(
                document,
                lines),
            applied);
    }

    private static string Format(
        double value) =>
        value.ToString(
            "G17",
            CultureInfo.InvariantCulture);

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
