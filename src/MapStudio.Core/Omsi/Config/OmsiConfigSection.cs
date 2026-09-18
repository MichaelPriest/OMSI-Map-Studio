namespace MapStudio.Core.Omsi.Config;

public sealed record OmsiConfigSection(
    string Keyword,
    int KeywordLineIndex,
    IReadOnlyList<string> RawBodyLines)
{
    public IEnumerable<string> DataLines =>
        RawBodyLines
            .Select(static line => line.Trim())
            .Where(static line => line.Length > 0 && !line.StartsWith('#'));
}
