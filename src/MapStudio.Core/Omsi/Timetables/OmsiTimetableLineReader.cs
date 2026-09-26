using System.Text;

namespace MapStudio.Core.Omsi.Timetables;

public sealed class OmsiTimetableLineReader
{
    public async Task<OmsiTimetableLine> ReadAsync(
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

        var priorityIndex =
            Array.FindIndex(
                lines,
                line =>
                    string.Equals(
                        line.Trim(),
                        "[priority]",
                        StringComparison.OrdinalIgnoreCase));

        if (
            priorityIndex < 0 ||
            priorityIndex + 1 >=
                lines.Length)
        {
            throw new InvalidDataException(
                "timetablePriorityMissing");
        }

        var comments =
            lines
                .Take(priorityIndex)
                .Where(
                    line =>
                        !string.IsNullOrWhiteSpace(
                            line) &&
                        !line.StartsWith(
                            "-",
                            StringComparison.Ordinal) &&
                        !line.StartsWith(
                            "[",
                            StringComparison.Ordinal) &&
                        !string.Equals(
                            line.Trim(),
                            "Time Table Line File",
                            StringComparison.OrdinalIgnoreCase))
                .TakeLast(2)
                .ToArray();

        var userAllowed =
            lines
                .Take(priorityIndex)
                .Any(
                    line =>
                        string.Equals(
                            line.Trim(),
                            "[userallowed]",
                            StringComparison.OrdinalIgnoreCase));

        var tourBuilders =
            new List<TourBuilder>();

        TourBuilder? current =
            null;

        for (
            var index =
                priorityIndex + 2;
            index < lines.Length;
            index++)
        {
            var keyword =
                lines[index].Trim();

            if (string.Equals(
                    keyword,
                    "[newtour]",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (
                    index + 3 >=
                        lines.Length)
                {
                    continue;
                }

                var name =
                    lines[index + 1]
                        .Trim();

                var aiGroup =
                    lines[index + 2]
                        .Trim();

                var line3 =
                    lines[index + 3]
                        .Trim();

                if (string.IsNullOrWhiteSpace(
                        name))
                {
                    current =
                        null;

                    continue;
                }

                current =
                    new TourBuilder(
                        name,
                        aiGroup,
                        line3);

                tourBuilders.Add(
                    current);

                continue;
            }

            if (
                current is null ||
                !string.Equals(
                    keyword,
                    "[addtrip]",
                    StringComparison.OrdinalIgnoreCase) ||
                index + 3 >=
                    lines.Length)
            {
                continue;
            }

            var tripName =
                lines[index + 1]
                    .Trim();

            if (string.IsNullOrWhiteSpace(
                    tripName))
            {
                continue;
            }

            current.Trips.Add(
                new OmsiTimetableAddTrip(
                    FindPreviousComment(
                        lines,
                        index - 1),
                    tripName,
                    lines[index + 2]
                        .Trim(),
                    lines[index + 3]
                        .Trim()));
        }

        var fullMapDirectory =
            Path.GetFullPath(
                mapDirectory);

        var fullFilePath =
            Path.GetFullPath(
                filePath);

        return new OmsiTimetableLine(
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
            userAllowed,
            lines[priorityIndex + 1]
                .Trim(),
            tourBuilders
                .Select(
                    tour =>
                        new OmsiTimetableTour(
                            tour.Name,
                            tour.AiGroupName,
                            tour.Line3,
                            tour.Trips
                                .ToArray()))
                .ToArray());
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
                    StringComparison.Ordinal))
            {
                return string.Empty;
            }

            return value;
        }

        return string.Empty;
    }

    private sealed class TourBuilder(
        string name,
        string aiGroupName,
        string line3)
    {
        public string Name { get; } =
            name;

        public string AiGroupName { get; } =
            aiGroupName;

        public string Line3 { get; } =
            line3;

        public List<OmsiTimetableAddTrip>
            Trips { get; } = [];
    }
}
