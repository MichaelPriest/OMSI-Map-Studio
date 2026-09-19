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
                OmsiSplineFieldLayout
                    .GetDataLineIndices(
                        section);

            if (
                !OmsiSplineFieldLayout.TryCreate(
                    version,
                    dataLineIndices.Count,
                    out var layout))
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

            var sourceNextSplineId = -1;

            if (
                layout.NextIndex is int nextIndex &&
                !int.TryParse(
                    dataValues[nextIndex],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out sourceNextSplineId))
            {
                throw new InvalidDataException(
                    "splineSourceChanged");
            }

            if (
                isHeightSpline !=
                    edit.IsHeightSpline ||
                !int.TryParse(
                    dataValues[layout.IdIndex],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var sourceSplineId) ||
                !int.TryParse(
                    dataValues[layout.PreviousIndex],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var previousSplineId) ||
                sourceSplineId !=
                    edit.SplineId ||
                previousSplineId !=
                    edit.PreviousSplineId ||
                sourceNextSplineId !=
                    edit.NextSplineId ||
                !string.Equals(
                    dataValues[layout.PathIndex],
                    edit.SplinePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "splineSourceChanged");
            }

            lines[dataLineIndices[layout.XIndex]] =
                Format(edit.X);
            lines[dataLineIndices[layout.ZIndex]] =
                Format(edit.Z);
            lines[dataLineIndices[layout.YIndex]] =
                Format(edit.Y);
            lines[dataLineIndices[layout.RotationIndex]] =
                Format(edit.Rotation);
            lines[dataLineIndices[layout.LengthIndex]] =
                Format(edit.Length);
            lines[dataLineIndices[layout.RadiusIndex]] =
                Format(edit.Radius);
            lines[dataLineIndices[layout.GradientStartIndex]] =
                Format(
                    edit.GradientStart);
            lines[dataLineIndices[layout.GradientEndIndex]] =
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
