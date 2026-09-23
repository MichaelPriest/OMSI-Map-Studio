using System.Globalization;
using System.Numerics;
using System.Text;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Timetables;

namespace MapStudio.Core.ProtonBus;

public sealed record ProtonBusOmsiTimetableConversionOptions(
    int DefaultPassengerAmount = 0,
    double BusStopRadius = 1.0,
    double MarkerHalfSize = 0.015,
    bool CreateEntrypoints = true);

public sealed record ProtonBusOmsiTimetableIssue(
    string Code,
    string Source,
    string? Detail = null);

public sealed record ProtonBusOmsiTimetableConversionResult(
    IReadOnlyList<ProtonBusBusStopDefinition> BusStops,
    IReadOnlyList<ProtonBusEntrypointDefinition> Entrypoints,
    ProtonBusExportScene MarkerScene,
    IReadOnlyList<ProtonBusOmsiTimetableIssue> Issues);

public static class ProtonBusOmsiTimetableConverter
{
    public static ProtonBusOmsiTimetableConversionResult Convert(
        IReadOnlyList<OmsiTileReference> tileOrder,
        OmsiTimetableCatalog timetable,
        IReadOnlyDictionary<(int X, int Y), OmsiTileContent> tileContents,
        IReadOnlyDictionary<(int X, int Y), ProtonBusOmsiAssetResolutionResult>?
            resolvedAssets = null,
        ProtonBusOmsiTimetableConversionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(tileOrder);
        ArgumentNullException.ThrowIfNull(timetable);
        ArgumentNullException.ThrowIfNull(tileContents);

        options ??=
            new();

        ValidateOptions(
            options);

        var busStops =
            new List<ProtonBusBusStopDefinition>();

        var entrypoints =
            new List<ProtonBusEntrypointDefinition>();

        var markers =
            new List<ProtonBusExportMesh>();

        var issues =
            new List<ProtonBusOmsiTimetableIssue>();

        var resolvedStops =
            new Dictionary<int, ResolvedStop>();

        foreach (
            var stop
            in timetable.BusStops)
        {
            var source =
                $"bus stop {stop.Id} '{stop.Name}'";

            if (
                resolvedStops.ContainsKey(
                    stop.Id))
            {
                issues.Add(
                    new(
                        "duplicateBusStopId",
                        source,
                        $"Bus stop ID {stop.Id} appears more than once in Busstops.cfg."));
                continue;
            }

            if (
                stop.TileIndex <
                    0 ||
                stop.TileIndex >=
                    tileOrder.Count)
            {
                issues.Add(
                    new(
                        "busStopTileIndexOutOfRange",
                        source,
                        $"TileIndex {stop.TileIndex} is outside 0..{Math.Max(0, tileOrder.Count - 1)}."));
                continue;
            }

            var tile =
                tileOrder[
                    stop.TileIndex];

            if (
                !tileContents.TryGetValue(
                    (
                        tile.X,
                        tile.Y
                    ),
                    out var content))
            {
                issues.Add(
                    new(
                        "busStopTileMissing",
                        source,
                        $"Tile {tile.X},{tile.Y} is not loaded for timetable conversion."));
                continue;
            }

            var placedObject =
                content
                    .Objects
                    .FirstOrDefault(
                        candidate =>
                            candidate.ObjectId ==
                            stop.Id);

            if (
                placedObject is null)
            {
                issues.Add(
                    new(
                        "busStopObjectMissing",
                        source,
                        $"Object ID {stop.Id} was not found on tile {tile.X},{tile.Y}."));
                continue;
            }

            var usesAbsoluteHeight =
                TryResolveSceneryAsset(
                    resolvedAssets,
                    tile,
                    placedObject
                        .SceneryObjectPath,
                    out var sceneryAsset) &&
                sceneryAsset!
                    .UsesAbsoluteHeight;

            var terrainOffset =
                usesAbsoluteHeight
                    ? 0.0
                    : ProtonBusOmsiTerrainSampler
                        .GetHeightAtLocalPoint(
                            content.Terrain,
                            placedObject.X,
                            placedObject.Y);

            var position =
                new Vector3(
                    (float)(
                        OmsiTileGrid
                            .GetOriginX(
                                tile.X) +
                        placedObject.X),
                    (float)(
                        placedObject.Z +
                        terrainOffset),
                    (float)(
                        OmsiTileGrid
                            .GetOriginZ(
                                tile.Y) +
                        placedObject.Y));

            var prefix =
                $"bs_{stop.Id}";

            var friendlyName =
                MakePortableDisplayName(
                    stop.Name,
                    $"Stop {stop.Id}");

            if (
                !string.Equals(
                    friendlyName,
                    stop.Name.Trim(),
                    StringComparison.Ordinal))
            {
                issues.Add(
                    new(
                        "busStopNameSanitized",
                        source,
                        $"Export name: '{friendlyName}'."));
            }

            var definition =
                new ProtonBusBusStopDefinition(
                    Prefix:
                        prefix,
                    FriendlyName:
                        friendlyName,
                    PassengerAmount:
                        options
                            .DefaultPassengerAmount,
                    DefaultPassengerRotationY:
                        placedObject
                            .Rotation,
                    IsFirstStop:
                        false,
                    IsLatestStop:
                        false,
                    IsLeft:
                        false,
                    Radius:
                        options
                            .BusStopRadius,
                    MaxPathsToCheck:
                        Math.Max(
                            1,
                            options
                                .DefaultPassengerAmount));

            busStops.Add(
                definition);

            markers.Add(
                ProtonBusMarkerMeshBuilder
                    .Create(
                        definition
                            .TriggerObjectName,
                        position,
                        options
                            .MarkerHalfSize));

            resolvedStops[
                stop.Id] =
                new(
                    stop,
                    tile,
                    placedObject,
                    position);
        }

        if (
            options.CreateEntrypoints)
        {
            CreateEntrypoints(
                timetable,
                resolvedStops,
                entrypoints,
                issues);
        }

        return new(
            busStops.ToArray(),
            entrypoints.ToArray(),
            new(
                markers.ToArray()),
            issues.ToArray());
    }

