namespace MapStudio.Core.Omsi.Timetables;

public sealed record OmsiTimetableAddTrip(
    string Comment,
    string TripName,
    string Line2,
    string DepartureTime)
{
    public double? DepartureSeconds =>
        double.TryParse(
            DepartureTime,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var value) &&
        double.IsFinite(value)
            ? value
            : null;
}
