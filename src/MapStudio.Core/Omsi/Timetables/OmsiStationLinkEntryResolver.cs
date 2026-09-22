namespace MapStudio.Core.Omsi.Timetables;

public static class OmsiStationLinkEntryResolver
{
    public static bool TryCreateFromKnownMetadata(
        OmsiTimetableCatalog catalog,
        int entityId,
        string pathIndex,
        out OmsiStationLinkEntry entry)
    {
        ArgumentNullException.ThrowIfNull(
            catalog);

        entry =
            null!;

        var stationLinkTemplate =
            catalog
                .StationLinks
                .SelectMany(
                    link =>
                        link.Entries)
                .FirstOrDefault(
                    candidate =>
                        candidate.Id ==
                            entityId &&
                        string.Equals(
                            candidate.Line2,
                            pathIndex,
                            StringComparison.Ordinal));

        if (stationLinkTemplate is not null)
        {
            entry =
                stationLinkTemplate with
                {
                    ChronoFiles =
                        Array.Empty<string>()
                };

            return true;
        }

        var trackTemplate =
            catalog
                .Tracks
                .SelectMany(
                    track =>
                        track.Entries)
                .FirstOrDefault(
                    candidate =>
                        candidate.Id ==
                            entityId &&
                        string.Equals(
                            candidate.Line2,
                            pathIndex,
                            StringComparison.Ordinal));

        if (trackTemplate is null)
        {
            return false;
        }

        entry =
            new OmsiStationLinkEntry(
                string.Empty,
                trackTemplate.Id,
                trackTemplate.Line2,
                trackTemplate.TileIndex,
                trackTemplate.Length,
                trackTemplate.Line4,
                trackTemplate.Line6,
                trackTemplate.Line7 ??
                    string.Empty,
                Array.Empty<string>());

        return true;
    }
}
