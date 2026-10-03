using MapStudio.Core.Omsi.Maps;
namespace MapStudio.Renderer.Scene;

public static class NativeTerrainSampler
{
    public static double GetHeightAtObject(
        NativeSceneSnapshot scene,
        NativeObjectEntity entity)
    {
        ArgumentNullException.ThrowIfNull(scene);

        foreach (var tile in scene.Tiles)
        {
            if (
                tile.Reference.X == entity.Tile.X &&
                tile.Reference.Y == entity.Tile.Y)
            {
                return GetHeightAtLocalPoint(
                    tile,
                    entity.Object.X,
                    entity.Object.Y);
            }
        }

        return 0;
    }

    public static double GetHeightAtWorldPoint(
        NativeSceneSnapshot scene,
        double worldX,
        double worldZ) =>
        TryGetHeightAtWorldPoint(
            scene,
            worldX,
            worldZ,
            out var height)
            ? height
            : 0.0;

    public static bool TryGetHeightAtWorldPoint(
        NativeSceneSnapshot scene,
        double worldX,
        double worldZ,
        out double height)
    {
        ArgumentNullException.ThrowIfNull(scene);

        height = 0.0;

        var tileX =
            (int)Math.Floor(
                worldX /
                OmsiTileGrid.TileSize);

        var tileY =
            (int)Math.Floor(
                worldZ /
                OmsiTileGrid.TileSize);

        foreach (var tile in scene.Tiles)
        {
            if (
                tile.Reference.X != tileX ||
                tile.Reference.Y != tileY)
            {
                continue;
            }

            var terrain =
                tile.Content.Terrain;

            if (
                terrain is null ||
                terrain.CellCount <= 0)
            {
                return false;
            }

            var sampleCount =
                terrain.CellCount + 1;

            if (
                terrain.Heights.Count !=
                sampleCount *
                sampleCount)
            {
                return false;
            }

            height =
                GetHeightAtLocalPoint(
                    tile,
                    worldX -
                    tileX *
                    OmsiTileGrid.TileSize,
                    worldZ -
                    tileY *
                    OmsiTileGrid.TileSize);

            return
                double.IsFinite(
                    height);
        }

        return false;
    }

    public static double GetHeightAtLocalPoint(
        NativeSceneTile tile,
        double localX,
        double localY)
    {
        var terrain = tile.Content.Terrain;

        if (terrain is null || terrain.CellCount <= 0)
        {
            return 0;
        }

        var cellCount = terrain.CellCount;
        var sampleCount = cellCount + 1;

        if (terrain.Heights.Count != sampleCount * sampleCount)
        {
            return 0;
        }

        var gridX =
            Math.Clamp(
                localX /
                OmsiTileGrid.TileSize *
                cellCount,
                0,
                cellCount);

        var gridY =
            Math.Clamp(
                localY /
                OmsiTileGrid.TileSize *
                cellCount,
                0,
                cellCount);

        var column0 =
            (int)Math.Floor(
                gridX);

        var row0 =
            (int)Math.Floor(
                gridY);

        var column1 =
            Math.Min(
                cellCount,
                column0 + 1);

        var row1 =
            Math.Min(
                cellCount,
                row0 + 1);

        var fractionX =
            gridX -
            column0;

        var fractionY =
            gridY -
            row0;

        var height00 =
            terrain.Heights[
                row0 *
                sampleCount +
                column0];

        var height10 =
            terrain.Heights[
                row0 *
                sampleCount +
                column1];

        var height01 =
            terrain.Heights[
                row1 *
                sampleCount +
                column0];

        var height11 =
            terrain.Heights[
                row1 *
                sampleCount +
                column1];

        if (
            !float.IsFinite(height00) ||
            !float.IsFinite(height10) ||
            !float.IsFinite(height01) ||
            !float.IsFinite(height11))
        {
            return 0;
        }

        var top =
            height00 +
            (height10 - height00) *
            fractionX;

        var bottom =
            height01 +
            (height11 - height01) *
            fractionX;

        return
            top +
            (bottom - top) *
            fractionY;
    }
}
