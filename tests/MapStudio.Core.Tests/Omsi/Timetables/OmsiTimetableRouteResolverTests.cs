using MapStudio.Core.Omsi.Timetables;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiTimetableRouteResolverTests
{
    [Fact]
    public void ResolvesTrackEntries()
    {
        var track =
            new OmsiTimetableTrack(
                "Track.ttr",
                "TTData/Track.ttr",
                "Track",
                string.Empty,
                string.Empty,
                [
                    new OmsiTimetableTrackEntry(
                        "0:",
                        10,
                        "2",
                        -1,
                        string.Empty,
                        45.5,
                        string.Empty,
                        null)
                ]);

        var catalog =
            new OmsiTimetableCatalog(
                [track],
                Array.Empty<
                    OmsiTimetableTrip>());

        var step =
            Assert.Single(
                OmsiTimetableRouteResolver
                    .Resolve(
                        catalog,
                        "Track",
                        "Track"));

        Assert.Equal(
            10,
            step.EntityId);

        Assert.Equal(
            "2",
            step.PathIndex);

        Assert.Equal(
            45.5,
            step.Length);
    }

    [Fact]
    public void ResolvesType2TripThroughStationLinks()
    {
        var trip =
            new OmsiTimetableTrip(
                "Trip.ttp",
                "TTData/Trip.ttp",
                "Trip",
                string.Empty,
                string.Empty,
                "CENTRAL",
                "100",
                string.Empty,
                false,
                [
                    new OmsiTimetableTripStationType2(
                        1001),
                    new OmsiTimetableTripStationType2(
                        1002),
                    new OmsiTimetableTripStationType2(
                        1003)
                ],
                Array.Empty<string>());

        var links =
            new[]
            {
                CreateLink(
                    1001,
                    1002,
                    20,
                    "0"),
                CreateLink(
                    1002,
                    1003,
                    21,
                    "1")
            };

        var catalog =
            new OmsiTimetableCatalog(
                Array.Empty<
                    OmsiTimetableTrack>(),
                [trip])
            {
                StationLinks =
                    links
            };

        var steps =
            OmsiTimetableRouteResolver
                .Resolve(
                    catalog,
                    "Trip",
                    "Trip");

        Assert.Equal(
            2,
            steps.Count);

        Assert.Equal(
            20,
            steps[0].EntityId);

        Assert.Equal(
            21,
            steps[1].EntityId);

        Assert.Contains(
            "1001→1002",
            steps[0]
                .SourceLabel);
    }

    [Fact]
    public void ResolvesLineToursInScheduleOrder()
    {
        var track =
            new OmsiTimetableTrack(
                "Track.ttr",
                "TTData/Track.ttr",
                "Track",
                string.Empty,
                string.Empty,
                [
                    new OmsiTimetableTrackEntry(
                        "0:",
                        30,
                        "0",
                        -1,
                        string.Empty,
                        12,
                        string.Empty,
                        null)
                ]);

        var trip =
            new OmsiTimetableTrip(
                "Trip.ttp",
                "TTData/Trip.ttp",
                "Trip",
                string.Empty,
                string.Empty,
                "Track",
                "DEST",
                "100",
                false,
                Array.Empty<
                    OmsiTimetableTripStation>(),
                Array.Empty<string>());

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
                        "Tour A",
                        "Busses",
                        "0",
                        [
                            new OmsiTimetableAddTrip(
                                string.Empty,
                                "Trip",
                                "0",
                                "28800"),
                            new OmsiTimetableAddTrip(
                                string.Empty,
                                "Trip",
                                "0",
                                "32400")
                        ])
                ]);

        var catalog =
            new OmsiTimetableCatalog(
                [track],
                [trip])
            {
                Lines =
                    [line]
            };

        var steps =
            OmsiTimetableRouteResolver
                .Resolve(
                    catalog,
                    "Line",
                    "Line");

        Assert.Equal(
            2,
            steps.Count);

        Assert.All(
            steps,
            step =>
                Assert.Equal(
                    30,
                    step.EntityId));
    }

    private static OmsiStationLink
        CreateLink(
            int start,
            int end,
            int entityId,
            string pathIndex) =>
        new(
            string.Empty,
            "0",
            start,
            end,
            "0",
            "0",
            "0",
            "0",
            "0",
            "0",
            [
                new OmsiStationLinkEntry(
                    "0:",
                    entityId,
                    pathIndex,
                    -1,
                    10,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    Array.Empty<string>())
            ]);
}
