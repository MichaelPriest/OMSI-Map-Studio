using System.Globalization;
using MapStudio.Core.Omsi.Config;

namespace MapStudio.Core.Omsi.Maps;

public static class OmsiTileObjectEditor
{
    public static OmsiTileObjectEditResult ApplyTransforms(
        OmsiConfigDocument document,
        IReadOnlyList<OmsiObjectTransformEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(edits);

        if (edits.Count == 0)
        {
            return new OmsiTileObjectEditResult(
                document.ToBytes(),
                0);
        }

        var lines =
            document.Lines.ToArray();

        var objectSections =
            document.FindSections("object")
                .ToArray();

        var applied = 0;
        var usedOrdinals =
            new HashSet<int>();

        foreach (var edit in edits)
        {
            if (
                edit.SourceSectionOrdinal < 0 ||
                edit.SourceSectionOrdinal >=
                    objectSections.Length ||
                !usedOrdinals.Add(
                    edit.SourceSectionOrdinal))
            {
                throw new InvalidDataException(
                    "invalidObjectSection");
            }

            var section =
                objectSections[
                    edit.SourceSectionOrdinal];

            var dataLineIndices =
                GetDataLineIndices(
                    section);

            if (dataLineIndices.Count < 9)
            {
                throw new InvalidDataException(
                    "malformedObjectSection");
            }

            var dataValues =
                dataLineIndices
                    .Select(index =>
                        lines[index].Trim())
                    .ToArray();

            if (
                !int.TryParse(
                    dataValues[2],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var sourceObjectId) ||
                sourceObjectId !=
                    edit.ObjectId ||
                !string.Equals(
                    dataValues[1],
                    edit.SceneryObjectPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "objectSourceChanged");
            }

            lines[dataLineIndices[3]] =
                Format(edit.X);
            lines[dataLineIndices[4]] =
                Format(edit.Y);
            lines[dataLineIndices[5]] =
                Format(edit.Z);
            lines[dataLineIndices[6]] =
                Format(edit.Rotation);
            lines[dataLineIndices[7]] =
                Format(edit.Pitch);
            lines[dataLineIndices[8]] =
                Format(edit.Bank);

            applied++;
        }

        return new OmsiTileObjectEditResult(
            Encode(
                document,
                lines),
            applied);
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
                value.StartsWith(
                    '#'))
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
