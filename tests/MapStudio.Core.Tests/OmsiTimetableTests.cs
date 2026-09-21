using System.Text;
using MapStudio.Core.Omsi.Timetables;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiTimetableTests
{
    [Fact]
    public async Task CatalogReadsTrackTripAndReference()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-tt-" +
                Guid.NewGuid()
                    .ToString("N"));

        var tt =
            Path.Combine(
                root,
                "TTData");

        Directory.CreateDirectory(
            tt);

        try
        {
            var trackPath =
                Path.Combine(
                    tt,
                    "Line_100.ttr");

            var tripPath =
                Path.Combine(
                    tt,
                    "Line_100_A.ttp");

            await File.WriteAllTextAsync(
                trackPath,
                "-----------------------\r\n" +
                "Time Table Track File\r\n" +
                "-----------------------\r\n\r\n" +
                "Line 100\r\nOutbound\r\n\r\n" +
                "Segment A\r\n" +
                "[track_entry]\r\n" +
                "77\r\n0\r\n5\r\n0\r\n125.5\r\n0\r\n\r\n",
                Encoding.Latin1);

            await File.WriteAllTextAsync(
                Path.Combine(
                    tt,
                    "Busstops.cfg"),
                "---------------------------\r\n" +
                "Time Table BusStopList File\r\n" +
                "---------------------------\r\n\r\n" +
                "Created\r\nDate\r\n\r\n" +
                "[busstop]\r\n" +
                "Central\r\n" +
                "5\r\n5001\r\n0\r\n0\r\n0\r\n\r\n" +
                "[busstop]\r\n" +
                "Depot\r\n" +
                "6\r\n5002\r\n0\r\n0\r\n0\r\n\r\n",
                Encoding.Latin1);

            await File.WriteAllTextAsync(
                Path.Combine(
                    tt,
                    "StnLinks.cfg"),
                "---------------------------\r\n" +
                "Time Table StnLinkList File\r\n" +
                "---------------------------\r\n\r\n" +
                "Created\r\nDate\r\n\r\n" +
                "Central ==> Depot\r\n" +
                "[StnLink]\r\n" +
                "182\r\n5001\r\n5002\r\n" +
                "3.3\r\n3.2\r\n0.7\r\n20.9\r\n1\r\n10\r\n\r\n" +
                "0:\r\n" +
                "[StnLink_entry]\r\n" +
                "77\r\n0\r\n5\r\n125.5\r\n-1\r\n0\r\n0\r\n\r\n",
                Encoding.Latin1);

            await File.WriteAllTextAsync(
                Path.Combine(
                    tt,
                    "Line_100.ttl"),
                "-----------------------\r\n" +
                "Time Table Line File\r\n" +
                "-----------------------\r\n\r\n" +
                "Line 100\r\nWeekday\r\n\r\n" +
                "[userallowed]\r\n\r\n" +
                "[priority]\r\n2\r\n" +
                "------------------------------------\r\n\r\n" +
                "[newtour]\r\n" +
                "10001\r\n" +
                "Busses\r\n" +
                "0\r\n\r\n" +
                "------------------------------------\r\n\r\n" +
                "Outbound\r\n" +
                "[addtrip]\r\n" +
                "Line_100_A\r\n" +
                "0\r\n" +
                "28800\r\n",
                Encoding.Latin1);

            await File.WriteAllTextAsync(
                tripPath,
                "-----------------------\r\n" +
                "Time Table Trip File\r\n" +
                "-----------------------\r\n\r\n" +
                "Line 100\r\nOutbound A\r\n\r\n" +
                "[trip]\r\n" +
                "Line_100\r\n" +
                "CENTRAL\r\n" +
                "100\r\n\r\n" +
                "[station_typ2]\r\n" +
                "5001\r\n\r\n" +
                ".........................\r\n" +
                "        Profiles\r\n" +
                ".........................\r\n\r\n" +
                "Standard\r\n",
                Encoding.Latin1);

            var catalog =
                await new OmsiTimetableCatalogReader()
                    .ReadAsync(
                        root);

            var track =
                Assert.Single(
                    catalog.Tracks);

            Assert.Equal(
                "Line_100",
                track.Name);

            var entry =
                Assert.Single(
                    track.Entries);

            Assert.Equal(77, entry.Id);
            Assert.Equal(5, entry.TileIndex);
            Assert.Equal(125.5, entry.Length);

            var trip =
                Assert.Single(
                    catalog.Trips);

            Assert.Equal(
                "Line_100",
                trip.TrackName);

            Assert.Equal(
                "CENTRAL",
                trip.Destination);

            Assert.Equal(
                "100",
                trip.Line);

            Assert.Single(
                trip.Stations);

            Assert.Equal(
                0,
                catalog
                    .BrokenTripTrackReferenceCount);

            Assert.Equal(
                2,
                catalog.BusStops.Count);

            Assert.Equal(
                1,
                catalog.StationLinks.Count);

            Assert.Equal(
                0,
                catalog
                    .BrokenStationLinkStopReferenceCount);

            var link =
                catalog.StationLinks[0];

            Assert.Equal(
                5001,
                link.StartBusStopId);

            Assert.Equal(
                5002,
                link.EndBusStopId);

            Assert.Single(
                link.Entries);

            var line =
                Assert.Single(
                    catalog.Lines);

            Assert.True(
                line.UserAllowed);

            Assert.Equal(
                "2",
                line.Priority);

            var tour =
                Assert.Single(
                    line.Tours);

            Assert.Equal(
                "10001",
                tour.Name);

            Assert.Equal(
                "Busses",
                tour.AiGroupName);

            Assert.Equal(
                "Line_100_A",
                Assert.Single(
                    tour.Trips)
                    .TripName);

            Assert.Equal(
                0,
                catalog
                    .BrokenLineTripReferenceCount);
        }
        finally
        {
            Directory.Delete(
                root,
                recursive: true);
        }
    }

    [Fact]
    public async Task CatalogFlagsMissingTrackReference()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-tt-" +
                Guid.NewGuid()
                    .ToString("N"));

        var tt =
            Path.Combine(
                root,
                "TTData");

        Directory.CreateDirectory(
            tt);

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(
                    tt,
                    "Broken.ttp"),
                "-----------------------\r\n" +
                "Time Table Trip File\r\n" +
                "-----------------------\r\n\r\n" +
                "Broken\r\nTrip\r\n\r\n" +
                "[trip]\r\n" +
                "MissingTrack\r\n" +
                "NOWHERE\r\n" +
                "X\r\n",
                Encoding.Latin1);

            var catalog =
                await new OmsiTimetableCatalogReader()
                    .ReadAsync(
                        root);

            Assert.Equal(
                1,
                catalog
                    .BrokenTripTrackReferenceCount);
        }
        finally
        {
            Directory.Delete(
                root,
                recursive: true);
        }
    }
    [Fact]
    public async Task TripReaderPreservesBlankTrackLineAndStationName()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-ttp-" +
                Guid.NewGuid()
                    .ToString("N"));

        Directory.CreateDirectory(
            root);

        var path =
            Path.Combine(
                root,
                "RealStyle.ttp");

        try
        {
            await File.WriteAllTextAsync(
                path,
                "-----------------------\r\n" +
                "Time Table Trip File\r\n" +
                "-----------------------\r\n\r\n" +
                "Created\r\nDate\r\n\r\n" +
                "[trip]\r\n" +
                "\r\n" +
                "54 GBB-NBH\r\n" +
                "54\r\n\r\n" +
                "[station]\r\n" +
                "738\r\n0\r\n\r\n0\r\n" +
                "-0.061\r\n31.205\r\n31.205\r\n5.000\r\n",
                Encoding.Latin1);

            var trip =
                await new OmsiTimetableTripReader()
                    .ReadAsync(
                        root,
                        path);

            Assert.Equal(
                string.Empty,
                trip.TrackName);

            Assert.Equal(
                "54 GBB-NBH",
                trip.Destination);

            Assert.Equal(
                "54",
                trip.Line);

            var station =
                Assert.IsType<
                    OmsiTimetableTripStationType1>(
                    Assert.Single(
                        trip.Stations));

            Assert.Equal(
                string.Empty,
                station.Name);
        }
        finally
        {
            Directory.Delete(
                root,
                recursive: true);
        }
    }

}