    private static void CreateEntrypoints(
        OmsiTimetableCatalog timetable,
        IReadOnlyDictionary<int, ResolvedStop> resolvedStops,
        ICollection<ProtonBusEntrypointDefinition> output,
        ICollection<ProtonBusOmsiTimetableIssue> issues)
    {
        var usedNames =
            new Dictionary<string, ProtonBusEntrypointDefinition>(
                StringComparer.OrdinalIgnoreCase);

        foreach (
            var trip
            in timetable.Trips)
        {
            var firstStation =
                trip
                    .Stations
                    .FirstOrDefault();

            if (
                firstStation is null)
            {
                continue;
            }

            if (
                !resolvedStops.TryGetValue(
                    firstStation.Id,
                    out var stop))
            {
                issues.Add(
                    new(
                        "tripFirstStopMissing",
                        $"trip '{trip.Name}'",
                        $"First bus stop ID {firstStation.Id} could not be resolved to a map object."));
                continue;
            }

            var rawName =
                BuildEntrypointDisplayName(
                    trip);

            var portableName =
                MakePortableEntrypointName(
                    rawName,
                    $"Trip {output.Count + 1}");

            if (
                !string.Equals(
                    portableName,
                    rawName.Trim(),
                    StringComparison.Ordinal))
            {
                issues.Add(
                    new(
                        "entrypointNameSanitized",
                        $"trip '{trip.Name}'",
                        $"Export name: '{portableName}'."));
            }

            var rotation =
                new Vector3(
                    0,
                    (float)
                        stop
                            .Object
                            .Rotation,
                    0);

            var candidate =
                new ProtonBusEntrypointDefinition(
                    portableName,
                    stop.Position,
                    rotation);

            if (
                usedNames.TryGetValue(
                    portableName,
                    out var existing))
            {
                if (
                    Vector3.DistanceSquared(
                        existing.Position,
                        candidate.Position) <
                    0.01f)
                {
                    continue;
                }

                portableName =
                    MakeUniqueName(
                        portableName,
                        usedNames.Keys);

                candidate =
                    candidate with
                    {
                        Name =
                            portableName
                    };
            }

            usedNames[
                portableName] =
                candidate;

            output.Add(
                candidate);
        }
    }

    private static string BuildEntrypointDisplayName(
        OmsiTimetableTrip trip)
    {
        var line =
            trip
                .EffectiveLine
                .Trim();

        var destination =
            trip
                .EffectiveDestination
                .Trim();

        var combined =
            string.Join(
                " ",
                new[]
                {
                    line,
                    destination
                }
                .Where(
                    value =>
                        !string
                            .IsNullOrWhiteSpace(
                                value)));

        return
            !string.IsNullOrWhiteSpace(
                combined)
                ? combined
                : trip.Name;
    }

