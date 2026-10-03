using MapStudio.Core.Omsi.Config;

namespace MapStudio.Core.Omsi.Maps;

public static class OmsiGlobalTileCatalogEditor
{
    public static byte[] AppendTile(
        OmsiConfigDocument document,
        OmsiTileReference tile)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(tile);

        ValidateTilePath(tile.RelativeMapPath);

        var existing =
            OmsiMapCatalog.ReadTiles(document);

        if (existing.Any(item =>
                item.X == tile.X &&
                item.Y == tile.Y))
        {
            throw new InvalidDataException(
                "mapTileCoordinateAlreadyExists");
        }

        if (existing.Any(item =>
                string.Equals(
                    item.RelativeMapPath,
                    tile.RelativeMapPath,
                    StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException(
                "mapTilePathAlreadyExists");
        }

        var lines =
            document.Lines.ToList();

        if (
            lines.Count > 0 &&
            lines[^1].Length != 0)
        {
            lines.Add(string.Empty);
        }

        lines.Add("[map]");
        lines.Add(
            tile.X.ToString(
                System.Globalization
                    .CultureInfo.InvariantCulture));
        lines.Add(
            tile.Y.ToString(
                System.Globalization
                    .CultureInfo.InvariantCulture));
        lines.Add(tile.RelativeMapPath);

        return new OmsiConfigDocument(
                lines,
                Array.Empty<OmsiConfigSection>(),
                document.NewLine,
                document.HasTrailingNewLine,
                document.TextEncoding,
                document.HasByteOrderMark)
            .ToBytes();
    }

    private static void ValidateTilePath(
        string relativeMapPath)
    {
        if (
            string.IsNullOrWhiteSpace(
                relativeMapPath) ||
            Path.IsPathRooted(
                relativeMapPath))
        {
            throw new InvalidDataException(
                "invalidMapTilePath");
        }

        var normalized =
            relativeMapPath.Replace('\\', '/');

        if (
            normalized.Split(
                    '/',
                    StringSplitOptions.RemoveEmptyEntries)
                .Any(part =>
                    part is "." or "..") ||
            !normalized.EndsWith(
                ".map",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "invalidMapTilePath");
        }
    }
}
