using System.Text;

namespace MapStudio.Core.Omsi.Timetables;

public sealed class OmsiTimetableLineWriter
{
    public byte[] Write(
        OmsiTimetableLine source,
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
            source.Tours.Count >
                10000 ||
            source.Tours.Sum(
                tour =>
                    tour.Trips.Count) >
                100000)
        {
            throw new InvalidDataException(
                "timetableLineTooLarge");
        }

        var lines =
            new List<string>
            {
                "-----------------------",
                "Time Table Line File",
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

        if (source.UserAllowed)
        {
            lines.Add(
                "[userallowed]");

            lines.Add(
                string.Empty);
        }

        lines.Add(
            "[priority]");

        lines.Add(
            source.Priority ??
            string.Empty);

        lines.Add(
            string.Empty);

        foreach (
            var tour in
                source.Tours)
        {
            if (string.IsNullOrWhiteSpace(
                    tour.Name))
            {
                throw new InvalidDataException(
                    "timetableTourNameMissing");
            }

            lines.Add(
                "------------------------------------");

            lines.Add(
                string.Empty);

            lines.Add(
                "[newtour]");

            lines.Add(
                tour.Name.Trim());

            lines.Add(
                tour.AiGroupName ??
                string.Empty);

            lines.Add(
                tour.Line3 ??
                string.Empty);

            lines.Add(
                string.Empty);

            foreach (
                var trip in
                    tour.Trips)
            {
                if (string.IsNullOrWhiteSpace(
                        trip.TripName))
                {
                    throw new InvalidDataException(
                        "timetableAddTripNameMissing");
                }

                if (
                    !string.IsNullOrWhiteSpace(
                        trip.Comment))
                {
                    lines.Add(
                        trip.Comment.Trim());
                }

                lines.Add(
                    "[addtrip]");

                lines.Add(
                    trip.TripName.Trim());

                lines.Add(
                    trip.Line2 ??
                    string.Empty);

                lines.Add(
                    trip.DepartureTime ??
                    string.Empty);

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
