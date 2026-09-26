using System.Globalization;
using System.Text;
using MapStudio.Core.Omsi.Indexing;

namespace MapStudio.Core.Generation.Vegetation;

public sealed class MapStudioVegetationAssetSuggester
{
    public OmsiAssetIndexEntry? Suggest(
        IReadOnlyList<OmsiAssetIndexEntry> assets,
        IReadOnlyList<MapStudioProjectedVegetationPoint> points,
        MapStudioOsmVegetationKind kind)
    {
        ArgumentNullException.ThrowIfNull(
            assets);

        ArgumentNullException.ThrowIfNull(
            points);

        var candidates =
            assets
                .Where(
                    asset =>
                        asset.Kind ==
                            OmsiAssetKind
                                .SceneryObject &&
                        OmsiAssetLibraryClassifier
                            .Classify(
                                asset) ==
                            OmsiAssetLibraryGroup
                                .Vegetation)
                .ToArray();

        if (
            candidates.Length ==
                0)
        {
            return null;
        }

        var metadataTokens =
            BuildMetadataTokens(
                points
                    .Where(
                        point =>
                            point.Kind ==
                            kind));

        return candidates
            .Select(
                asset =>
                    (
                        Asset:
                            asset,
                        Score:
                            Score(
                                asset,
                                metadataTokens,
                                kind)
                    ))
            .OrderByDescending(
                item =>
                    item.Score)
            .ThenBy(
                item =>
                    item.Asset
                        .RelativePath,
                StringComparer
                    .CurrentCultureIgnoreCase)
            .First()
            .Asset;
    }

    private static IReadOnlyDictionary<string, int>
        BuildMetadataTokens(
            IEnumerable<MapStudioProjectedVegetationPoint>
                points)
    {
        var weights =
            new Dictionary<string, int>(
                StringComparer.Ordinal);

        foreach (
            var point in points)
        {
            AddTokens(
                weights,
                point.Species,
                8);

            AddTokens(
                weights,
                point.Genus,
                6);

            AddTokens(
                weights,
                point.LeafType,
                3);
        }

        return weights;
    }

    private static int Score(
        OmsiAssetIndexEntry asset,
        IReadOnlyDictionary<string, int>
            metadataTokens,
        MapStudioOsmVegetationKind kind)
    {
        var normalizedPath =
            Normalize(
                asset.RelativePath);

        var score =
            0;

        foreach (
            var pair in
                metadataTokens)
        {
            if (
                ContainsToken(
                    normalizedPath,
                    pair.Key))
            {
                score +=
                    pair.Value;
            }
        }

        var genericTokens =
            kind ==
                MapStudioOsmVegetationKind
                    .Tree
                ? new[]
                {
                    "tree",
                    "baum",
                    "arvore",
                    "arbol",
                    "arbore",
                    "oak",
                    "pine",
                    "fir",
                    "birch"
                }
                : new[]
                {
                    "shrub",
                    "bush",
                    "hedge",
                    "arbusto",
                    "busch",
                    "scrub"
                };

        foreach (
            var token in
                genericTokens)
        {
            if (
                ContainsToken(
                    normalizedPath,
                    token))
            {
                score +=
                    2;
            }
        }

        return score;
    }

    private static void AddTokens(
        IDictionary<string, int> weights,
        string? value,
        int weight)
    {
        if (
            string.IsNullOrWhiteSpace(
                value))
        {
            return;
        }

        foreach (
            var token in
                Tokenize(
                    value))
        {
            if (
                token.Length <
                    3)
            {
                continue;
            }

            if (
                weights.TryGetValue(
                    token,
                    out var existing))
            {
                weights[token] =
                    existing +
                    weight;
            }
            else
            {
                weights[token] =
                    weight;
            }
        }
    }

    private static IEnumerable<string> Tokenize(
        string value) =>
        Normalize(
                value)
            .Split(
                ' ',
                StringSplitOptions
                    .RemoveEmptyEntries |
                StringSplitOptions
                    .TrimEntries)
            .Distinct(
                StringComparer.Ordinal);

    private static bool ContainsToken(
        string normalizedText,
        string token)
    {
        var index =
            0;

        while (
            (
                index =
                    normalizedText.IndexOf(
                        token,
                        index,
                        StringComparison.Ordinal)
            ) >=
            0)
        {
            var leftOk =
                index ==
                    0 ||
                normalizedText[
                    index -
                    1] ==
                    ' ';

            var end =
                index +
                token.Length;

            var rightOk =
                end ==
                    normalizedText.Length ||
                normalizedText[end] ==
                    ' ';

            if (
                leftOk &&
                rightOk)
            {
                return true;
            }

            index =
                end;
        }

        return false;
    }

    private static string Normalize(
        string value)
    {
        var formD =
            value
                .Normalize(
                    NormalizationForm
                        .FormD);

        var builder =
            new StringBuilder(
                formD.Length);

        var previousSpace =
            true;

        foreach (
            var character in
                formD)
        {
            var category =
                CharUnicodeInfo
                    .GetUnicodeCategory(
                        character);

            if (
                category ==
                    UnicodeCategory
                        .NonSpacingMark)
            {
                continue;
            }

            if (
                char.IsLetterOrDigit(
                    character))
            {
                builder.Append(
                    char.ToLowerInvariant(
                        character));

                previousSpace =
                    false;

                continue;
            }

            if (!previousSpace)
            {
                builder.Append(
                    ' ');

                previousSpace =
                    true;
            }
        }

        return builder
            .ToString()
            .Trim();
    }
}
