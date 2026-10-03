using MapStudio.Core.Omsi.Config;

namespace MapStudio.Core.Omsi.Maps;

public static class OmsiTileWaterMarkerEditor
{
    public static byte[] EnsurePresent(
        OmsiConfigDocument document)
    {
        ArgumentNullException.ThrowIfNull(
            document);

        if (
            document.FindFirstSection(
                "water") is not null)
        {
            return document.ToBytes();
        }

        var lines =
            document.Lines
                .ToList();

        if (
            lines.Count >
                0 &&
            lines[^1].Length !=
                0)
        {
            lines.Add(
                string.Empty);
        }

        lines.Add(
            "[water]");

        lines.Add(
            string.Empty);

        return Encode(
            document,
            lines);
    }

    public static byte[] Remove(
        OmsiConfigDocument document)
    {
        ArgumentNullException.ThrowIfNull(
            document);

        var section =
            document.FindFirstSection(
                "water");

        if (section is null)
        {
            return document.ToBytes();
        }

        var start =
            section
                .KeywordLineIndex;

        var count =
            1 +
            section
                .RawBodyLines
                .Count;

        var lines =
            document.Lines
                .Where(
                    (_, index) =>
                        index <
                            start ||
                        index >=
                            start +
                            count)
                .ToList();

        while (
            lines.Count >
                1 &&
            lines[^1].Length ==
                0 &&
            lines[^2].Length ==
                0)
        {
            lines.RemoveAt(
                lines.Count -
                    1);
        }

        return Encode(
            document,
            lines);
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
            lines.Count >
                0)
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

        if (preamble.Length ==
            0)
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
