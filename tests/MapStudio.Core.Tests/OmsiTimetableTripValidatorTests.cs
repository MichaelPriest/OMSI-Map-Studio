using MapStudio.Core.Omsi.Timetables;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiTimetableTripValidatorTests
{
    [Fact]
    public void AcceptsKnownStopsConnectedByStationLinks()
    {
        var catalog =
            new OmsiTimetableCatalog(
                [],
                [])
            {
                BusStops =
                [
                    new OmsiTimetableBusStop(
                        "A",
                        0,
                        100,
                        null,
                        "0",
                        "0",
                        string.Empty),
                    new OmsiTimetableBusStop(
                        "B",
                        0,
                        200,
                        null,
                        "0",
                        "0",
                        string.Empty)
                ],
                StationLinks =
                [
                    new OmsiStationLink(
                        "A-B",
                        "0",
                        100,
                        200,
                        "0",
                        "0",
                        "0",
                        "0",
                        "0",
                        "0",
                        [])
                ]
            };

        var result =
            OmsiTimetableTripValidator
                .ValidateType2StationLinks(
                    catalog,
                    [
                        new OmsiTimetableTripStationType2(
                            100),
                        new OmsiTimetableTripStationType2(
                            200)
                    ]);

        Assert.True(
            result.IsValid);

        Assert.Equal(
            OmsiTimetableType2TripValidationFailure.None,
            result.Failure);
    }

    [Fact]
    public void ReportsUnknownStop()
    {
        var catalog =
            new OmsiTimetableCatalog(
                [],
                [])
            {
                BusStops =
                [
                    new OmsiTimetableBusStop(
                        "A",
                        0,
                        100,
                        null,
                        "0",
                        "0",
                        string.Empty)
                ]
            };

        var result =
            OmsiTimetableTripValidator
                .ValidateType2StationLinks(
                    catalog,
                    [
                        new OmsiTimetableTripStationType2(
                            100),
                        new OmsiTimetableTripStationType2(
                            999)
                    ]);

        Assert.False(
            result.IsValid);

        Assert.Equal(
            OmsiTimetableType2TripValidationFailure.UnknownStop,
            result.Failure);

        Assert.Equal(
            999,
            result.StopId);
    }

    [Fact]
    public void ReportsMissingConsecutiveStationLink()
    {
        var catalog =
            new OmsiTimetableCatalog(
                [],
                [])
            {
                BusStops =
                [
                    new OmsiTimetableBusStop(
                        "A",
                        0,
                        100,
                        null,
                        "0",
                        "0",
                        string.Empty),
                    new OmsiTimetableBusStop(
                        "B",
                        0,
                        200,
                        null,
                        "0",
                        "0",
                        string.Empty)
                ]
            };

        var result =
            OmsiTimetableTripValidator
                .ValidateType2StationLinks(
                    catalog,
                    [
                        new OmsiTimetableTripStationType2(
                            100),
                        new OmsiTimetableTripStationType2(
                            200)
                    ]);

        Assert.False(
            result.IsValid);

        Assert.Equal(
            OmsiTimetableType2TripValidationFailure.MissingStationLink,
            result.Failure);

        Assert.Equal(
            100,
            result.StartStopId);

        Assert.Equal(
            200,
            result.EndStopId);
    }

    [Fact]
    public void RejectsMixedOrShortStationSequences()
    {
        var catalog =
            new OmsiTimetableCatalog(
                [],
                []);

        var result =
            OmsiTimetableTripValidator
                .ValidateType2StationLinks(
                    catalog,
                    [
                        new OmsiTimetableTripStationType2(
                            100)
                    ]);

        Assert.False(
            result.IsValid);

        Assert.Equal(
            OmsiTimetableType2TripValidationFailure.InvalidStationSequence,
            result.Failure);
    }
}
