using System.Globalization;

namespace MapStudio.Core.Omsi.Timetables;

public sealed record OmsiTimetableProfileStopTime(
    int StationIndex,
    double Minutes);

public sealed record OmsiTimetableProfileDefinition(
    int Index,
    string Name,
    double? TotalMinutes,
    IReadOnlyList<
        OmsiTimetableProfileStopTime>
        StopTimes,
    int StartLineIndex,
    int EndLineIndexExclusive)
{
    public string DisplayText =>
        TotalMinutes is double minutes
            ? $"{Name} · {minutes:0.###} min · {StopTimes.Count} ponto(s)"
            : $"{Name} · duração não definida · {StopTimes.Count} ponto(s)";
}

public static class OmsiTimetableProfileEditor
{
    private const string ProfileKeyword =
        "[profile]";

    private const string ManualArrivalKeyword =
        "[profile_man_arr_time]";

    public static IReadOnlyList<
        OmsiTimetableProfileDefinition>
        ReadProfiles(
            IReadOnlyList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(
            lines);

        var starts =
            Enumerable
                .Range(
                    0,
                    lines.Count)
                .Where(
                    index =>
                        IsKeyword(
                            lines[index],
                            ProfileKeyword))
                .ToArray();

        var result =
            new List<
                OmsiTimetableProfileDefinition>(
                    starts.Length);

        for (
            var profileIndex = 0;
            profileIndex <
                starts.Length;
            profileIndex++)
        {
            var start =
                starts[profileIndex];

            var end =
                profileIndex + 1 <
                    starts.Length
                    ? starts[
                        profileIndex +
                        1]
                    : lines.Count;

            var name =
                start + 1 < end
                    ? lines[start + 1]
                        .Trim()
                    : string.Empty;

            double? totalMinutes =
                null;

            if (
                start + 2 < end &&
                double.TryParse(
                    lines[start + 2]
                        .Trim(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var parsedTotal) &&
                double.IsFinite(
                    parsedTotal))
            {
                totalMinutes =
                    parsedTotal;
            }

            var stopTimes =
                new List<
                    OmsiTimetableProfileStopTime>();

            for (
                var index =
                    start + 3;
                index < end;
                index++)
            {
                if (
                    !IsKeyword(
                        lines[index],
                        ManualArrivalKeyword) ||
                    index + 2 >= end)
                {
                    continue;
                }

                if (
                    int.TryParse(
                        lines[index + 1]
                            .Trim(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var stationIndex) &&
                    double.TryParse(
                        lines[index + 2]
                            .Trim(),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var minutes) &&
                    stationIndex >= 0 &&
                    double.IsFinite(
                        minutes))
                {
                    stopTimes.Add(
                        new OmsiTimetableProfileStopTime(
                            stationIndex,
                            minutes));
                }

                index += 2;
            }

            result.Add(
                new OmsiTimetableProfileDefinition(
                    profileIndex,
                    string.IsNullOrWhiteSpace(
                        name)
                        ? $"Profile {profileIndex + 1}"
                        : name,
                    totalMinutes,
                    stopTimes
                        .OrderBy(
                            value =>
                                value.StationIndex)
                        .ToArray(),
                    start,
                    end));
        }

        return result;
    }

    public static IReadOnlyList<string>
        CreateProfile(
            IReadOnlyList<string> lines,
            string name,
            double totalMinutes)
    {
        ArgumentNullException.ThrowIfNull(
            lines);

        var normalizedName =
            NormalizeName(
                name);

        ValidateMinutes(
            totalMinutes);

        if (
            ReadProfiles(
                lines)
                .Any(
                    profile =>
                        string.Equals(
                            profile.Name,
                            normalizedName,
                            StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException(
                "duplicateTimeProfileName");
        }

        var result =
            lines
                .ToList();

        result.Add(
            ProfileKeyword);

        result.Add(
            normalizedName);

        result.Add(
            FormatMinutes(
                totalMinutes));

        return result;
    }

    public static IReadOnlyList<string>
        SetTotalMinutes(
            IReadOnlyList<string> lines,
            int profileIndex,
            double totalMinutes)
    {
        ArgumentNullException.ThrowIfNull(
            lines);

        ValidateMinutes(
            totalMinutes);

        var profile =
            GetProfile(
                lines,
                profileIndex);

        var result =
            lines
                .ToList();

        var totalIndex =
            profile.StartLineIndex +
            2;

        if (
            totalIndex >=
                profile.EndLineIndexExclusive)
        {
            throw new InvalidDataException(
                "timeProfileTotalMissing");
        }

        result[totalIndex] =
            FormatMinutes(
                totalMinutes);

        return result;
    }

    public static IReadOnlyList<string>
        SetManualArrivalMinutes(
            IReadOnlyList<string> lines,
            int profileIndex,
            int stationIndex,
            double minutes)
    {
        ArgumentNullException.ThrowIfNull(
            lines);

        if (stationIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(
                    stationIndex));
        }

        if (
            !double.IsFinite(
                minutes) ||
            minutes < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(
                    minutes));
        }

        var profile =
            GetProfile(
                lines,
                profileIndex);

        var result =
            lines
                .ToList();

        for (
            var index =
                profile.StartLineIndex +
                3;
            index <
                profile.EndLineIndexExclusive;
            index++)
        {
            if (
                !IsKeyword(
                    result[index],
                    ManualArrivalKeyword) ||
                index + 2 >=
                    profile.EndLineIndexExclusive)
            {
                continue;
            }

            if (
                int.TryParse(
                    result[index + 1]
                        .Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var existingStationIndex) &&
                existingStationIndex ==
                    stationIndex)
            {
                result[index + 2] =
                    FormatMinutes(
                        minutes);

                return result;
            }
        }

        result.InsertRange(
            profile.EndLineIndexExclusive,
            [
                ManualArrivalKeyword,
                stationIndex.ToString(
                    CultureInfo.InvariantCulture),
                FormatMinutes(
                    minutes)
            ]);

        return result;
    }

    public static IReadOnlyList<string>
        DeleteProfile(
            IReadOnlyList<string> lines,
            int profileIndex)
    {
        ArgumentNullException.ThrowIfNull(
            lines);

        var profile =
            GetProfile(
                lines,
                profileIndex);

        var result =
            lines
                .ToList();

        result.RemoveRange(
            profile.StartLineIndex,
            profile.EndLineIndexExclusive -
                profile.StartLineIndex);

        return result;
    }

    private static OmsiTimetableProfileDefinition
        GetProfile(
            IReadOnlyList<string> lines,
            int profileIndex)
    {
        var profiles =
            ReadProfiles(
                lines);

        if (
            profileIndex < 0 ||
            profileIndex >=
                profiles.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(
                    profileIndex));
        }

        return profiles[
            profileIndex];
    }

    private static bool IsKeyword(
        string? line,
        string keyword) =>
        string.Equals(
            line?.Trim(),
            keyword,
            StringComparison.OrdinalIgnoreCase);

    private static string NormalizeName(
        string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            name);

        var normalized =
            name.Trim();

        if (
            normalized.Contains(
                '\r') ||
            normalized.Contains(
                '\n') ||
            normalized.StartsWith(
                "[",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "timeProfileNameInvalid");
        }

        return normalized;
    }

    private static void ValidateMinutes(
        double minutes)
    {
        if (
            !double.IsFinite(
                minutes) ||
            minutes <= 0 ||
            minutes > 100000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(
                    minutes));
        }
    }

    private static string FormatMinutes(
        double minutes) =>
        minutes.ToString(
            "0.###",
            CultureInfo.InvariantCulture);
}
