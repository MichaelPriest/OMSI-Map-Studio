using System.Globalization;
using System.Text;

namespace MapStudio.Core.Omsi.Timetables;

public sealed class OmsiTimetableTrackWriter
{
    public byte[] Write(
        OmsiTimetableTrack source,
        IReadOnlyList<
            OmsiTimetableTrackEntry>
            entries,
        string newLine = "\r\n")
    {
        ArgumentNullException.ThrowIfNull(
            source);

        ArgumentNullException.ThrowIfNull(
            entries);

        if (
            entries.Count >
            100000)
        {
            throw new InvalidDataException(
                "trackEntryCountTooLarge");
        }

        if (
            string.IsNullOrEmpty(
                newLine))
        {
            newLine =
                "\r\n";
        }

        var lines =
            new List<string>
            {
                "-----------------------",
                "Time Table Track File",
                "-----------------------",
                string.Empty
            };

        if (
            !string.IsNullOrWhiteSpace(
                source.Comment1))
        {
            lines.Add(
                source.Comment1);
        }

        if (
            !string.IsNullOrWhiteSpace(
                source.Comment2))
        {
            lines.Add(
                source.Comment2);
        }

        if (
            lines[^1].Length !=
                0)
        {
            lines.Add(
                string.Empty);
        }

        for (
            var index = 0;
            index < entries.Count;
            index++)
        {
            var entry =
                entries[index];

            if (entry.Id < 0)
            {
                throw new InvalidDataException(
                    "trackEntryIdInvalid");
            }

            var line2 =
                entry.Line2
                    .Trim();

            if (
                string.IsNullOrWhiteSpace(
                    line2))
            {
                throw new InvalidDataException(
                    "trackEntryPathIndexMissing");
            }

            var comment =
                string.IsNullOrWhiteSpace(
                    entry.Comment)
                    ? $"{index}:"
                    : entry.Comment.Trim();

            lines.Add(
                comment);

            lines.Add(
                "[track_entry]");

            lines.Add(
                entry.Id.ToString(
                    CultureInfo.InvariantCulture));

            lines.Add(
                line2);

            var extended =
                entry.TileIndex >=
                    0 ||
                !string.IsNullOrWhiteSpace(
                    entry.Line4) ||
                entry.Length.HasValue ||
                !string.IsNullOrWhiteSpace(
                    entry.Line6) ||
                entry.Line7 is not
                    null;

            if (extended)
            {
                lines.Add(
                    entry.TileIndex.ToString(
                        CultureInfo.InvariantCulture));

                lines.Add(
                    string.IsNullOrWhiteSpace(
                        entry.Line4)
                        ? "0"
                        : entry.Line4.Trim());

                lines.Add(
                    entry.Length
                        ?.ToString(
                            "G17",
                            CultureInfo.InvariantCulture) ??
                    "0");

                lines.Add(
                    string.IsNullOrWhiteSpace(
                        entry.Line6)
                        ? "0"
                        : entry.Line6.Trim());

                if (
                    entry.Line7 is not
                        null)
                {
                    lines.Add(
                        entry.Line7.Trim());
                }
            }

            lines.Add(
                string.Empty);
        }

        var text =
            string.Join(
                newLine,
                lines);

        if (
            !text.EndsWith(
                newLine,
                StringComparison.Ordinal))
        {
            text +=
                newLine;
        }

        return Encoding.Latin1
            .GetBytes(
                text);
    }
}
