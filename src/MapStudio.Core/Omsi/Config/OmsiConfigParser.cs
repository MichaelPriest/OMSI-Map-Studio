using System.Text;

namespace MapStudio.Core.Omsi.Config;

public static class OmsiConfigParser
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    static OmsiConfigParser()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static OmsiConfigDocument Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return ParseText(text, new UTF8Encoding(false), hasByteOrderMark: false);
    }

    public static OmsiConfigDocument ParseBytes(ReadOnlySpan<byte> bytes)
    {
        var (encoding, hasBom, preambleLength) = DetectEncoding(bytes);
        var text = encoding.GetString(bytes[preambleLength..]);
        return ParseText(text, encoding, hasBom);
    }

    public static async Task<OmsiConfigDocument> ParseFileAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        return ParseBytes(bytes);
    }

    private static OmsiConfigDocument ParseText(
        string text,
        Encoding encoding,
        bool hasByteOrderMark)
    {
        var newLine = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var hasTrailingNewLine =
            text.EndsWith("\r\n", StringComparison.Ordinal) ||
            text.EndsWith("\n", StringComparison.Ordinal);

        var lines = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .ToList();

        if (hasTrailingNewLine && lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        var keywords = new List<(int Index, string Keyword)>();

        for (var index = 0; index < lines.Count; index++)
        {
            var value = lines[index].Trim();

            if (value.Length >= 3 &&
                value[0] == '[' &&
                value[^1] == ']' &&
                value.IndexOf(']') == value.Length - 1)
            {
                keywords.Add((index, value[1..^1]));
            }
        }

        var sections = new List<OmsiConfigSection>();

        for (var index = 0; index < keywords.Count; index++)
        {
            var current = keywords[index];
            var bodyStart = current.Index + 1;
            var bodyEnd = index + 1 < keywords.Count ? keywords[index + 1].Index : lines.Count;

            sections.Add(new OmsiConfigSection(
                current.Keyword,
                current.Index,
                lines.Skip(bodyStart).Take(bodyEnd - bodyStart).ToArray()));
        }

        return new OmsiConfigDocument(
            lines,
            sections,
            newLine,
            hasTrailingNewLine,
            encoding,
            hasByteOrderMark);
    }

    private static (Encoding Encoding, bool HasBom, int PreambleLength) DetectEncoding(
        ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }))
        {
            return (new UTF8Encoding(true, true), true, 3);
        }

        if (bytes.StartsWith(new byte[] { 0xFF, 0xFE }))
        {
            return (new UnicodeEncoding(false, true, true), true, 2);
        }

        if (bytes.StartsWith(new byte[] { 0xFE, 0xFF }))
        {
            return (new UnicodeEncoding(true, true, true), true, 2);
        }

        try
        {
            _ = StrictUtf8.GetString(bytes);
            return (new UTF8Encoding(false, true), false, 0);
        }
        catch (DecoderFallbackException)
        {
            return (
                Encoding.GetEncoding(
                    1252,
                    EncoderFallback.ExceptionFallback,
                    DecoderFallback.ExceptionFallback),
                false,
                0);
        }
    }
}
