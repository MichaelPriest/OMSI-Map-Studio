using MapStudio.Core.Omsi.Timetables;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiTimetableDepartureTimeTests
{
    [Theory]
    [InlineData("28800", 28800)]
    [InlineData("08:00", 28800)]
    [InlineData("08:15:30", 29730)]
    [InlineData("27:05:00", 97500)]
    public void ParsesEditorDepartureValues(
        string input,
        double expected)
    {
        Assert.True(
            OmsiTimetableDepartureTime
                .TryParseEditorValue(
                    input,
                    out var seconds));

        Assert.Equal(
            expected,
            seconds,
            6);
    }

    [Theory]
    [InlineData("")]
    [InlineData("-1")]
    [InlineData("08:60")]
    [InlineData("08:10:60")]
    [InlineData("abc")]
    public void RejectsInvalidEditorDepartureValues(
        string input)
    {
        Assert.False(
            OmsiTimetableDepartureTime
                .TryParseEditorValue(
                    input,
                    out _));
    }

    [Theory]
    [InlineData("28800", "08:00:00")]
    [InlineData("97500", "27:05:00")]
    [InlineData("29730.5", "08:15:30.5")]
    public void FormatsOmsiSecondsForEditor(
        string input,
        string expected)
    {
        Assert.Equal(
            expected,
            OmsiTimetableDepartureTime
                .FormatEditorValue(
                    input));
    }

    [Fact]
    public void FormatsEditorSecondsBackForOmsi()
    {
        Assert.Equal(
            "28800",
            OmsiTimetableDepartureTime
                .FormatOmsiSeconds(
                    28800));
    }
}
