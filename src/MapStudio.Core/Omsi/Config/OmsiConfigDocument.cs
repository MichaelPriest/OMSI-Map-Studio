using System.Text;

namespace MapStudio.Core.Omsi.Config;

public sealed class OmsiConfigDocument
{
    public OmsiConfigDocument(
        IReadOnlyList<string> lines,
        IReadOnlyList<OmsiConfigSection> sections,
        string newLine,
        bool hasTrailingNewLine,
        Encoding textEncoding,
        bool hasByteOrderMark)
    {
        Lines = lines;
        Sections = sections;
        NewLine = newLine;
        HasTrailingNewLine = hasTrailingNewLine;
        TextEncoding = textEncoding;
        HasByteOrderMark = hasByteOrderMark;
    }

    public IReadOnlyList<string> Lines { get; }
    public IReadOnlyList<OmsiConfigSection> Sections { get; }
    public string NewLine { get; }
    public bool HasTrailingNewLine { get; }
    public Encoding TextEncoding { get; }
    public bool HasByteOrderMark { get; }

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

    public byte[] ToBytes()
    {
        var content = TextEncoding.GetBytes(ToText());

        if (!HasByteOrderMark)
        {
            return content;
        }

        var preamble = TextEncoding.GetPreamble();

        if (preamble.Length == 0)
        {
            return content;
        }

        var result = new byte[preamble.Length + content.Length];
        Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
        Buffer.BlockCopy(content, 0, result, preamble.Length, content.Length);
        return result;
    }
}
