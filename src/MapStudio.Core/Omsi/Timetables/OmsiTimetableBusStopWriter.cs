using System.Globalization;
using System.Text;

namespace MapStudio.Core.Omsi.Timetables;

public sealed class OmsiTimetableBusStopWriter
{
    public byte[] Write(
        IReadOnlyList<
            OmsiTimetableBusStop> stops,
        string newLine = "\r\n")
    {
        ArgumentNullException.ThrowIfNull(
            stops);

        if (stops.Count > 100000)
        {
            throw new InvalidDataException(
                "busStopCountTooLarge");
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
                "Time Table BusStopList File",
                "---------------------------",
                string.Empty,
                "Edited with OMSI Map Studio",
                string.Empty
            };

        foreach (
            var stop in stops)
        {
            if (
                stop.Id < 0 ||
                stop.TileIndex < -1)
            {
                throw new InvalidDataException(
                    "busStopInvalid");
            }

            lines.Add(
                "[busstop]");

            lines.Add(
                stop.Name ??
                string.Empty);

            lines.Add(
                stop.TileIndex
                    .ToString(
                        CultureInfo
                            .InvariantCulture));

            lines.Add(
                stop.Id
                    .ToString(
                        CultureInfo
                            .InvariantCulture));

            lines.Add(
                stop.ExitingPassengers
                    ?.ToString(
                        "G17",
                        CultureInfo
                            .InvariantCulture) ??
                "0");

            lines.Add(
                stop.Line4 ??
                string.Empty);

            lines.Add(
                stop.Line5 ??
                string.Empty);

            lines.Add(
                stop.SubName ??
                string.Empty);

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
