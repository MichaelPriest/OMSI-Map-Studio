using System.Globalization;
using System.Text;

namespace MapStudio.Core.Omsi.Timetables;

public sealed class OmsiStationLinkWriter
{
    public byte[] Write(
        IReadOnlyList<OmsiStationLink> links,
        string newLine = "\r\n")
    {
        ArgumentNullException.ThrowIfNull(
            links);

        if (
            links.Count >
            100000)
        {
            throw new InvalidDataException(
                "stationLinkCountTooLarge");
        }

        if (string.IsNullOrEmpty(
                newLine))
        {
            newLine =
                "\r\n";
        }

        var lines =
            new List<string>
            {
                "---------------------------",
                "Time Table StnLinkList File",
                "---------------------------",
                string.Empty,
                "Edited with OMSI Map Studio",
                string.Empty
            };

        foreach (
            var link in
                links)
        {
            if (
                link.StartBusStopId <
                    0 ||
                link.EndBusStopId <
                    0 ||
                link.Entries.Count >
                    100000)
            {
                throw new InvalidDataException(
                    "stationLinkInvalid");
            }

            if (
                !string.IsNullOrWhiteSpace(
                    link.Comment))
            {
                lines.Add(
                    link.Comment.Trim());

                lines.Add(
                    string.Empty);
            }

            lines.Add(
                "[StnLink]");

            lines.Add(
                link.Line1 ??
                string.Empty);

            lines.Add(
                link.StartBusStopId
                    .ToString(
                        CultureInfo
                            .InvariantCulture));

            lines.Add(
                link.EndBusStopId
                    .ToString(
                        CultureInfo
                            .InvariantCulture));

            lines.Add(
                link.Line4 ??
                string.Empty);

            lines.Add(
                link.Line5 ??
                string.Empty);

            lines.Add(
                link.Line6 ??
                string.Empty);

            lines.Add(
                link.Line7 ??
                string.Empty);

            lines.Add(
                link.Line8 ??
                string.Empty);

            lines.Add(
                link.Line9 ??
                string.Empty);

            lines.Add(
                string.Empty);

            for (
                var index = 0;
                index < link.Entries.Count;
                index++)
            {
                var entry =
                    link.Entries[index];

                if (
                    entry.Id < 0 ||
                    entry.TileIndex <
                        -1)
                {
                    throw new InvalidDataException(
                        "stationLinkEntryInvalid");
                }

                lines.Add(
                    string.IsNullOrWhiteSpace(
                        entry.Comment)
                        ? $"   {index}:"
                        : entry.Comment.Trim());

                lines.Add(
                    "[StnLink_entry]");

                lines.Add(
                    entry.Id.ToString(
                        CultureInfo
                            .InvariantCulture));

                lines.Add(
                    entry.Line2 ??
                    string.Empty);

                lines.Add(
                    entry.TileIndex
                        .ToString(
                            CultureInfo
                                .InvariantCulture));

                lines.Add(
                    entry.Length
                        ?.ToString(
                            "G17",
                            CultureInfo
                                .InvariantCulture) ??
                    "0");

                lines.Add(
                    entry.Line5 ??
                    string.Empty);

                lines.Add(
                    entry.Line6 ??
                    string.Empty);

                lines.Add(
                    entry.Line7 ??
                    string.Empty);

                foreach (
                    var chrono in
                        entry.ChronoFiles)
                {
                    if (
                        !string.IsNullOrWhiteSpace(
                            chrono))
                    {
                        lines.Add(
                            chrono.Trim());
                    }
                }

                lines.Add(
                    string.Empty);
            }
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
