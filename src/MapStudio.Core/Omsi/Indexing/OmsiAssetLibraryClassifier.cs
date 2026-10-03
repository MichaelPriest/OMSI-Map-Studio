using System.Globalization;
using System.Text;

namespace MapStudio.Core.Omsi.Indexing;

public static class OmsiAssetLibraryClassifier
{
    private static readonly IReadOnlyDictionary<
        string,
        string[]> SearchSynonyms =
            new Dictionary<
                string,
                string[]>(
                    StringComparer.OrdinalIgnoreCase)
            {
                ["rua"] =
                    [
                        "rua",
                        "road",
                        "street",
                        "strasse",
                        "straße"
                    ],
                ["avenida"] =
                    [
                        "avenida",
                        "avenue",
                        "allee",
                        "boulevard"
                    ],
                ["arvore"] =
                    [
                        "arvore",
                        "árvore",
                        "tree",
                        "baum"
                    ],
                ["casa"] =
                    [
                        "casa",
                        "house",
                        "haus",
                        "wohn"
                    ],
                ["predio"] =
                    [
                        "predio",
                        "prédio",
                        "building",
                        "gebaeude",
                        "gebäude"
                    ],
                ["ponte"] =
                    [
                        "ponte",
                        "bridge",
                        "bruecke",
                        "brücke"
                    ],
                ["cruzamento"] =
                    [
                        "cruzamento",
                        "junction",
                        "intersection",
                        "kreuzung",
                        "kreuz",
                        "einm",
                        "wende"
                    ],
                ["calcada"] =
                    [
                        "calcada",
                        "calçada",
                        "sidewalk",
                        "gehweg",
                        "sdwk"
                    ],
                ["trilho"] =
                    [
                        "trilho",
                        "rail",
                        "track",
                        "gleis",
                        "tram"
                    ],
                ["poste"] =
                    [
                        "poste",
                        "pole",
                        "lamp",
                        "light"
                    ],
                ["ponto"] =
                    [
                        "ponto",
                        "busstop",
                        "bus stop",
                        "haltestelle",
                        "hst",
                        "wartehaus"
                    ],
                ["semaforo"] =
                    [
                        "semaforo",
                        "semáforo",
                        "trafficlight",
                        "traffic light",
                        "traffic signal",
                        "ampel",
                        "signalgeber"
                    ],
                ["faixa"] =
                    [
                        "faixa",
                        "crosswalk",
                        "zebra",
                        "zebrastreifen",
                        "pedestrian crossing",
                        "travessia"
                    ]
            };

    public static IReadOnlyList<
        OmsiAssetLibraryGroup>
        GetGroupsForKind(
            OmsiAssetKind kind) =>
        kind switch
        {
            OmsiAssetKind.SceneryObject =>
                [
                    OmsiAssetLibraryGroup.All,
                    OmsiAssetLibraryGroup.Junctions,
                    OmsiAssetLibraryGroup.Bridges,
                    OmsiAssetLibraryGroup.Buildings,
                    OmsiAssetLibraryGroup.Vegetation,
                    OmsiAssetLibraryGroup.Transit,
                    OmsiAssetLibraryGroup.StreetFurniture,
                    OmsiAssetLibraryGroup.Utilities,
                    OmsiAssetLibraryGroup.Other
                ],

            OmsiAssetKind.Spline =>
                [
                    OmsiAssetLibraryGroup.All,
                    OmsiAssetLibraryGroup.Roads,
                    OmsiAssetLibraryGroup.Paths,
                    OmsiAssetLibraryGroup.Rail,
                    OmsiAssetLibraryGroup.Bridges,
                    OmsiAssetLibraryGroup.Markings,
                    OmsiAssetLibraryGroup.Other
                ],

            _ =>
                [
                    OmsiAssetLibraryGroup.All
                ]
        };

