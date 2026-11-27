using MapStudio.Core.Omsi.Timetables;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiStationLinkWriterTests
{
    [Fact]
    public async Task WriteRoundTripsLinksEntriesAndChrono()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-stn-writer-" +
                Guid.NewGuid()
                    .ToString("N"));

        Directory.CreateDirectory(
            root);

        var path =
            Path.Combine(
                root,
                "StnLinks.cfg");

        try
        {
            var source =
                new[]
                {
                    new OmsiStationLink(
                        "Terminal A ==> Terminal B",
                        "125.5",
                        10,
                        20,
                        "1.25",
                        "1.50",
                        "8",
                        "12",
                        "0",
                        "2",
                        [
                            new OmsiStationLinkEntry(
                                "0:",
                                100,
                                "2",
                                3,
                                25,
                                "-1",
                                "0",
                                "0",
                                [
                                    "Chrono\\EventA.txt"
                                ]),
                            new OmsiStationLinkEntry(
                                "1:",
                                101,
                                "4",
                                3,
                                0,
                                "-1",
                                "0",
                                "0",
                                [])
                        ])
                };

            await File.WriteAllBytesAsync(
                path,
                new OmsiStationLinkWriter()
                    .Write(source));

            var parsed =
                await new OmsiStationLinkReader()
                    .ReadAsync(
                        path);

            var link =
                Assert.Single(
                    parsed);

            Assert.Equal(
                10,
                link.StartBusStopId);

            Assert.Equal(
                20,
                link.EndBusStopId);

            Assert.Equal(
                2,
                link.Entries.Count);

            Assert.Equal(
                100,
                link.Entries[0].Id);

            Assert.Equal(
                "2",
                link.Entries[0].Line2);

            Assert.Equal(
                25,
                link.Entries[0].Length);

            Assert.Equal(
                [
                    "Chrono\\EventA.txt"
                ],
                link.Entries[0]
                    .ChronoFiles);
        }
        finally
        {
            Directory.Delete(
                root,
                recursive: true);
        }
    }
}
