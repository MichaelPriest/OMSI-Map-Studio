using System.Globalization;

namespace MapStudio.Core.Omsi.Timetables;

public static class OmsiTimetableDepartureTime
{
    public static bool TryParseEditorValue(
        string? text,
        out double seconds)
    {
        seconds =
            0;

        var value =
            text?.Trim() ??
            string.Empty;

        if (
            double.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var raw) &&
            double.IsFinite(
                raw) &&
            raw >= 0)
        {
            seconds =
                raw;

            return true;
        }

        var parts =
            value.Split(
                ':',
                StringSplitOptions.TrimEntries);

        if (
            parts.Length is not
                (
                    2 or
                    3
                ) ||
            !int.TryParse(
                parts[0],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var hours) ||
            !int.TryParse(
                parts[1],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var minutes) ||
            hours < 0 ||
            minutes is < 0 or > 59)
        {
            return false;
        }

        var parsedSeconds =
            0d;

        if (
            parts.Length ==
                3 &&
            (
                !double.TryParse(
                    parts[2],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out parsedSeconds) ||
                !double.IsFinite(
                    parsedSeconds) ||
                parsedSeconds < 0 ||
                parsedSeconds >= 60
            ))
        {
            return false;
        }

        seconds =
            hours *
                3600d +
            minutes *
                60d +
            parsedSeconds;

        return true;
    }

    public static string FormatEditorValue(
        string? omsiValue)
    {
        if (
            !double.TryParse(
                omsiValue?.Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var seconds) ||
            !double.IsFinite(
                seconds) ||
            seconds < 0)
        {
            return omsiValue ??
                string.Empty;
        }

        return FormatEditorSeconds(
            seconds);
    }

    public static string FormatEditorSeconds(
        double seconds)
    {
        if (
            !double.IsFinite(
                seconds) ||
            seconds < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(
                    seconds));
        }

        var hours =
            (long)Math.Floor(
                seconds /
                3600d);

        var afterHours =
            seconds -
            hours *
                3600d;

        var minutes =
            (int)Math.Floor(
                afterHours /
                60d);

        var remainder =
            afterHours -
            minutes *
                60d;

        var secondsText =
            Math.Abs(
                remainder -
                Math.Round(
                    remainder)) <
                0.000000001
                ? Math.Round(
                        remainder)
                    .ToString(
                        "00",
                        CultureInfo.InvariantCulture)
                : remainder
                    .ToString(
                        "00.###",
                        CultureInfo.InvariantCulture);

        return
            $"{hours:00}:{minutes:00}:{secondsText}";
    }

    public static string FormatOmsiSeconds(
        double seconds)
    {
        if (
            !double.IsFinite(
                seconds) ||
            seconds < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(
                    seconds));
        }

        return seconds.ToString(
            "G17",
            CultureInfo.InvariantCulture);
    }
}
