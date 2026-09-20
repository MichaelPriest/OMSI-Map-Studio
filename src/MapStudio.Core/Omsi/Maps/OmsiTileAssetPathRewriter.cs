using MapStudio.Core.Omsi.Config;

namespace MapStudio.Core.Omsi.Maps;

public static class OmsiTileAssetPathRewriter
{
    public static OmsiTileAssetPathRewriteResult Replace(
        OmsiConfigDocument document,
        string oldPath,
        string newPath,
        bool replaceObjects,
        bool replaceSplines)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(oldPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(newPath);

        if (
            !replaceObjects &&
            !replaceSplines)
        {
            throw new ArgumentException(
                "No asset category selected.");
        }

        var lines =
            document.Lines.ToArray();
        var objectReplacements = 0;
        var splineReplacements = 0;

        if (replaceObjects)
        {
            foreach (var section in
                document.FindSections("object"))
            {
                var indices =
                    GetDataLineIndices(
                        section);

                if (indices.Count < 2)
                {
                    continue;
                }

                var pathIndex =
                    indices[1];

                if (
                    string.Equals(
                        lines[pathIndex].Trim(),
                        oldPath,
                        StringComparison
                            .OrdinalIgnoreCase))
                {
                    lines[pathIndex] =
                        newPath;
                    objectReplacements += 1;
                }
            }
        }

        if (replaceSplines)
        {
            var version =
                OmsiSplineFieldLayout
                    .ReadVersion(document);

            foreach (var section in
                document.Sections.Where(
                    OmsiSplineFieldLayout
                        .IsSplineSection))
            {
                var indices =
                    OmsiSplineFieldLayout
                        .GetDataLineIndices(
                            section);

                if (
                    !OmsiSplineFieldLayout
                        .TryCreate(
                            version,
                            indices.Count,
                            out var layout))
                {
                    continue;
                }

                var pathIndex =
                    indices[
                        layout.PathIndex];

                if (
                    string.Equals(
                        lines[pathIndex].Trim(),
                        oldPath,
                        StringComparison
                            .OrdinalIgnoreCase))
                {
                    lines[pathIndex] =
                        newPath;
                    splineReplacements += 1;
                }
            }
        }

        return new OmsiTileAssetPathRewriteResult(
            Encode(document, lines),
            objectReplacements,
            splineReplacements);
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
