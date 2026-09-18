namespace MapStudio.Core.Omsi.Maps;

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
