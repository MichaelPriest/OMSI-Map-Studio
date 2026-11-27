using System.Text;
using MapStudio.Core.Omsi.Timetables;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiTimetableTrackWriterTests
{
    [Fact]
    public async Task WriteRoundTripsExtendedEntries()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-ttr-write-" +
                Guid.NewGuid()
                    .ToString("N"));

        Directory.CreateDirectory(
            root);

        try
        {
            var path =
                Path.Combine(
                    root,
                    "Track.ttr");

            var source =
                new OmsiTimetableTrack(
                    path,
                    "Track.ttr",
                    "Track",
                    "Line 100",
                    "Outbound",
                    []);

            var bytes =
                new OmsiTimetableTrackWriter()
                    .Write(
                        source,
                        [
                            new OmsiTimetableTrackEntry(
                                "Segment A",
                                77,
                                "0",
                                5,
                                "0",
                                125.5,
                                "0",
                                null)
                        ]);

            await File.WriteAllBytesAsync(
                path,
                bytes);

            var read =
                await new OmsiTimetableTrackReader()
                    .ReadAsync(
                        root,
                        path);

            var entry =
                Assert.Single(
                    read.Entries);

            Assert.Equal(
                77,
                entry.Id);

            Assert.Equal(
                "0",
                entry.Line2);

            Assert.Equal(
                5,
                entry.TileIndex);

            Assert.Equal(
                125.5,
                entry.Length);
        }
        finally
        {
            Directory.Delete(
                root,
                recursive: true);
        }
    }

    [Fact]
    public async Task WriteRoundTripsCompactEntries()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-ttr-compact-" +
                Guid.NewGuid()
                    .ToString("N"));

        Directory.CreateDirectory(
            root);

        try
        {
            var path =
                Path.Combine(
                    root,
                    "Compact.ttr");

            var source =
                new OmsiTimetableTrack(
                    path,
                    "Compact.ttr",
                    "Compact",
                    "Created",
                    "Date",
                    []);

            var bytes =
                new OmsiTimetableTrackWriter()
                    .Write(
                        source,
                        [
                            new OmsiTimetableTrackEntry(
                                "0:",
                                156078,
                                "1",
                                -1,
                                string.Empty,
                                null,
                                string.Empty,
                                null),
                            new OmsiTimetableTrackEntry(
                                "1:",
                                191432,
                                "0",
                                -1,
                                string.Empty,
                                null,
                                string.Empty,
                                null)
                        ]);

            await File.WriteAllBytesAsync(
                path,
                bytes);

            var read =
                await new OmsiTimetableTrackReader()
                    .ReadAsync(
                        root,
                        path);

            Assert.Equal(
                2,
                read.Entries.Count);

            Assert.Equal(
                156078,
                read.Entries[0].Id);

            Assert.Equal(
                "1",
                read.Entries[0].Line2);

            Assert.Equal(
                -1,
                read.Entries[0].TileIndex);
        }
        finally
        {
            Directory.Delete(
                root,
                recursive: true);
        }
    }
}
