using MapStudio.Core.Omsi.Timetables;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiStationLinkEntryResolverTests
{
    [Fact]
    public void PrefersExistingStationLinkMetadata()
    {
        var linkEntry =
            new OmsiStationLinkEntry(
                "3:",
                77,
                "2",
                5,
                123.4,
                "A",
                "B",
                "C",
                ["Chrono"]);

        var catalog =
            new OmsiTimetableCatalog(
                [],
                [])
            {
                StationLinks =
                [
                    new OmsiStationLink(
                        "link",
                        "0",
                        1,
                        2,
                        "0",
                        "0",
                        "0",
                        "0",
                        "0",
                        "0",
                        [linkEntry])
                ]
            };

        Assert.True(
            OmsiStationLinkEntryResolver
                .TryCreateFromKnownMetadata(
                    catalog,
                    77,
                    "2",
                    out var resolved));

        Assert.Equal(
            linkEntry.TileIndex,
            resolved.TileIndex);

        Assert.Equal(
            linkEntry.Length,
            resolved.Length);

        Assert.Equal(
            linkEntry.Line5,
            resolved.Line5);

        Assert.Empty(
            resolved.ChronoFiles);
    }

    [Fact]
    public void ConvertsKnownTrackMetadataWhenNoStationLinkTemplateExists()
    {
        var trackEntry =
            new OmsiTimetableTrackEntry(
                "0:",
                90,
                "4",
                8,
                "L4",
                55.5,
                "L6",
                "L7");

        var catalog =
            new OmsiTimetableCatalog(
                [
                    new OmsiTimetableTrack(
                        "track.ttr",
                        "TTData/track.ttr",
                        "track",
                        "",
                        "",
                        [trackEntry])
                ],
                []);

        Assert.True(
            OmsiStationLinkEntryResolver
                .TryCreateFromKnownMetadata(
                    catalog,
                    90,
                    "4",
                    out var resolved));

        Assert.Equal(
            trackEntry.TileIndex,
            resolved.TileIndex);

        Assert.Equal(
            trackEntry.Length,
            resolved.Length);

        Assert.Equal(
            trackEntry.Line4,
            resolved.Line5);

        Assert.Equal(
            trackEntry.Line6,
            resolved.Line6);

        Assert.Equal(
            trackEntry.Line7,
            resolved.Line7);
    }

    [Fact]
    public void RefusesUnknownMetadata()
    {
        var catalog =
            new OmsiTimetableCatalog(
                [],
                []);

        Assert.False(
            OmsiStationLinkEntryResolver
                .TryCreateFromKnownMetadata(
                    catalog,
                    999,
                    "0",
                    out _));
    }
}
