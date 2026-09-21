using MapStudio.Core.Omsi.Timetables;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiTimetableBusStopWriterTests
{
    [Fact]
    public async Task WriteRoundTripsBusStopsIncludingBlankSubName()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-busstop-writer-" +
                Guid.NewGuid()
                    .ToString("N"));

        Directory.CreateDirectory(
            root);

        var path =
            Path.Combine(
                root,
                "Busstops.cfg");

        try
        {
            var source =
                new[]
                {
                    new OmsiTimetableBusStop(
                        "Terminal",
                        3,
                        100,
                        12.5,
                        "0",
                        "0",
                        string.Empty),
                    new OmsiTimetableBusStop(
                        "Avenida",
                        4,
                        101,
                        0,
                        "1",
                        "2",
                        "Plataforma B")
                };

            await File.WriteAllBytesAsync(
                path,
                new OmsiTimetableBusStopWriter()
                    .Write(source));

            var parsed =
                await new OmsiTimetableBusStopReader()
                    .ReadAsync(
                        path);

            Assert.Equal(
                2,
                parsed.Count);

            Assert.Equal(
                "Terminal",
                parsed[0].Name);

            Assert.Equal(
                string.Empty,
                parsed[0].SubName);

            Assert.Equal(
                "Plataforma B",
                parsed[1].SubName);
        }
        finally
        {
            Directory.Delete(
                root,
                recursive: true);
        }
    }
}
