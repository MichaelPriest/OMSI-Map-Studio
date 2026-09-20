using System.Globalization;
using MapStudio.Core.Omsi.Config;

namespace MapStudio.Core.Omsi.Maps;

public static class OmsiTileObjectDeleter
{
    public static OmsiTileObjectDeleteResult Remove(
        OmsiConfigDocument document,
        int sourceSectionOrdinal,
        string sceneryObjectPath,
        int objectId)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            sceneryObjectPath);

        var objectSections =
            document.FindSections("object")
                .ToArray();

        if (
            sourceSectionOrdinal < 0 ||
            sourceSectionOrdinal >=
                objectSections.Length)
        {
            throw new InvalidDataException(
                "invalidObjectSection");
        }

        var section =
            objectSections[
                sourceSectionOrdinal];

        var dataLineIndices =
            GetDataLineIndices(section);

        if (dataLineIndices.Count < 9)
        {
            throw new InvalidDataException(
                "malformedObjectSection");
        }

        var lines =
            document.Lines;

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
            sourceObjectId != objectId ||
            !string.Equals(
                dataValues[1],
                sceneryObjectPath,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "objectSourceChanged");
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

        return new OmsiTileObjectDeleteResult(
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