    public static OmsiAssetLibraryGroup
        Classify(
            OmsiAssetIndexEntry entry)
    {
        ArgumentNullException.ThrowIfNull(
            entry);

        var text =
            Normalize(
                entry.RelativePath);

        var originalRole =
            MapStudioOriginalOmsiAssetCatalog
                .Classify(
                    entry);

        if (
            entry.Kind ==
                OmsiAssetKind.SceneryObject)
        {
            var originalGroup =
                originalRole switch
                {
                    MapStudioOriginalOmsiAssetRole.JunctionObject =>
                        OmsiAssetLibraryGroup.Junctions,
                    MapStudioOriginalOmsiAssetRole.BusStopObject or
                    MapStudioOriginalOmsiAssetRole.BusShelterObject =>
                        OmsiAssetLibraryGroup.Transit,
                    MapStudioOriginalOmsiAssetRole.BuildingObject =>
                        OmsiAssetLibraryGroup.Buildings,
                    MapStudioOriginalOmsiAssetRole.VegetationObject =>
                        OmsiAssetLibraryGroup.Vegetation,
                    MapStudioOriginalOmsiAssetRole.UtilityObject or
                    MapStudioOriginalOmsiAssetRole.UtilityPoleObject =>
                        OmsiAssetLibraryGroup.Utilities,
                    MapStudioOriginalOmsiAssetRole.CrosswalkObject or
                    MapStudioOriginalOmsiAssetRole.TrafficSignalObject or
                    MapStudioOriginalOmsiAssetRole.TrafficSignObject or
                    MapStudioOriginalOmsiAssetRole.StreetLightObject or
                    MapStudioOriginalOmsiAssetRole.BenchObject or
                    MapStudioOriginalOmsiAssetRole.WasteBasketObject or
                    MapStudioOriginalOmsiAssetRole.BollardObject or
                    MapStudioOriginalOmsiAssetRole.StreetFurnitureObject =>
                        OmsiAssetLibraryGroup.StreetFurniture,
                    _ =>
                        OmsiAssetLibraryGroup.All
                };

            if (
                originalGroup !=
                    OmsiAssetLibraryGroup.All)
            {
                return originalGroup;
            }
            if (ContainsAny(
                text,
                "junction",
                "intersection",
                "kreuzung",
                "crossing",
                "cruzamento",
                "rotatoria",
                "roundabout"))
            {
                return OmsiAssetLibraryGroup
                    .Junctions;
            }

            if (ContainsAny(
                text,
                "bridge",
                "bruecke",
                "brucke",
                "ponte",
                "viaduct",
                "viaduto",
                "overpass",
                "elevated"))
            {
                return OmsiAssetLibraryGroup
                    .Bridges;
            }

            if (ContainsAny(
                text,
                "tree",
                "baum",
                "bush",
                "shrub",
                "hedge",
                "grass",
                "vegetation",
                "flora",
                "arvore",
                "arbusto"))
            {
                return OmsiAssetLibraryGroup
                    .Vegetation;
            }

            if (ContainsAny(
                text,
                "busstop",
                "bus stop",
                "haltestelle",
                "terminal",
                "bahnhof",
                "station",
                "shelter",
                "abrigo",
                "depot",
                "garage",
                "garagem",
                "ponto",
                "parada",
                "estacao"))
            {
                return OmsiAssetLibraryGroup
                    .Transit;
            }

            if (ContainsAny(
                text,
                "house",
                "haus",
                "building",
                "gebaeude",
                "wohn",
                "apartment",
                "shop",
                "store",
                "factory",
                "warehouse",
                "school",
                "hospital",
                "igreja",
                "church",
                "predio",
                "casa"))
            {
                return OmsiAssetLibraryGroup
                    .Buildings;
            }

            if (ContainsAny(
                text,
                "lamp",
                "light",
                "bench",
                "bank",
                "bin",
                "trash",
                "sign",
                "schild",
                "traffic",
                "trafficlight",
                "ampel",
                "signalgeber",
                "crosswalk",
                "zebra",
                "zebrastreifen",
                "fence",
                "zaun",
                "bollard",
                "hydrant",
                "hidrante",
                "pole",
                "poste",
                "placa",
                "semaforo"))
            {
                return OmsiAssetLibraryGroup
                    .StreetFurniture;
            }

            if (ContainsAny(
                text,
                "power",
                "utility",
                "utilitybox",
                "cabinet",
                "substation",
                "transformer",
                "water",
                "wasser",
                "sewer",
                "gas",
                "pipeline",
                "tower",
                "antenna",
                "infra",
                "infraestrutura",
                "energia",
                "agua",
                "saneamento",
                "esgoto"))
            {
                return OmsiAssetLibraryGroup
                    .Utilities;
            }

            return OmsiAssetLibraryGroup.Other;
        }

        if (
            entry.Kind ==
                OmsiAssetKind.Spline)
        {
            var originalGroup =
                originalRole switch
                {
                    MapStudioOriginalOmsiAssetRole.RoadSpline or
                    MapStudioOriginalOmsiAssetRole.InvisibleRoadSpline =>
                        OmsiAssetLibraryGroup.Roads,
                    MapStudioOriginalOmsiAssetRole.SidewalkSpline =>
                        OmsiAssetLibraryGroup.Paths,
                    MapStudioOriginalOmsiAssetRole.RailSpline =>
                        OmsiAssetLibraryGroup.Rail,
                    _ =>
                        OmsiAssetLibraryGroup.All
                };

            if (
                originalGroup !=
                    OmsiAssetLibraryGroup.All)
            {
                return originalGroup;
            }

            if (ContainsAny(
                text,
                "bridge",
                "bruecke",
                "brucke",
                "ponte",
                "viaduct",
                "viaduto",
                "tunnel",
                "tunel",
                "elevated"))
            {
                return OmsiAssetLibraryGroup
                    .Bridges;
            }

            if (ContainsAny(
                text,
                "rail",
                "track",
                "gleis",
                "tram",
                "strab",
                "bahn",
                "metro"))
            {
                return OmsiAssetLibraryGroup
                    .Rail;
            }

            if (ContainsAny(
                text,
                "sidewalk",
                "foot",
                "path",
                "walk",
                "cycle",
                "bike",
                "radweg",
                "gehweg",
                "calcada",
                "caminho"))
            {
                return OmsiAssetLibraryGroup
                    .Paths;
            }

            if (ContainsAny(
                text,
                "mark",
                "line",
                "stripe",
                "lane",
                "roadmark",
                "fahrbahnmark",
                "faixa"))
            {
                return OmsiAssetLibraryGroup
                    .Markings;
            }

            if (ContainsAny(
                text,
                "road",
                "street",
                "strasse",
                "strabe",
                "fahrbahn",
                "asphalt",
                "avenue",
                "allee",
                "rua",
                "avenida"))
            {
                return OmsiAssetLibraryGroup
                    .Roads;
            }

            return OmsiAssetLibraryGroup.Other;
        }

        return OmsiAssetLibraryGroup.All;
    }

