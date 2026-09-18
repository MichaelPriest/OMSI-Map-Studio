namespace MapStudio.Core.Omsi.Config;

public sealed class OmsiConfigDocument
{
    public OmsiConfigDocument(
        IReadOnlyList<string> lines,
        IReadOnlyList<OmsiConfigSection> sections,
        string newLine,
        bool hasTrailingNewLine)
    {
        Lines = lines;
        Sections = sections;
        NewLine = newLine;
        HasTrailingNewLine = hasTrailingNewLine;
    }

    public IReadOnlyList<string> Lines { get; }
    public IReadOnlyList<OmsiConfigSection> Sections { get; }
    public string NewLine { get; }
    public bool HasTrailingNewLine { get; }

    public IEnumerable<OmsiConfigSection> FindSections(string keyword) =>
        Sections.Where(section =>
            string.Equals(section.Keyword, keyword, StringComparison.OrdinalIgnoreCase));

    public OmsiConfigSection? FindFirstSection(string keyword) =>
        FindSections(keyword).FirstOrDefault();

    public string ToText()
    {
        var text = string.Join(NewLine, Lines);
        return HasTrailingNewLine && Lines.Count > 0 ? text + NewLine : text;
    }
}
