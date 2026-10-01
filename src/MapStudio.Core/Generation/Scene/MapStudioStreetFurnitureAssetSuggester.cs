using System.Globalization;
using System.Text;
using MapStudio.Core.Generation.Scene;
using MapStudio.Core.Omsi.Indexing;

namespace MapStudio.Core.Generation.Scene;

public sealed class MapStudioStreetFurnitureAssetSuggester
{
    public OmsiAssetIndexEntry? Suggest(
        IReadOnlyList<OmsiAssetIndexEntry> assets,
        MapStudioGeoStreetFurniturePoint point)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(point);

        var candidates =
            assets
                .Where(
                    asset =>
                        asset.Kind ==
                            OmsiAssetKind.SceneryObject)
                .Where(
                    asset =>
                        IsAllowedGroup(
                            OmsiAssetLibraryClassifier
                                .Classify(asset),
                            point.Kind))
                .Where(
                    asset =>
                        HasKindToken(
                            asset.RelativePath,
                            point.Kind) ||
                        OriginalRoleMatches(
                            MapStudioOriginalOmsiAssetCatalog
                                .Classify(
                                    asset),
                            point.Kind))
                .Select(
                    asset =>
                        (
                            Asset: asset,
                            Score:
                                Score(
                                    asset,
                                    point)
                        ))
                .OrderByDescending(
                    item =>
                        item.Score)
                .ThenBy(
                    item =>
                        item.Asset.RelativePath,
                    StringComparer
                        .CurrentCultureIgnoreCase)
                .ToArray();

