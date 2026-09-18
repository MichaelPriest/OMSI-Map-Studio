namespace MapStudio.Core.Omsi.Config;

public static class OmsiConfigParser
{
    public static OmsiConfigDocument Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

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

        return new OmsiConfigDocument(lines, sections, newLine, hasTrailingNewLine);
    }

    public static async Task<OmsiConfigDocument> ParseFileAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Parse(await File.ReadAllTextAsync(path, cancellationToken));
    }
}
