using MapStudio.Core.Omsi.Timetables;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiTimetableLineWriterTests
{
    [Fact]
    public async Task WriteRoundTripsToursAndTrips()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-ttl-writer-" +
                Guid.NewGuid()
                    .ToString("N"));

        Directory.CreateDirectory(
            root);

        var path =
            Path.Combine(
                root,
                "Line_100.ttl");

        try
        {
            var source =
                new OmsiTimetableLine(
                    path,
                    "TTData\\Line_100.ttl",
                    "Line_100",
                    "Line 100",
                    "Weekday",
                    true,
                    "2",
                    [
                        new OmsiTimetableTour(
                            "10001",
                            "Busses",
                            "0",
                            [
                                new OmsiTimetableAddTrip(
                                    "Outbound",
                                    "Line_100_A",
                                    "0",
                                    "28800"),
                                new OmsiTimetableAddTrip(
                                    "Inbound",
                                    "Line_100_B",
                                    "0",
                                    "30600")
                            ])
                    ]);

            await File.WriteAllBytesAsync(
                path,
                new OmsiTimetableLineWriter()
                    .Write(source));

            var parsed =
                await new OmsiTimetableLineReader()
                    .ReadAsync(
                        root,
                        path);

            Assert.True(
                parsed.UserAllowed);

            Assert.Equal(
                "2",
                parsed.Priority);

            var tour =
                Assert.Single(
                    parsed.Tours);

            Assert.Equal(
                "10001",
                tour.Name);

            Assert.Equal(
                "Busses",
                tour.AiGroupName);

            Assert.Equal(
                2,
                tour.Trips.Count);

            Assert.Equal(
                "Line_100_A",
                tour.Trips[0].TripName);

            Assert.Equal(
                "28800",
                tour.Trips[0].DepartureTime);
        }
        finally
        {
            Directory.Delete(
                root,
                recursive: true);
        }
    }
}
