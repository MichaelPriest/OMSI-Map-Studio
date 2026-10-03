using System.Globalization;
using System.Text;

namespace MapStudio.Core.Omsi.Timetables;

public sealed class OmsiTimetableBusStopReader
{
    public async Task<IReadOnlyList<
        OmsiTimetableBusStop>> ReadAsync(
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
            new List<
                OmsiTimetableBusStop>();

        for (
            var index = 0;
            index < lines.Length;
            index++)
        {
            if (!string.Equals(
                    lines[index].Trim(),
                    "[busstop]",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (
                index + 7 >=
                    lines.Length)
            {
                continue;
            }

            var values =
                lines
                    .Skip(index + 1)
                    .Take(7)
                    .Select(
                        value =>
                            value.Trim())
                    .ToArray();

            if (
                !int.TryParse(
                    values[1],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var tileIndex) ||
                !int.TryParse(
                    values[2],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var id))
            {
                continue;
            }

            double? exitingPassengers =
                null;

            if (
                double.TryParse(
                    values[3],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var parsed) &&
                double.IsFinite(
                    parsed))
            {
                exitingPassengers =
                    parsed;
            }

            result.Add(
                new OmsiTimetableBusStop(
                    values[0],
                    tileIndex,
                    id,
                    exitingPassengers,
                    values[4],
                    values[5],
                    values[6]));
        }

        return result;
    }
}