    public static bool MatchesSmartSearch(
        string text,
        string query)
    {
        if (string.IsNullOrWhiteSpace(
                query))
        {
            return true;
        }

        var normalizedText =
            Normalize(
                text);

        var tokens =
            Normalize(
                query)
                .Split(
                    ' ',
                    StringSplitOptions
                        .RemoveEmptyEntries);

        return tokens.All(
            token =>
            {
                var alternatives =
                    SearchSynonyms
                        .TryGetValue(
                            token,
                            out var values)
                        ? values
                        : [token];

                return alternatives.Any(
                    alternative =>
                        normalizedText
                            .Contains(
                                Normalize(
                                    alternative),
                                StringComparison
                                    .Ordinal));
            });
    }

    public static string GetSubcategory(
        OmsiAssetIndexEntry entry)
    {
        ArgumentNullException.ThrowIfNull(
            entry);

        var text =
            Normalize(
                entry.RelativePath);

        var group =
            Classify(
                entry);

        return group switch
        {
            OmsiAssetLibraryGroup.Junctions =>
                ContainsAny(
                    text,
                    "roundabout",
                    "rotatoria")
                    ? "Rotatórias"
                    : "Interseções",

            OmsiAssetLibraryGroup.Bridges
                when entry.Kind ==
                    OmsiAssetKind.SceneryObject =>
                ContainsAny(
                    text,
                    "viaduct",
                    "viaduto",
                    "overpass",
                    "elevated")
                    ? "Viadutos / elevados"
                    : "Pontes",

            OmsiAssetLibraryGroup.Buildings =>
                ContainsAny(
                    text,
                    "shop",
                    "store",
                    "commercial",
                    "loja")
                    ? "Comercial"
                    : ContainsAny(
                        text,
                        "factory",
                        "warehouse",
                        "industrial",
                        "fabrica")
                        ? "Industrial"
                        : ContainsAny(
                            text,
                            "school",
                            "hospital",
                            "church",
                            "public",
                            "escola",
                            "igreja")
                            ? "Público"
                            : ContainsAny(
                                text,
                                "house",
                                "haus",
                                "wohn",
                                "apartment",
                                "residential",
                                "casa")
                                ? "Residencial"
                                : "Edificações",

            OmsiAssetLibraryGroup.Vegetation =>
                ContainsAny(
                    text,
                    "grass",
                    "grama")
                    ? "Grama"
                    : ContainsAny(
                        text,
                        "bush",
                        "shrub",
                        "hedge",
                        "arbusto")
                        ? "Arbustos"
                        : ContainsAny(
                            text,
                            "tree",
                            "baum",
                            "arvore")
                            ? "Árvores"
                            : "Vegetação",

            OmsiAssetLibraryGroup.Transit =>
                ContainsAny(
                    text,
                    "garage",
                    "depot",
                    "garagem")
                    ? "Garagens / depósitos"
                    : ContainsAny(
                        text,
                        "terminal",
                        "station",
                        "bahnhof",
                        "estacao")
                        ? "Terminais / estações"
                        : ContainsAny(
                            text,
                            "busstop",
                            "bus stop",
                            "haltestelle",
                            "shelter",
                            "wartehaus",
                            "hst",
                            "abrigo",
                            "ponto",
                            "parada")
                            ? "Pontos / abrigos"
                            : "Transporte",

            OmsiAssetLibraryGroup.StreetFurniture =>
                ContainsAny(
                    text,
                    "crosswalk",
                    "zebra",
                    "zebrastreifen",
                    "travessia",
                    "faixa pedestre")
                    ? "Faixas de pedestres"
                    : ContainsAny(
                        text,
                        "sign",
                        "schild",
                        "verkehrszeichen",
                        "zeichen",
                        "traffic",
                        "trafficlight",
                        "traffic light",
                        "ampel",
                        "signalgeber",
                        "semaforo",
                        "placa")
                        ? "Sinalização"
                    : ContainsAny(
                        text,
                        "lamp",
                        "light",
                        "strlt",
                        "peitschenleuchte",
                        "gaslight",
                        "neonlight",
                        "sodiumlight",
                        "pole",
                        "poste")
                        ? "Iluminação"
                        : ContainsAny(
                            text,
                            "fence",
                            "zaun",
                            "barrier",
                            "bollard",
                            "cerca")
                            ? "Cercas / barreiras"
                            : "Mobiliário urbano",

            OmsiAssetLibraryGroup.Utilities =>
                ContainsAny(
                    text,
                    "power",
                    "substation",
                    "transformer",
                    "energia")
                    ? "Energia"
                    : ContainsAny(
                        text,
                        "water",
                        "wasser",
                        "sewer",
                        "agua",
                        "saneamento",
                        "esgoto")
                        ? "Água / saneamento"
                        : "Infraestrutura",

            OmsiAssetLibraryGroup.Roads =>
                ContainsAny(
                    text,
                    "oneway",
                    "one-way",
                    "einbahn")
                    ? "Mão única"
                    : ContainsAny(
                        text,
                        "avenue",
                        "boulevard",
                        "allee",
                        "avenida")
                        ? "Avenidas"
                        : ContainsAny(
                            text,
                            "highway",
                            "country",
                            "landstrasse",
                            "estrada")
                            ? "Estradas"
                            : "Ruas urbanas",

            OmsiAssetLibraryGroup.Paths =>
                ContainsAny(
                    text,
                    "cycle",
                    "bike",
                    "radweg",
                    "ciclovia")
                    ? "Ciclovias"
                    : "Calçadas / caminhos",

            OmsiAssetLibraryGroup.Rail =>
                ContainsAny(
                    text,
                    "tram",
                    "strab",
                    "streetcar",
                    "bonde")
                    ? "Bonde / tram"
                    : "Ferrovia",

            OmsiAssetLibraryGroup.Bridges =>
                ContainsAny(
                    text,
                    "tunnel",
                    "tunel")
                    ? "Túneis"
                    : "Pontes / elevados",

            OmsiAssetLibraryGroup.Markings =>
                "Marcação viária",

            _ =>
                "Outros"
        };
    }