    private static string MakeUniqueName(
        string baseName,
        IEnumerable<string> usedNames)
    {
        var used =
            usedNames.ToHashSet(
                StringComparer.OrdinalIgnoreCase);

        for (
            var suffix = 2;
            suffix <
            10000;
            suffix++)
        {
            var candidate =
                $"{baseName} {suffix}";

            if (
                !used.Contains(
                    candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            "Unable to allocate a unique Proton Bus entrypoint name.");
    }

    private static string MakePortableDisplayName(
        string value,
        string fallback)
    {
        var portable =
            RemoveDiacritics(
                value)
            .Select(
                character =>
                    character >
                        127 ||
                    character is
                        '' or
                        '
' or
                        '='
                        ? ' '
                        : character)
            .ToArray();

        var normalized =
            CollapseSpaces(
                new string(
                    portable));

        return
            string.IsNullOrWhiteSpace(
                normalized)
                ? fallback
                : normalized;
    }

    private static string MakePortableEntrypointName(
        string value,
        string fallback)
    {
        var portable =
            RemoveDiacritics(
                value)
            .Select(
                character =>
                    char.IsAsciiLetterOrDigit(
                        character) ||
                    character is
                        ' ' or
                        '_' or
                        '-'
                        ? character
                        : ' ')
            .ToArray();

        var normalized =
            CollapseSpaces(
                new string(
                    portable));

        return
            string.IsNullOrWhiteSpace(
                normalized)
                ? fallback
                : normalized;
    }

    private static string RemoveDiacritics(
        string value)
    {
        var normalized =
            value
                .Normalize(
                    NormalizationForm.FormD);

        var builder =
            new StringBuilder(
                normalized.Length);

        foreach (
            var character
            in normalized)
        {
            var category =
                CharUnicodeInfo
                    .GetUnicodeCategory(
                        character);

            if (
                category !=
                UnicodeCategory
                    .NonSpacingMark)
            {
                builder.Append(
                    character);
            }
        }

        return builder
            .ToString()
            .Normalize(
                NormalizationForm.FormC);
    }

    private static string CollapseSpaces(
        string value) =>
        string.Join(
            " ",
            value
                .Split(
                    ' ',
                    StringSplitOptions
                        .RemoveEmptyEntries |
                    StringSplitOptions
                        .TrimEntries));

    private static bool TryResolveSceneryAsset(
        IReadOnlyDictionary<(int X, int Y), ProtonBusOmsiAssetResolutionResult>?
            assetsByTile,
        OmsiTileReference tile,
        string sceneryPath,
        out ProtonBusResolvedSceneryAsset? asset)
    {
        asset =
            null;

        if (
            assetsByTile is null ||
            !assetsByTile.TryGetValue(
                (
                    tile.X,
                    tile.Y
                ),
                out var tileAssets))
        {
            return false;
        }

        if (
            tileAssets
                .SceneryAssets
                .TryGetValue(
                    sceneryPath,
                    out asset))
        {
            return true;
        }

        var normalized =
            NormalizePath(
                sceneryPath);

        foreach (
            var pair
            in tileAssets
                .SceneryAssets)
        {
            if (
                string.Equals(
                    NormalizePath(
                        pair.Key),
                    normalized,
                    StringComparison.OrdinalIgnoreCase))
            {
                asset =
                    pair.Value;

                return true;
            }
        }

        return false;
    }

    private static string NormalizePath(
        string value) =>
        value
            .Trim()
            .Replace(
                '\\',
                '/');

    private static void ValidateOptions(
        ProtonBusOmsiTimetableConversionOptions options)
    {
        if (
            options
                .DefaultPassengerAmount <
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Default passenger amount cannot be negative.");
        }

        if (
            !double.IsFinite(
                options.BusStopRadius) ||
            options.BusStopRadius <
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Bus stop radius must be finite and non-negative.");
        }

        if (
            !double.IsFinite(
                options.MarkerHalfSize) ||
            options.MarkerHalfSize <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Marker size must be finite and greater than zero.");
        }
    }

    private sealed record ResolvedStop(
        OmsiTimetableBusStop Stop,
        OmsiTileReference Tile,
        OmsiPlacedObject Object,
        Vector3 Position);
}
