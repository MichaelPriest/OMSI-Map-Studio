namespace MapStudio.Core.Omsi.Maps;

public readonly record struct OmsiTileWorldBounds(
    double MinX,
    double MinZ,
    double MaxX,
    double MaxZ);

public static class OmsiTileGrid
{
    public const double TileSize = 300.0;

    public const double HalfTileSize =
        TileSize / 2.0;

    public static double GetOriginX(
        int tileX) =>
        tileX *
        TileSize;

    public static double GetOriginZ(
        int tileY) =>
        tileY *
        TileSize;

    public static OmsiTileWorldBounds GetBounds(
        int tileX,
        int tileY)
    {
        var minX =
            GetOriginX(
                tileX);

        var minZ =
            GetOriginZ(
                tileY);

        return new OmsiTileWorldBounds(
            minX,
            minZ,
            minX +
                TileSize,
            minZ +
                TileSize);
    }

    public static int WorldToTileX(
        double worldX) =>
        WorldToTileCoordinate(
            worldX);

    public static int WorldToTileY(
        double worldZ) =>
        WorldToTileCoordinate(
            worldZ);

    public static double WorldToLocalX(
        double worldX,
        int tileX) =>
        worldX -
        GetOriginX(
            tileX);

    public static double WorldToLocalZ(
        double worldZ,
        int tileY) =>
        worldZ -
        GetOriginZ(
            tileY);

    private static int WorldToTileCoordinate(
        double worldCoordinate)
    {
        if (!double.IsFinite(
                worldCoordinate))
        {
            throw new ArgumentOutOfRangeException(
                nameof(worldCoordinate));
        }

        return checked(
            (int)Math.Floor(
                worldCoordinate /
                TileSize));
    }
}
