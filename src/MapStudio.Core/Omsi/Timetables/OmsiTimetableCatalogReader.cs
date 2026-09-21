namespace MapStudio.Core.Omsi.Timetables;

public sealed class OmsiTimetableCatalogReader
{
    public async Task<OmsiTimetableCatalog> ReadAsync(
        string mapDirectory,
        CancellationToken cancellationToken =
            default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            mapDirectory);

        var fullMapDirectory =
            Path.GetFullPath(
                mapDirectory);

        var ttDataPath =
            Path.Combine(
                fullMapDirectory,
                "TTData");

        if (!Directory.Exists(
                ttDataPath))
        {
            return new OmsiTimetableCatalog(
                Array.Empty<OmsiTimetableTrack>(),
                Array.Empty<OmsiTimetableTrip>());
        }

        var trackReader =
            new OmsiTimetableTrackReader();

        var tripReader =
            new OmsiTimetableTripReader();

        var tracks =
            new List<OmsiTimetableTrack>();

        foreach (
            var path in Directory
                .EnumerateFiles(
                    ttDataPath,
                    "*.ttr",
                    SearchOption
                        .TopDirectoryOnly)
                .OrderBy(
                    path => path,
                    StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            try
            {
                tracks.Add(
                    await trackReader
                        .ReadAsync(
                            fullMapDirectory,
                            path,
                            cancellationToken)
                        .ConfigureAwait(false));
            }
            catch (
                InvalidDataException)
            {
            }
        }

        var trips =
            new List<OmsiTimetableTrip>();

        foreach (
            var path in Directory
                .EnumerateFiles(
                    ttDataPath,
                    "*.ttp",
                    SearchOption
                        .TopDirectoryOnly)
                .OrderBy(
                    path => path,
                    StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            try
            {
                trips.Add(
                    await tripReader
                        .ReadAsync(
                            fullMapDirectory,
                            path,
                            cancellationToken)
                        .ConfigureAwait(false));
            }
            catch (
                InvalidDataException)
            {
            }
        }

        var busStopsPath =
            Path.Combine(
                ttDataPath,
                "Busstops.cfg");

        var stationLinksPath =
            Path.Combine(
                ttDataPath,
                "StnLinks.cfg");

        var busStops =
            File.Exists(
                busStopsPath)
                ? await new OmsiTimetableBusStopReader()
                    .ReadAsync(
                        busStopsPath,
                        cancellationToken)
                    .ConfigureAwait(false)
                : Array.Empty<
                    OmsiTimetableBusStop>();

        var stationLinks =
            File.Exists(
                stationLinksPath)
                ? await new OmsiStationLinkReader()
                    .ReadAsync(
                        stationLinksPath,
                        cancellationToken)
                    .ConfigureAwait(false)
                : Array.Empty<
                    OmsiStationLink>();

        return new OmsiTimetableCatalog(
            tracks,
            trips)
        {
            BusStops =
                busStops,
            StationLinks =
                stationLinks
        };
    }
}