    public static string GetDisplayName(
        OmsiAssetLibraryGroup group) =>
        group switch
        {
            OmsiAssetLibraryGroup.Junctions =>
                "Cruzamentos",
            OmsiAssetLibraryGroup.Bridges =>
                "Pontes / túneis",
            OmsiAssetLibraryGroup.Buildings =>
                "Casas / prédios",
            OmsiAssetLibraryGroup.Vegetation =>
                "Árvores / verde",
            OmsiAssetLibraryGroup.Transit =>
                "Transporte",
            OmsiAssetLibraryGroup.StreetFurniture =>
                "Mobiliário / sinalização",
            OmsiAssetLibraryGroup.Utilities =>
                "Infraestrutura",
            OmsiAssetLibraryGroup.Roads =>
                "Ruas",
            OmsiAssetLibraryGroup.Paths =>
                "Calçadas / caminhos",
            OmsiAssetLibraryGroup.Rail =>
                "Trilhos",
            OmsiAssetLibraryGroup.Markings =>
                "Faixas / marcas",
            OmsiAssetLibraryGroup.Other =>
                "Outros",
            _ =>
                "Todos"
        };

    private static bool ContainsAny(
        string text,
        params string[] terms)
    {
        var normalizedText =
            Normalize(
                text);

        var tokens =
            normalizedText
                .Split(
                    [
                        '/',
                        '\\',
                        '_',
                        '-',
                        '.',
                        ' ',
                        '(',
                        ')',
                        '[',
                        ']'
                    ],
                    StringSplitOptions
                        .RemoveEmptyEntries);

        return terms.Any(
            term =>
            {
                var normalizedTerm =
                    Normalize(
                        term);

                if (
                    normalizedTerm.IndexOfAny(
                        [
                            ' ',
                            '/',
                            '\\',
                            '_',
                            '-'
                        ]) >=
                    0)
                {
                    return normalizedText
                        .Contains(
                            normalizedTerm,
                            StringComparison.Ordinal);
                }

                return tokens.Any(
                    token =>
                        string.Equals(
                            token,
                            normalizedTerm,
                            StringComparison.Ordinal));
            });
    }

    private static string Normalize(
        string value)
    {
        var decomposed =
            value
                .Replace(
                    '\\',
                    '/')
                .Normalize(
                    NormalizationForm
                        .FormD);

        var builder =
            new StringBuilder(
                decomposed.Length);

        foreach (
            var character in
                decomposed)
        {
            if (
                CharUnicodeInfo
                    .GetUnicodeCategory(
                        character) ==
                UnicodeCategory
                    .NonSpacingMark)
            {
                continue;
            }

            builder.Append(
                char.ToLowerInvariant(
                    character));
        }

        return builder
            .ToString()
            .Normalize(
                NormalizationForm
                    .FormC);
    }
}
