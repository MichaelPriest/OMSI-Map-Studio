using MapStudio.Core.Omsi.Maps;
using MapStudio.Renderer.Picking;

namespace MapStudio.Renderer.Scene;

public sealed class NativeSceneBuilder
{
    public NativeSceneSnapshot Build(
        IReadOnlyList<NativeSceneTile> tiles,
        PickingRegistry<object> picking)
    {
        ArgumentNullException.ThrowIfNull(
            tiles);

        ArgumentNullException.ThrowIfNull(
            picking);

        picking.Clear();

        var objects =
            new List<NativeObjectEntity>();

        var splines =
            new List<NativeSplineEntity>();

        var terrain =
            new List<NativeTerrainEntity>();

        foreach (var tile in tiles)
        {
            var tileX =
                OmsiTileGrid.GetOriginX(
                    tile.Reference.X);

            var tileY =
                OmsiTileGrid.GetOriginZ(
                    tile.Reference.Y);

            foreach (
                var placedObject in
                    tile.Content.Objects)
            {
                var pickingId =
                    picking.Register(
                        PickingKind.Object,
                        placedObject);

                objects.Add(
                    new NativeObjectEntity(
                        pickingId,
                        tile.Reference,
                        placedObject,
                        WorldX:
                            (float)(
                                tileX +
                                placedObject.X),
                        WorldY:
                            (float)
                                placedObject.Z,
                        WorldZ:
                            (float)(
                                tileY +
                                placedObject.Y)));
            }

            foreach (
                var placedSpline in
                    tile.Content.Splines)
            {
                var pickingId =
                    picking.Register(
                        PickingKind.Spline,
                        placedSpline);

                splines.Add(
                    new NativeSplineEntity(
                        pickingId,
                        tile.Reference,
                        placedSpline,
                        WorldX:
                            (float)(
                                tileX +
                                placedSpline.X),
                        WorldY:
                            (float)
                                placedSpline.Z,
                        WorldZ:
                            (float)(
                                tileY +
                                placedSpline.Y)));
            }

            if (
                tile.Content.Terrain is
                    { } terrainGrid)
            {
                terrain.Add(
                    new NativeTerrainEntity(
                        tile.Reference,
                        terrainGrid));
            }
        }

        return new NativeSceneSnapshot(
            tiles.ToArray(),
            objects,
            splines,
            terrain);
    }
}
