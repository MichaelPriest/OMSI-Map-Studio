namespace MapStudio.Core.Omsi.Maps;

public enum OmsiTileStreamDetail
{
    Full = 0,
    Summary = 1
    public static IReadOnlyList<
        OmsiTileStreamSelection>
        SelectForStreaming(
            IReadOnlyList<OmsiTileReference> tiles,
            int centerX,
            int centerY,
            int fullRadius,
            int metadataRadius)
    {
        ArgumentNullException.ThrowIfNull(
            tiles);

        if (fullRadius < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fullRadius));
        }

        if (metadataRadius < fullRadius)
        {
            throw new ArgumentOutOfRangeException(
                nameof(metadataRadius));
        }

        return tiles
            .Select(tile =>
            {
                var ring =
                    Math.Max(
                        Math.Abs(
                            tile.X - centerX),
                        Math.Abs(
                            tile.Y - centerY));

                return new
                    OmsiTileStreamSelection(
                        tile,
                        ring,
                        ring <= fullRadius
                            ? OmsiTileStreamDetail
                                .Full
                            : OmsiTileStreamDetail
                                .Summary);
            })
            .Where(selection =>
                selection.Ring <=
                    metadataRadius)
            .OrderBy(selection =>
                selection.Ring)
            .ThenBy(selection =>
                selection.Detail)
            .ThenBy(selection =>
                selection.Tile.Y)
            .ThenBy(selection =>
                selection.Tile.X)
            .ToArray();
    }
}

public sealed record OmsiTileStreamSelection(
    OmsiTileReference Tile,
    int Ring,
    OmsiTileStreamDetail Detail);

public static class OmsiTileRegionSelector
{
    public static OmsiTileReference? FindInitialTile(
        IReadOnlyList<OmsiTileReference> tiles)
    {
        ArgumentNullException.ThrowIfNull(tiles);

        if (tiles.Count == 0)
        {
            return null;
        }

        var minX = tiles.Min(static tile => tile.X);
        var maxX = tiles.Max(static tile => tile.X);
        var minY = tiles.Min(static tile => tile.Y);
        var maxY = tiles.Max(static tile => tile.Y);

        var centerX = (minX + maxX) / 2.0;
        var centerY = (minY + maxY) / 2.0;

        return tiles
            .OrderBy(tile =>
            {
                var dx = tile.X - centerX;
                var dy = tile.Y - centerY;
                return (dx * dx) + (dy * dy);
            })
            .ThenBy(static tile => tile.Y)
            .ThenBy(static tile => tile.X)
            .First();
    }

    public static IReadOnlyList<OmsiTileReference> Select(
        IReadOnlyList<OmsiTileReference> tiles,
        int centerX,
        int centerY,
        int radius)
    {
        ArgumentNullException.ThrowIfNull(tiles);

        if (radius < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(radius));
        }

        return tiles
            .Where(tile =>
                Math.Abs(tile.X - centerX) <= radius &&
                Math.Abs(tile.Y - centerY) <= radius)
            .OrderBy(tile =>
                Math.Max(
                    Math.Abs(tile.X - centerX),
                    Math.Abs(tile.Y - centerY)))
            .ThenBy(static tile => tile.Y)
            .ThenBy(static tile => tile.X)
            .ToArray();
    }
}
