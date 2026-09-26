using System.Globalization;
using MapStudio.Core.Omsi.Config;

namespace MapStudio.Core.Omsi.Maps;

public sealed record OmsiTileDeletionSafety(
    bool CanDelete,
    int MapIndex,
    IReadOnlyList<string> Reasons);

public static class OmsiTileDeletionSafetyAnalyzer
{
    public static OmsiTileDeletionSafety Analyze(
        OmsiConfigDocument globalDocument,
        OmsiTileReference tile,
        OmsiTileContent tileContent)
    {
        ArgumentNullException.ThrowIfNull(
            globalDocument);

        ArgumentNullException.ThrowIfNull(
            tile);

        ArgumentNullException.ThrowIfNull(
            tileContent);

        var reasons =
            new List<string>();

        var mapSections =
            globalDocument
                .FindSections("map")
                .ToArray();

        var parsedTiles =
            OmsiMapCatalog
                .ReadTiles(
                    globalDocument);

        var mapIndex =
            parsedTiles
                .Select(
                    (item, index) =>
                        (
                            item,
                            index
                        ))
                .Where(
                    pair =>
                        pair.item.X ==
                            tile.X &&
                        pair.item.Y ==
                            tile.Y &&
                        string.Equals(
                            pair.item
                                .RelativeMapPath,
                            tile.RelativeMapPath,
                            StringComparison
                                .OrdinalIgnoreCase))
                .Select(
                    pair =>
                        pair.index)
                .DefaultIfEmpty(-1)
                .SingleOrDefault();

        if (mapIndex < 0)
        {
            reasons.Add(
                "mapTileNotFound");
        }

        if (parsedTiles.Count <= 1)
        {
            reasons.Add(
                "cannotDeleteOnlyMapTile");
        }

        if (
            mapIndex >= 0 &&
            mapIndex !=
                parsedTiles.Count - 1)
        {
            reasons.Add(
                "tileDeletionWouldShiftMapIndices");
        }

        if (
            tileContent.Objects.Count >
                0 ||
            tileContent.Splines.Count >
                0)
        {
            reasons.Add(
                "tileNotEmpty");
        }

        if (
            mapIndex >= 0 &&
            ReferencesTileFromEntrypoints(
                globalDocument,
                mapIndex))
        {
            reasons.Add(
                "tileReferencedByEntrypoint");
        }

        if (
            mapSections.Length !=
                parsedTiles.Count)
        {
            reasons.Add(
                "globalMapSectionsNotCanonical");
        }

        return new OmsiTileDeletionSafety(
            reasons.Count ==
                0,
            mapIndex,
            reasons);
    }

    private static bool
        ReferencesTileFromEntrypoints(
            OmsiConfigDocument document,
            int mapIndex)
    {
        foreach (
            var section in
                document.FindSections(
                    "entrypoints"))
        {
            var values =
                section.DataLines
                    .ToArray();

            if (
                values.Length >=
                    11 &&
                int.TryParse(
                    values[10],
                    NumberStyles.Integer,
                    CultureInfo
                        .InvariantCulture,
                    out var referencedIndex) &&
                referencedIndex ==
                    mapIndex)
            {
                return true;
            }
        }

        return false;
    }
}

public static class OmsiGlobalTileCatalogRemover
{
    public static byte[] RemoveLastTile(
        OmsiConfigDocument document,
        OmsiTileReference tile)
    {
        ArgumentNullException.ThrowIfNull(
            document);

        ArgumentNullException.ThrowIfNull(
            tile);

        var sections =
            document
                .FindSections(
                    "map")
                .ToArray();

        if (sections.Length == 0)
        {
            throw new InvalidDataException(
                "mapTileNotFound");
        }

        var last =
            sections[^1];

        var values =
            last.DataLines
                .Take(3)
                .ToArray();

        if (
            values.Length <
                3 ||
            !int.TryParse(
                values[0],
                NumberStyles.Integer,
                CultureInfo
                    .InvariantCulture,
                out var x) ||
            !int.TryParse(
                values[1],
                NumberStyles.Integer,
                CultureInfo
                    .InvariantCulture,
                out var y) ||
            x !=
                tile.X ||
            y !=
                tile.Y ||
            !string.Equals(
                values[2],
                tile.RelativeMapPath,
                StringComparison
                    .OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "tileDeletionRequiresLastMapSection");
        }

        var lines =
            document.Lines
                .ToList();

        var start =
            last.KeywordLineIndex;

        var count =
            1 +
            last.RawBodyLines.Count;

        if (
            start <
                0 ||
            count <=
                0 ||
            start +
                count >
                lines.Count)
        {
            throw new InvalidDataException(
                "invalidMapSectionRange");
        }

        lines.RemoveRange(
            start,
            count);

        while (
            lines.Count >
                0 &&
            string.IsNullOrEmpty(
                lines[^1]))
        {
            lines.RemoveAt(
                lines.Count - 1);
        }

        return new OmsiConfigDocument(
                lines,
                Array.Empty<
                    OmsiConfigSection>(),
                document.NewLine,
                document
                    .HasTrailingNewLine,
                document
                    .TextEncoding,
                document
                    .HasByteOrderMark)
            .ToBytes();
    }
}
