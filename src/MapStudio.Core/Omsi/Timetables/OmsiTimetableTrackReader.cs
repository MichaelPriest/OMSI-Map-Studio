using System.Globalization;
using System.Text;

namespace MapStudio.Core.Omsi.Timetables;

public sealed class OmsiTimetableTrackReader
{
    public async Task<OmsiTimetableTrack> ReadAsync(
        string mapDirectory,
        string filePath,
        CancellationToken cancellationToken =
            default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            mapDirectory);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            filePath);

        var lines =
            await File.ReadAllLinesAsync(
                filePath,
                Encoding.Latin1,
                cancellationToken)
                .ConfigureAwait(false);

        var firstEntry =
            Array.FindIndex(
                lines,
                line =>
                    string.Equals(
                        line.Trim(),
                        "[track_entry]",
                        StringComparison.OrdinalIgnoreCase));

        var comments =
            lines
                .Take(
                    firstEntry >= 0
                        ? firstEntry
                        : lines.Length)
                .Where(
                    line =>
                        !string.IsNullOrWhiteSpace(
                            line) &&
                        !line.StartsWith(
                            "-",
                            StringComparison.Ordinal) &&
                        !string.Equals(
                            line.Trim(),
                            "Time Table Track File",
                            StringComparison.OrdinalIgnoreCase))
                .TakeLast(2)
                .ToArray();

        var entries =
            new List<OmsiTimetableTrackEntry>();

        for (
            var index = 0;
            index < lines.Length;
            index++)
        {
            if (!string.Equals(
                    lines[index].Trim(),
                    "[track_entry]",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var data =
                ReadFollowingDataLines(
                    lines,
                    index + 1,
                    7);

            if (
                data.Count < 6 ||
                !int.TryParse(
                    data[0],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var id) ||
                !int.TryParse(
                    data[2],
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
                    data[4],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var parsedLength) &&
                double.IsFinite(
                    parsedLength))
            {
                length =
                    parsedLength;
            }

            entries.Add(
                new OmsiTimetableTrackEntry(
                    FindPreviousNonEmptyLine(
                        lines,
                        index - 1),
                    id,
                    data[1],
                    tileIndex,
                    data[3],
                    length,
                    data[5],
                    data.Count > 6
                        ? data[6]
                        : null));
        }

        var fullMapDirectory =
            Path.GetFullPath(
                mapDirectory);

        var fullFilePath =
            Path.GetFullPath(
                filePath);

        return new OmsiTimetableTrack(
            fullFilePath,
            Path.GetRelativePath(
                fullMapDirectory,
                fullFilePath),
            Path.GetFileNameWithoutExtension(
                fullFilePath),
            comments.ElementAtOrDefault(0) ??
                string.Empty,
            comments.ElementAtOrDefault(1) ??
                string.Empty,
            entries);
    }

    private static List<string>
        ReadFollowingDataLines(
            IReadOnlyList<string> lines,
            int start,
            int maximum)
    {
        var result =
            new List<string>(
                maximum);

        for (
            var index = start;
            index < lines.Count &&
            result.Count < maximum;
            index++)
        {
            var value =
                lines[index].Trim();

            if (value.StartsWith(
                    "[",
                    StringComparison.Ordinal))
            {
                break;
            }

            if (
                string.IsNullOrWhiteSpace(
                    value))
            {
                if (result.Count > 0)
                {
                    break;
                }

                continue;
            }

            result.Add(value);
        }

        return result;
    }

    private static string
        FindPreviousNonEmptyLine(
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

            if (
                !string.IsNullOrWhiteSpace(
                    value) &&
                !value.StartsWith(
                    "[",
                    StringComparison.Ordinal))
            {
                return value;
            }
        }

        return string.Empty;
    }
}
