using MapStudio.Core.Omsi.Timetables;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiTimetableLineTableEditorTests
{
    [Fact]
    public void FlattensAndBuildsTours()
    {
        var line =
            new OmsiTimetableLine(
                "Line.ttl",
                "TTData/Line.ttl",
                "Line",
                string.Empty,
                string.Empty,
                true,
                "2",
                [
                    new OmsiTimetableTour(
                        "Tour 1",
                        "Busses",
                        "0",
                        [
                            new OmsiTimetableAddTrip(
                                "Morning",
                                "Trip A",
                                "0",
                                "28800"),
                            new OmsiTimetableAddTrip(
                                "Late",
                                "Trip B",
                                "0",
                                "97500")
                        ])
                ]);

        var rows =
            OmsiTimetableLineTableEditor
                .Flatten(
                    line);

        Assert.Equal(
            "08:00:00",
            rows[0].DepartureText);

        Assert.Equal(
            "27:05:00",
            rows[1].DepartureText);

        var tours =
            OmsiTimetableLineTableEditor
                .BuildTours(
                    rows,
                    [
                        "Trip A",
                        "Trip B"
                    ]);

        var tour =
            Assert.Single(
                tours);

        Assert.Equal(
            "Tour 1",
            tour.Name);

        Assert.Equal(
            "28800",
            tour.Trips[0]
                .DepartureTime);

        Assert.Equal(
            "97500",
            tour.Trips[1]
                .DepartureTime);
    }

    [Fact]
    public void RejectsConflictingTourMetadata()
    {
        var rows =
            new[]
            {
                new OmsiTimetableLineTableRow(
                    "Tour 1",
                    "Busses",
                    "0",
                    string.Empty,
                    "Trip A",
                    "0",
                    "08:00"),
                new OmsiTimetableLineTableRow(
                    "Tour 1",
                    "Trucks",
                    "0",
                    string.Empty,
                    "Trip B",
                    "0",
                    "09:00")
            };

        Assert.Throws<
            InvalidDataException>(
                () =>
                    OmsiTimetableLineTableEditor
                        .BuildTours(
                            rows));
    }

    [Fact]
    public void RejectsUnknownTripWhenCatalogIsProvided()
    {
        var rows =
            new[]
            {
                new OmsiTimetableLineTableRow(
                    "Tour 1",
                    "Busses",
                    "0",
                    string.Empty,
                    "Missing",
                    "0",
                    "08:00")
            };

        Assert.Throws<
            InvalidDataException>(
                () =>
                    OmsiTimetableLineTableEditor
                        .BuildTours(
                            rows,
                            [
                                "Known"
                            ]));
    }
}
