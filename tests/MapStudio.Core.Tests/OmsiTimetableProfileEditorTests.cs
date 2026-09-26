using MapStudio.Core.Omsi.Timetables;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiTimetableProfileEditorTests
{
    [Fact]
    public void ReadProfilesParsesTotalAndManualArrivalTimes()
    {
        var lines =
            new[]
            {
                "[profile]",
                "standard",
                "8.000",
                "[profile_man_arr_time]",
                "0",
                "0.000",
                "[profile_man_arr_time]",
                "2",
                "5.500",
                "[profile]",
                "fast",
                "6.25"
            };

        var profiles =
            OmsiTimetableProfileEditor
                .ReadProfiles(lines);

        Assert.Equal(
            2,
            profiles.Count);

        Assert.Equal(
            "standard",
            profiles[0].Name);

        Assert.Equal(
            8.0,
            profiles[0].TotalMinutes);

        Assert.Equal(
            2,
            profiles[0].StopTimes.Count);

        Assert.Equal(
            2,
            profiles[0]
                .StopTimes[1]
                .StationIndex);

        Assert.Equal(
            5.5,
            profiles[0]
                .StopTimes[1]
                .Minutes);
    }

    [Fact]
    public void SetManualArrivalMinutesPreservesUnknownProfileLines()
    {
        var lines =
            new[]
            {
                "[profile]",
                "standard",
                "8.000",
                "[custom_unknown]",
                "keep-me",
                "[profile_man_arr_time]",
                "0",
                "0.000"
            };

        var updated =
            OmsiTimetableProfileEditor
                .SetManualArrivalMinutes(
                    lines,
                    0,
                    0,
                    1.25);

        Assert.Contains(
            "[custom_unknown]",
            updated);

        Assert.Contains(
            "keep-me",
            updated);

        var parsed =
            OmsiTimetableProfileEditor
                .ReadProfiles(
                    updated);

        Assert.Equal(
            1.25,
            parsed[0]
                .StopTimes[0]
                .Minutes);
    }

    [Fact]
    public void CreateAndDeleteProfileRoundTrips()
    {
        var created =
            OmsiTimetableProfileEditor
                .CreateProfile(
                    [],
                    "Normal",
                    12.5);

        var profiles =
            OmsiTimetableProfileEditor
                .ReadProfiles(
                    created);

        Assert.Single(
            profiles);

        Assert.Equal(
            12.5,
            profiles[0]
                .TotalMinutes);

        var deleted =
            OmsiTimetableProfileEditor
                .DeleteProfile(
                    created,
                    0);

        Assert.Empty(
            deleted);
    }
}
