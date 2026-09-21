using System.Globalization;
using System.Text;

namespace MapStudio.Core.Omsi.Timetables;

public sealed class OmsiTimetableTripReader
{
    public async Task<OmsiTimetableTrip> ReadAsync(
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

        var tripIndex =
            Array.FindIndex(
                lines,
                line =>
                    string.Equals(
                        line.Trim(),
                        "[trip]",
                        StringComparison.OrdinalIgnoreCase));

        if (tripIndex < 0)
        {
            throw new InvalidDataException(
                "tripSectionMissing");
        }

        var tripData =
            ReadFollowingDataLines(
                lines,
                tripIndex + 1,
                3);

        if (tripData.Count < 3)
        {
            throw new InvalidDataException(
                "tripSectionMalformed");
        }

        var comments =
            lines
                .Take(tripIndex)
                .Where(
                    line =>
                        !string.IsNullOrWhiteSpace(
                            line) &&
                        !line.StartsWith(
                            "-",
                            StringComparison.Ordinal) &&
                        !string.Equals(
                            line.Trim(),
                            "Time Table Trip File",
                            StringComparison.OrdinalIgnoreCase))
                .TakeLast(2)
                .ToArray();

        var stations =
            new List<
                OmsiTimetableTripStation>();

        var trainReverse =
            false;

        var profilesStart =
            -1;

        for (
            var index = tripIndex + 1;
            index < lines.Length;
            index++)
        {
            var keyword =
                lines[index].Trim();

            if (string.Equals(
                    keyword,
                    "[trainreverse]",
                    StringComparison.OrdinalIgnoreCase))
            {
                trainReverse =
                    true;

                continue;
            }

            if (string.Equals(
                    keyword,
                    "[station_typ2]",
                    StringComparison.OrdinalIgnoreCase))
            {
                var data =
                    ReadFollowingDataLines(
                        lines,
                        index + 1,
                        1);

                if (
                    data.Count == 1 &&
                    int.TryParse(
                        data[0],
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var id))
                {
                    stations.Add(
                        new OmsiTimetableTripStationType2(
                            id));
                }

                continue;
            }

            if (string.Equals(
                    keyword,
                    "[station]",
                    StringComparison.OrdinalIgnoreCase))
            {
                var data =
                    ReadFollowingDataLines(
                        lines,
                        index + 1,
                        8);

                if (
                    data.Count >= 8 &&
                    int.TryParse(
                        data[0],
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var id) &&
                    int.TryParse(
                        data[3],
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var tileIndex))
                {
                    stations.Add(
                        new OmsiTimetableTripStationType1(
                            id,
                            data[1],
                            data[2],
                            tileIndex,
                            data[4],
                            data[5],
                            data[6],
                            data[7]));
                }

                continue;
            }

            if (string.Equals(
                    keyword,
                    "Profiles",
                    StringComparison.OrdinalIgnoreCase))
            {
                profilesStart =
                    index + 1;
            }
        }

        var profileLines =
            profilesStart < 0
                ? Array.Empty<string>()
                : lines
                    .Skip(profilesStart)
                    .Select(
                        line =>
                            line.Trim())
                    .Where(
                        line =>
                            !string.IsNullOrWhiteSpace(
                                line) &&
                            !line.All(
                                character =>
                                    character ==
                                    '.'))
                    .ToArray();

        var fullMapDirectory =
            Path.GetFullPath(
                mapDirectory);

        var fullFilePath =
            Path.GetFullPath(
                filePath);

        return new OmsiTimetableTrip(
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
            tripData[0],
            tripData[1],
            tripData[2],
            trainReverse,
            stations,
            profileLines);
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
}
