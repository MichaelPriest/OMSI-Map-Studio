using System.Globalization;
using System.Text;

namespace MapStudio.Core.Omsi.Timetables;

public sealed class OmsiStationLinkReader
{
    public async Task<IReadOnlyList<
        OmsiStationLink>> ReadAsync(
        string filePath,
        CancellationToken cancellationToken =
            default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            filePath);

        var lines =
            await File.ReadAllLinesAsync(
                filePath,
                Encoding.Latin1,
                cancellationToken)
                .ConfigureAwait(false);

        var result =
            new List<OmsiStationLink>();

        StationLinkBuilder?
            current =
                null;

        for (
            var index = 0;
            index < lines.Length;
            index++)
        {
            var keyword =
                lines[index].Trim();

            if (string.Equals(
                    keyword,
                    "[StnLink]",
                    StringComparison.OrdinalIgnoreCase))
            {
                var data =
                    ReadFixedFollowingLines(
                        lines,
                        index + 1,
                        9);

                if (
                    data is null ||
                    !int.TryParse(
                        data[1],
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var startId) ||
                    !int.TryParse(
                        data[2],
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var endId))
                {
                    current =
                        null;

                    continue;
                }

                current =
                    new StationLinkBuilder(
                        FindPreviousComment(
                            lines,
                            index - 1),
                        data[0],
                        startId,
                        endId,
                        data[3],
                        data[4],
                        data[5],
                        data[6],
                        data[7],
                        data[8]);

                result.Add(
                    current.Link);

                continue;
            }

            if (
                current is null ||
                !string.Equals(
                    keyword,
                    "[StnLink_entry]",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var values =
                ReadFixedFollowingLines(
                    lines,
                    index + 1,
                    7);

            if (
                values is null ||
                !int.TryParse(
                    values[0],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var id) ||
                !int.TryParse(
                    values[2],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var tileIndex))
            {
                continue;
            }

            double? length =
                null;

            if (
                double.TryParse(
                    values[3],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var parsedLength) &&
                double.IsFinite(
                    parsedLength))
            {
                length =
                    parsedLength;
            }

            var chronoFiles =
                ReadChronoLines(
                    lines,
                    index + 8);

            current.Entries.Add(
                new OmsiStationLinkEntry(
                    FindPreviousComment(
                        lines,
                        index - 1),
                    id,
                    values[1],
                    tileIndex,
                    length,
                    values[4],
                    values[5],
                    values[6],
                    chronoFiles));
        }

        return result;
    }

    private static string[]?
        ReadFixedFollowingLines(
            IReadOnlyList<string> lines,
            int start,
            int count)
    {
        if (
            start < 0 ||
            start + count >
                lines.Count)
        {
            return null;
        }

        return lines
            .Skip(start)
            .Take(count)
            .Select(
                value =>
                    value.Trim())
            .ToArray();
    }

    private static IReadOnlyList<string>
        ReadChronoLines(
            IReadOnlyList<string> lines,
            int start)
    {
        var result =
            new List<string>();

        for (
            var index = start;
            index < lines.Count;
            index++)
        {
            var value =
                lines[index].Trim();

            if (
                string.IsNullOrWhiteSpace(
                    value) ||
                value.StartsWith(
                    "[",
                    StringComparison.Ordinal))
            {
                break;
            }

            result.Add(
                value);
        }

        return result;
    }

    private static string FindPreviousComment(
        IReadOnlyList<string> lines,
        int start)
    {
        for (
            var index = start;
            index >= 0;
            index--)
        {
            var value =
                lines[index].Trim();

            if (string.IsNullOrWhiteSpace(
                    value))
            {
                continue;
            }

            if (
                value.StartsWith(
                    "[",
                    StringComparison.Ordinal) ||
                value.StartsWith(
                    "-",
                    StringComparison.Ordinal) ||
                string.Equals(
                    value,
                    "Time Table StnLinkList File",
                    StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return value;
        }

        return string.Empty;
    }

    private sealed class StationLinkBuilder
    {
        public StationLinkBuilder(
            string comment,
            string line1,
            int startId,
            int endId,
            string line4,
            string line5,
            string line6,
            string line7,
            string line8,
            string line9)
        {
            Link =
                new OmsiStationLink(
                    comment,
                    line1,
                    startId,
                    endId,
                    line4,
                    line5,
                    line6,
                    line7,
                    line8,
                    line9,
                    Entries);
        }

        public List<OmsiStationLinkEntry>
            Entries { get; } = [];

        public OmsiStationLink Link
        { get; }
    }
}
