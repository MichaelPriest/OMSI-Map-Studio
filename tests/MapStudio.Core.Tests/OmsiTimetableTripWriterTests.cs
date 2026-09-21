using System.Text;
using MapStudio.Core.Omsi.Timetables;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiTimetableTripWriterTests
{
    [Fact]
    public async Task WriteRoundTripsTripStationsAndProfiles()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-trip-writer-" +
                Guid.NewGuid()
                    .ToString("N"));

        Directory.CreateDirectory(
            root);

        var path =
            Path.Combine(
                root,
                "Line_100_A.ttp");

        try
        {
            var source =
                new OmsiTimetableTrip(
                    path,
                    "TTData\\Line_100_A.ttp",
                    "Line_100_A",
                    "Created with OMSI",
                    "Date: today",
                    "Track_A",
                    "CENTRO",
                    "100",
                    true,
                    [
                        new OmsiTimetableTripStationType2(
                            10),
                        new OmsiTimetableTripStationType1(
                            20,
                            "0",
                            "Terminal",
                            3,
                            "1.5",
                            "30",
                            "35",
                            "5")
                    ],
                    [
                        "[profile]",
                        "standard",
                        "8.000",
                        "[profile_man_arr_time]",
                        "0",
                        "0.000"
                    ]);

            var bytes =
                new OmsiTimetableTripWriter()
                    .Write(
                        source);

            await File.WriteAllBytesAsync(
                path,
                bytes);

            var parsed =
                await new OmsiTimetableTripReader()
                    .ReadAsync(
                        root,
                        path);

            Assert.Equal(
                source.TrackName,
                parsed.TrackName);

            Assert.Equal(
                source.Destination,
                parsed.Destination);

            Assert.Equal(
                source.Line,
                parsed.Line);

            Assert.True(
                parsed.TrainReverse);

            Assert.Equal(
                2,
                parsed.Stations.Count);

            Assert.Equal(
                source.ProfileLines,
                parsed.ProfileLines);
        }
        finally
        {
            Directory.Delete(
                root,
                recursive: true);
        }
    }

    [Fact]
    public async Task WritePreservesBlankTrackName()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-trip-writer-blank-" +
                Guid.NewGuid()
                    .ToString("N"));

        Directory.CreateDirectory(
            root);

        var path =
            Path.Combine(
                root,
                "Blank.ttp");

        try
        {
            var source =
                new OmsiTimetableTrip(
                    path,
                    "TTData\\Blank.ttp",
                    "Blank",
                    "",
                    "",
                    "",
                    "Destino",
                    "54",
                    false,
                    [
                        new OmsiTimetableTripStationType2(
                            1)
                    ],
                    []);

            await File.WriteAllBytesAsync(
                path,
                new OmsiTimetableTripWriter()
                    .Write(source));

            var parsed =
                await new OmsiTimetableTripReader()
                    .ReadAsync(
                        root,
                        path);

            Assert.Equal(
                string.Empty,
                parsed.TrackName);

            Assert.Equal(
                "Destino",
                parsed.Destination);
        }
        finally
        {
            Directory.Delete(
                root,
                recursive: true);
        }
    }
}
