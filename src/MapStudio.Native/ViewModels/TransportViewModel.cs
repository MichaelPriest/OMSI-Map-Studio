using System.Globalization;
using MapStudio.Core.Omsi.Timetables;

namespace MapStudio.Native.ViewModels;

public sealed record TransportExplorerItem(
    string Kind,
    string Key,
    string DisplayText,
    string Detail);

public sealed class TransportViewModel
{
    public IReadOnlyList<
        TransportExplorerItem>
        Items
    {
        get;
        private set;
    } =
        Array.Empty<
            TransportExplorerItem>();

    public void Load(
        OmsiTimetableCatalog? catalog,
        int kindIndex)
    {
        if (catalog is null)
        {
            Items =
                Array.Empty<
                    TransportExplorerItem>();

            return;
        }

        Items =
            kindIndex switch
            {
                1 =>
                    catalog.Trips
                        .Select(
                            trip =>
                                new TransportExplorerItem(
                                    "Trip",
                                    trip.Name,
                                    $"{trip.Name} · linha {trip.EffectiveLine} → {trip.EffectiveDestination}",
                                    (
                                        trip.UsesStationLinks
                                            ? "Rota: StationLinks (tipo 2)\n"
                                            : $"Track: {trip.EffectiveTrackName}\n"
                                    ) +
                                    $"Estações: {trip.Stations.Count}\n" +
                                    $"Train reverse: {(trip.TrainReverse ? "sim" : "não")}\n" +
                                    $"Arquivo: {trip.RelativePath}"))
                        .ToArray(),

                2 =>
                    catalog.BusStops
                        .Select(
                            (stop, index) =>
                                new TransportExplorerItem(
                                    "Stop",
                                    index.ToString(
                                        CultureInfo.InvariantCulture),
                                    $"{stop.Id} · {stop.Name}",
                                    $"Índice: {index}\n" +
                                    $"Tile index: {stop.TileIndex}\n" +
                                    $"Subnome: {stop.SubName}"))
                        .ToArray(),

                3 =>
                    catalog.StationLinks
                        .Select(
                            (link, index) =>
                                new TransportExplorerItem(
                                    "StationLink",
                                    index.ToString(
                                        CultureInfo.InvariantCulture),
                                    $"{link.StartBusStopId} → {link.EndBusStopId} · {link.Comment}",
                                    $"Índice: {index}\n" +
                                    $"Entradas: {link.Entries.Count}\n" +
                                    $"Comprimento/ref: {link.Line1}"))
                        .ToArray(),

                4 =>
                    catalog.Lines
                        .Select(
                            line =>
                                new TransportExplorerItem(
                                    "Line",
                                    line.Name,
                                    $"{line.Name} · {line.Tours.Count} tour(s)",
                                    $"Arquivo: {line.RelativePath}\n" +
                                    $"Prioridade: {line.Priority}\n" +
                                    $"Jogador permitido: {(line.UserAllowed ? "sim" : "não")}\n" +
                                    $"Trips agendados: {line.Tours.Sum(tour => tour.Trips.Count)}"))
                        .ToArray(),

                _ =>
                    catalog.Tracks
                        .Select(
                            track =>
                                new TransportExplorerItem(
                                    "Track",
                                    track.Name,
                                    $"{track.Name} · {track.Entries.Count} segmentos",
                                    $"Arquivo: {track.RelativePath}\n" +
                                    $"Comentário: {track.Comment1} {track.Comment2}"))
                        .ToArray()
            };
    }

    public IReadOnlyList<
        TransportExplorerItem>
        Filter(
            string? query)
    {
        var normalized =
            query?.Trim() ??
            string.Empty;

        if (
            string.IsNullOrWhiteSpace(
                normalized))
        {
            return Items;
        }

        return Items
            .Where(
                item =>
                    item.DisplayText.Contains(
                        normalized,
                        StringComparison.OrdinalIgnoreCase) ||
                    item.Detail.Contains(
                        normalized,
                        StringComparison.OrdinalIgnoreCase) ||
                    item.Key.Contains(
                        normalized,
                        StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }
}