        return candidates.Length == 0
            ? null
            : candidates[0].Asset;
    }

    private static bool IsAllowedGroup(
        OmsiAssetLibraryGroup group,
        MapStudioOsmStreetFurnitureKind kind) =>
        kind switch
        {
            MapStudioOsmStreetFurnitureKind.BusShelter =>
                group is
                    OmsiAssetLibraryGroup.Transit or
                    OmsiAssetLibraryGroup.StreetFurniture,
            MapStudioOsmStreetFurnitureKind.UtilityPole =>
                group is
                    OmsiAssetLibraryGroup.Utilities or
                    OmsiAssetLibraryGroup.StreetFurniture,
            MapStudioOsmStreetFurnitureKind.StreetLight =>
                group is
                    OmsiAssetLibraryGroup.StreetFurniture or
                    OmsiAssetLibraryGroup.Utilities,
            MapStudioOsmStreetFurnitureKind.TrafficSign =>
                group ==
                    OmsiAssetLibraryGroup.StreetFurniture,
            MapStudioOsmStreetFurnitureKind.Bench or
            MapStudioOsmStreetFurnitureKind.WasteBasket or
            MapStudioOsmStreetFurnitureKind.Bollard =>
                group ==
                    OmsiAssetLibraryGroup.StreetFurniture,
            MapStudioOsmStreetFurnitureKind.FireHydrant =>
                group is
                    OmsiAssetLibraryGroup.Utilities or
                    OmsiAssetLibraryGroup.StreetFurniture,
            MapStudioOsmStreetFurnitureKind.TrafficSignal =>
                group ==
                    OmsiAssetLibraryGroup.StreetFurniture,
            MapStudioOsmStreetFurnitureKind.Crosswalk =>
                group is
                    OmsiAssetLibraryGroup.StreetFurniture or
                    OmsiAssetLibraryGroup.Junctions,
            _ =>
                false
        };

    private static int Score(
        OmsiAssetIndexEntry asset,
        MapStudioGeoStreetFurniturePoint point)
    {
        var normalized =
            Normalize(
                asset.RelativePath);

        var originalRole =
            MapStudioOriginalOmsiAssetCatalog
                .Classify(
                    asset);

        var score =
            20 +
            (
                OriginalRoleMatches(
                    originalRole,
                    point.Kind)
                    ? 24
                    : originalRole !=
                        MapStudioOriginalOmsiAssetRole.None
                        ? 4
                        : 0
            );

        foreach (
            var token in
                GetKindTokens(
                    point.Kind))
        {
            if (
                ContainsToken(
                    normalized,
                    token))
            {
                score +=
                    8;
            }
        }

        score +=
            ScoreMetadata(
                normalized,
                point.Reference,
                12);

        score +=
            ScoreMetadata(
                normalized,
                point.ShelterType,
                8);

        score +=
            ScoreMetadata(
                normalized,
                point.Operator,
                5);

        score +=
            ScoreMetadata(
                normalized,
                point.Name,
                3);

        return score;
    }

    private static int ScoreMetadata(
        string normalizedPath,
        string? value,
        int weight)
    {
        if (
            string.IsNullOrWhiteSpace(
                value))
        {
            return 0;
        }

        var total =
            0;

        foreach (
            var token in
                Tokenize(value))
        {
            if (
                token.Length >= 2 &&
                ContainsToken(
                    normalizedPath,
                    token))
            {
                total +=
                    weight;
            }
        }

        return total;
    }

    private static bool HasKindToken(
        string path,
        MapStudioOsmStreetFurnitureKind kind)
    {
        var normalized =
            Normalize(path);

        return GetKindTokens(kind)
            .Any(
                token =>
                    ContainsToken(
                        normalized,
                        token));
    }

    private static IReadOnlyList<string> GetKindTokens(
        MapStudioOsmStreetFurnitureKind kind) =>
        kind switch
        {
            MapStudioOsmStreetFurnitureKind.StreetLight =>
                [
                    "lamp",
                    "light",
                    "streetlight",
                    "street lamp",
                    "lantern",
                    "strlt",
                    "peitschenleuchte",
                    "gaslight",
                    "neonlight",
                    "sodiumlight",
                    "poste"
                ],
            MapStudioOsmStreetFurnitureKind.UtilityPole =>
                [
                    "pole",
                    "poste",
                    "power",
                    "utility",
                    "tower"
                ],
            MapStudioOsmStreetFurnitureKind.TrafficSign =>
                [
                    "sign",
                    "schild",
                    "verkehrszeichen",
                    "zeichen",
                    "traffic",
                    "placa"
                ],
            MapStudioOsmStreetFurnitureKind.BusShelter =>
                [
                    "busstop",
                    "bus stop",
                    "haltestelle",
                    "shelter",
                    "wartehaus",
                    "busbahnsteig",
                    "hst",
                    "abrigo",
                    "ponto"
                ],
            MapStudioOsmStreetFurnitureKind.Bench =>
                [
                    "bench",
                    "bank",
                    "banco"
                ],
            MapStudioOsmStreetFurnitureKind.WasteBasket =>
                [
                    "bin",
                    "trash",
                    "waste",
                    "basket",
                    "muelleinmer",
                    "lixeira"
                ],
            MapStudioOsmStreetFurnitureKind.Bollard =>
                [
                    "bollard",
                    "balizador",
                    "postelete"
                ],
            MapStudioOsmStreetFurnitureKind.FireHydrant =>
                [
                    "hydrant",
                    "fire hydrant",
                    "hidrante"
                ],
            MapStudioOsmStreetFurnitureKind.TrafficSignal =>
                [
                    "trafficlight",
                    "traffic light",
                    "traffic signal",
                    "ampel",
                    "semaforo",
                    "signalgeber"
                ],
            MapStudioOsmStreetFurnitureKind.Crosswalk =>
                [
                    "crosswalk",
                    "zebra",
                    "zebrastreifen",
                    "pedestrian crossing",
                    "faixa pedestre",
                    "travessia"
                ],
            _ =>
                Array.Empty<string>()
        };

    private static bool OriginalRoleMatches(
        MapStudioOriginalOmsiAssetRole role,
        MapStudioOsmStreetFurnitureKind kind) =>
        kind switch
        {
            MapStudioOsmStreetFurnitureKind.BusShelter =>
                role is
                    MapStudioOriginalOmsiAssetRole.BusStopObject or
                    MapStudioOriginalOmsiAssetRole.BusShelterObject,
            MapStudioOsmStreetFurnitureKind.StreetLight =>
                role ==
                    MapStudioOriginalOmsiAssetRole.StreetLightObject,
            MapStudioOsmStreetFurnitureKind.TrafficSign =>
                role ==
                    MapStudioOriginalOmsiAssetRole.TrafficSignObject,
            MapStudioOsmStreetFurnitureKind.Bench =>
                role ==
                    MapStudioOriginalOmsiAssetRole.BenchObject,
            MapStudioOsmStreetFurnitureKind.WasteBasket =>
                role ==
                    MapStudioOriginalOmsiAssetRole.WasteBasketObject,
            MapStudioOsmStreetFurnitureKind.Bollard =>
                role ==
                    MapStudioOriginalOmsiAssetRole.BollardObject,
            MapStudioOsmStreetFurnitureKind.TrafficSignal =>
                role ==
                    MapStudioOriginalOmsiAssetRole.TrafficSignalObject,
            MapStudioOsmStreetFurnitureKind.Crosswalk =>
                role ==
                    MapStudioOriginalOmsiAssetRole.CrosswalkObject,
            MapStudioOsmStreetFurnitureKind.UtilityPole =>
                role ==
                    MapStudioOriginalOmsiAssetRole.UtilityPoleObject,
            _ =>
                false
        };

    private static IEnumerable<string> Tokenize(
        string value) =>
        Normalize(value)
            .Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .Distinct(
                StringComparer.Ordinal);

    private static bool ContainsToken(
        string normalizedText,
        string token)
    {
        var normalizedToken =
            Normalize(token);

        return normalizedText.Contains(
            normalizedToken,
            StringComparison.Ordinal);
    }

    private static string Normalize(
        string value)
    {
        var decomposed =
            value
                .ToLowerInvariant()
                .Normalize(
                    NormalizationForm.FormD);

        var builder =
            new StringBuilder(
                decomposed.Length);

        foreach (var character in decomposed)
        {
            var category =
                CharUnicodeInfo.GetUnicodeCategory(
                    character);

            builder.Append(
                category ==
                    UnicodeCategory.NonSpacingMark
                    ? ' '
                    : char.IsLetterOrDigit(character)
                        ? character
                        : ' ');
        }

        return string.Join(
            " ",
            builder
                .ToString()
                .Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries));
    }
}
