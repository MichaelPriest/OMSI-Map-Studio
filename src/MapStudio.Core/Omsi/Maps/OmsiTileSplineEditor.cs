using System.Globalization;
using MapStudio.Core.Omsi.Config;

namespace MapStudio.Core.Omsi.Maps;

public static class OmsiTileSplineEditor
{
    public static OmsiTileSplineEditResult ApplyTransforms(
        OmsiConfigDocument document,
        IReadOnlyList<OmsiSplineTransformEdit> edits)
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
                    splineSections.Length ||
                !usedOrdinals.Add(
                    edit.SourceSectionOrdinal) ||
                !IsFinite(edit))
            {
                throw new InvalidDataException(
                    "invalidSplineSection");
            }

            var section =
                splineSections[
                    edit.SourceSectionOrdinal];

            var dataLineIndices =
                GetDataLineIndices(
                    section);

            if (dataLineIndices.Count < 13)
            {
                throw new InvalidDataException(
                    "malformedSplineSection");
            }

            var dataValues =
                dataLineIndices
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
                    dataValues[2],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var sourceSplineId) ||
                !int.TryParse(
                    dataValues[3],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var previousSplineId) ||
                !int.TryParse(
                    dataValues[4],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var nextSplineId) ||
                sourceSplineId !=
                    edit.SplineId ||
                previousSplineId !=
                    edit.PreviousSplineId ||
                nextSplineId !=
                    edit.NextSplineId ||
                !string.Equals(
                    dataValues[1],
                    edit.SplinePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "splineSourceChanged");
            }

            lines[dataLineIndices[5]] =
                Format(edit.X);
            lines[dataLineIndices[6]] =
                Format(edit.Z);
            lines[dataLineIndices[7]] =
                Format(edit.Y);
            lines[dataLineIndices[8]] =
                Format(edit.Rotation);
            lines[dataLineIndices[9]] =
                Format(edit.Length);
            lines[dataLineIndices[10]] =
                Format(edit.Radius);
            lines[dataLineIndices[11]] =
                Format(
                    edit.GradientStart);
            lines[dataLineIndices[12]] =
                Format(
                    edit.GradientEnd);

            applied++;
        }

        return new OmsiTileSplineEditResult(
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

    private static bool IsFinite(
        OmsiSplineTransformEdit edit) =>
        double.IsFinite(edit.X) &&
        double.IsFinite(edit.Z) &&
        double.IsFinite(edit.Y) &&
        double.IsFinite(
            edit.Rotation) &&
        double.IsFinite(edit.Length) &&
        double.IsFinite(edit.Radius) &&
        double.IsFinite(
            edit.GradientStart) &&
        double.IsFinite(
            edit.GradientEnd);

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
