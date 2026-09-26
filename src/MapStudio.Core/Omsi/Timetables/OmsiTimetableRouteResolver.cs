using System.Globalization;

namespace MapStudio.Core.Omsi.Timetables;

public sealed record OmsiTimetableRouteStep(
    int Sequence,
    int EntityId,
    string PathIndex,
    double? Length,
    string SourceLabel);

public static class OmsiTimetableRouteResolver
{
    public static IReadOnlyList<
        OmsiTimetableRouteStep>
        Resolve(
            OmsiTimetableCatalog catalog,
            string kind,
            string key)
    {
        ArgumentNullException.ThrowIfNull(
            catalog);

        var result =
            new List<
                OmsiTimetableRouteStep>();

        void AddTrackEntries(
            IEnumerable<
                OmsiTimetableTrackEntry> entries,
            string sourceLabel)
        {
            foreach (
                var entry in
                    entries)
            {
                result.Add(
                    new OmsiTimetableRouteStep(
                        result.Count +
                            1,
                        entry.Id,
                        entry.Line2,
                        entry.Length,
                        sourceLabel));
            }
        }

        void AddStationLinkEntries(
            IEnumerable<
                OmsiStationLinkEntry> entries,
            string sourceLabel)
        {
            foreach (
                var entry in
                    entries)
            {
                result.Add(
                    new OmsiTimetableRouteStep(
                        result.Count +
                            1,
                        entry.Id,
                        entry.Line2,
                        entry.Length,
                        sourceLabel));
            }
        }

        void AddTripEntries(
            OmsiTimetableTrip trip,
            string sourcePrefix)
        {
            if (trip.UsesStationLinks)
            {
                var stations =
                    trip.Stations
                        .OfType<
                            OmsiTimetableTripStationType2>()
                        .ToArray();

                for (
                    var index = 0;
                    index <
                        stations.Length -
                            1;
                    index++)
                {
                    var start =
                        stations[index]
                            .Id;

                    var end =
                        stations[index + 1]
                            .Id;

                    var link =
                        catalog
                            .StationLinks
                            .FirstOrDefault(
                                candidate =>
                                    candidate
                                        .StartBusStopId ==
                                        start &&
                                    candidate
                                        .EndBusStopId ==
                                        end);

                    if (link is null)
                    {
                        continue;
                    }

                    AddStationLinkEntries(
                        link.Entries,
                        $"{sourcePrefix} · StationLink {start}→{end}");
                }

                return;
            }

            var track =
                catalog.Tracks
                    .FirstOrDefault(
                        candidate =>
                            string.Equals(
                                candidate.Name,
                                trip.EffectiveTrackName,
                                StringComparison
                                    .OrdinalIgnoreCase));

            if (track is not null)
            {
                AddTrackEntries(
                    track.Entries,
                    $"{sourcePrefix} → Track {track.Name}");
            }
        }

        if (
            string.Equals(
                kind,
                "Track",
                StringComparison.OrdinalIgnoreCase))
        {
            var track =
                catalog.Tracks
                    .FirstOrDefault(
                        candidate =>
                            string.Equals(
                                candidate.Name,
                                key,
                                StringComparison
                                    .OrdinalIgnoreCase));

            if (track is not null)
            {
                AddTrackEntries(
                    track.Entries,
                    $"Track {track.Name}");
            }

            return result;
        }

        if (
            string.Equals(
                kind,
                "Trip",
                StringComparison.OrdinalIgnoreCase))
        {
            var trip =
                catalog.Trips
                    .FirstOrDefault(
                        candidate =>
                            string.Equals(
                                candidate.Name,
                                key,
                                StringComparison
                                    .OrdinalIgnoreCase));

            if (trip is not null)
            {
                AddTripEntries(
                    trip,
                    $"Trip {trip.Name}");
            }

            return result;
        }

        if (
            string.Equals(
                kind,
                "StationLink",
                StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(
                key,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var linkIndex) &&
            linkIndex >= 0 &&
            linkIndex <
                catalog
                    .StationLinks
                    .Count)
        {
            var link =
                catalog
                    .StationLinks[
                        linkIndex];

            AddStationLinkEntries(
                link.Entries,
                $"StationLink {link.StartBusStopId}→{link.EndBusStopId}");

            return result;
        }

        if (
            !string.Equals(
                kind,
                "Line",
                StringComparison.OrdinalIgnoreCase))
        {
            return result;
        }

        var line =
            catalog.Lines
                .FirstOrDefault(
                    candidate =>
                        string.Equals(
                            candidate.Name,
                            key,
                            StringComparison
                                .OrdinalIgnoreCase));

        if (line is null)
        {
            return result;
        }

        foreach (
            var tour in
                line.Tours)
        {
            foreach (
                var scheduledTrip in
                    tour.Trips)
            {
                var trip =
                    catalog.Trips
                        .FirstOrDefault(
                            candidate =>
                                string.Equals(
                                    candidate.Name,
                                    scheduledTrip
                                        .TripName,
                                    StringComparison
                                        .OrdinalIgnoreCase));

                if (trip is null)
                {
                    continue;
                }

                AddTripEntries(
                    trip,
                    $"{tour.Name} · {trip.Name}");
            }
        }

        return result;
    }
}
