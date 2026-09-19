using System.Globalization;
using MapStudio.Core.Omsi.Config;

namespace MapStudio.Core.Omsi.Maps;

public static class OmsiTileSplineInserter
{
    public static OmsiTileSplineInsertResult Append(
        OmsiConfigDocument document,
        OmsiNewPlacedSpline placedSpline)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(placedSpline);

        if (
            string.IsNullOrWhiteSpace(
                placedSpline.HeaderValue) ||
            string.IsNullOrWhiteSpace(
                placedSpline.SplinePath) ||
            placedSpline.SplineId <= 0 ||
            !IsFinite(placedSpline))
        {
            throw new InvalidDataException(
                "invalidNewSpline");
        }

        var lines =
            document.Lines.ToList();

        if (
            lines.Count > 0 &&
            lines[^1].Length != 0)
        {
            lines.Add(string.Empty);
        }

        var sourceSectionOrdinal =
            document.Sections.Count(
                section =>
                    string.Equals(
                        section.Keyword,
                        "spline",
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        section.Keyword,
                        "spline_h",
                        StringComparison.OrdinalIgnoreCase));

        lines.Add(
            placedSpline.IsHeightSpline
                ? "[spline_h]"
                : "[spline]");
        lines.Add(
            placedSpline.HeaderValue);
        lines.Add(
            placedSpline.SplinePath);
        lines.Add(
            placedSpline.SplineId
                .ToString(
                    CultureInfo.InvariantCulture));
        lines.Add(
            placedSpline.PreviousSplineId
                .ToString(
                    CultureInfo.InvariantCulture));
        lines.Add(
            placedSpline.NextSplineId
                .ToString(
                    CultureInfo.InvariantCulture));
        lines.Add(Format(placedSpline.X));
        lines.Add(Format(placedSpline.Z));
        lines.Add(Format(placedSpline.Y));
        lines.Add(
            Format(
                placedSpline.Rotation));
        lines.Add(
            Format(
                placedSpline.Length));
        lines.Add(
            Format(
                placedSpline.Radius));
        lines.Add(
            Format(
                placedSpline.GradientStart));
        lines.Add(
            Format(
                placedSpline.GradientEnd));

        foreach (var extra in
            placedSpline.ExtraValues)
        {
            lines.Add(extra);
        }

        return new OmsiTileSplineInsertResult(
            Encode(document, lines),
            sourceSectionOrdinal);
    }

    private static bool IsFinite(
        OmsiNewPlacedSpline value) =>
        double.IsFinite(value.X) &&
        double.IsFinite(value.Z) &&
        double.IsFinite(value.Y) &&
        double.IsFinite(
            value.Rotation) &&
        double.IsFinite(value.Length) &&
        value.Length >= 0 &&
        double.IsFinite(value.Radius) &&
        double.IsFinite(
            value.GradientStart) &&
        double.IsFinite(
            value.GradientEnd);

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
