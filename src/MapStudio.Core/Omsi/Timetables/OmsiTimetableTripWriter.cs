using System.Globalization;
using System.Text;

namespace MapStudio.Core.Omsi.Timetables;

public sealed class OmsiTimetableTripWriter
{
    public byte[] Write(
        OmsiTimetableTrip source,
        string newLine = "\r\n")
    {
        ArgumentNullException.ThrowIfNull(
            source);

        if (string.IsNullOrEmpty(
                newLine))
        {
            newLine =
                "\r\n";
        }

        if (
            source.Stations.Count >
                100000 ||
            source.ProfileLines.Count >
                500000)
        {
            throw new InvalidDataException(
                "tripDataTooLarge");
        }

        var lines =
            new List<string>
            {
                "-----------------------",
                "Time Table Trip File",
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

        lines.Add(
            "[trip]");

        lines.Add(
            source.TrackName ??
            string.Empty);

        lines.Add(
            source.Destination ??
            string.Empty);

        lines.Add(
            source.Line ??
            string.Empty);

        lines.Add(
            string.Empty);

        if (source.TrainReverse)
        {
            lines.Add(
                "[trainreverse]");

            lines.Add(
                string.Empty);
        }

        lines.Add(
            ".........................");

        lines.Add(
            "        Stations");

        lines.Add(
            ".........................");

        lines.Add(
            string.Empty);

        foreach (
            var station in
                source.Stations)
        {
            switch (station)
            {
                case OmsiTimetableTripStationType2
                    type2:
                    lines.Add(
                        "[station_typ2]");

                    lines.Add(
                        type2.Id.ToString(
                            CultureInfo
                                .InvariantCulture));

                    lines.Add(
                        string.Empty);

                    break;

                case OmsiTimetableTripStationType1
                    type1:
                    lines.Add(
                        "[station]");

                    lines.Add(
                        type1.Id.ToString(
                            CultureInfo
                                .InvariantCulture));

                    lines.Add(
                        type1.Interval ??
                        string.Empty);

                    lines.Add(
                        type1.Name ??
                        string.Empty);

                    lines.Add(
                        type1.TileIndex
                            .ToString(
                                CultureInfo
                                    .InvariantCulture));

                    lines.Add(
                        type1.Line5 ??
                        string.Empty);

                    lines.Add(
                        type1.Line6 ??
                        string.Empty);

                    lines.Add(
                        type1.Line7 ??
                        string.Empty);

                    lines.Add(
                        type1.Line8 ??
                        string.Empty);

                    lines.Add(
                        string.Empty);

                    break;

                default:
                    throw new InvalidDataException(
                        "tripStationTypeUnsupported");
            }
        }

        if (
            source.ProfileLines.Count >
            0)
        {
            lines.Add(
                ".........................");

            lines.Add(
                "        Profiles");

            lines.Add(
                ".........................");

            lines.Add(
                string.Empty);

            foreach (
                var line in
                    source.ProfileLines)
            {
                lines.Add(
                    line ??
                    string.Empty);
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
