using MapStudio.Core.Omsi.Maps;
using System.Numerics;

namespace MapStudio.Renderer.Scene;

public sealed record NativeMapGeometry(
    NativeMapVertex[] GridVertices,
    NativeMapVertex[] ObjectGuideVertices,
    NativeMapVertex[] SplineGuideVertices)
{
    public NativeMapVertex[] Vertices =>
        GridVertices
            .Concat(ObjectGuideVertices)
            .Concat(SplineGuideVertices)
            .ToArray();

    public int LineCount =>
        (
            GridVertices.Length +
            ObjectGuideVertices.Length +
            SplineGuideVertices.Length
        ) / 2;
}

public sealed class NativeMapGeometryBuilder
{
    private static readonly Vector4 TerrainColor =
        new(
            0.16f,
            0.32f,
            0.38f,
            0.85f);

    private static readonly Vector4 ObjectColor =
        new(
            0.95f,
            0.62f,
            0.18f,
            1.0f);

    private static readonly Vector4 SplineColor =
        new(
            0.16f,
            0.80f,
            0.98f,
            1.0f);

    public NativeMapGeometry Build(
        NativeSceneSnapshot scene)
    {
        ArgumentNullException.ThrowIfNull(
            scene);

        if (scene.Tiles.Count == 0)
        {
            return new NativeMapGeometry(
                [],
                [],
                []);
        }

        var gridVertices =
            new List<NativeMapVertex>(
                4096);

        var objectGuideVertices =
            new List<NativeMapVertex>(
                Math.Max(
                    64,
                    scene.Objects.Count * 4));

        var splineGuideVertices =
            new List<NativeMapVertex>(
                Math.Max(
                    128,
                    scene.Splines.Count * 16));

        static void AddLine(
            List<NativeMapVertex> output,
            double x1,
            double y1,
            double z1,
            double x2,
            double y2,
            double z2,
            Vector4 color)
        {
            output.Add(
                new NativeMapVertex(
                    new Vector3(
                        (float)x1,
                        (float)y1,
                        (float)z1),
                    color));

            output.Add(
                new NativeMapVertex(
                    new Vector3(
                        (float)x2,
                        (float)y2,
                        (float)z2),
                    color));
        }

        void AddGridLine(
            double x1,
            double y1,
            double z1,
            double x2,
            double y2,
            double z2,
            Vector4 color) =>
            AddLine(
                gridVertices,
                x1,
                y1,
                z1,
                x2,
                y2,
                z2,
                color);

        void AddObjectLine(
            double x1,
            double y1,
            double z1,
            double x2,
            double y2,
            double z2,
            Vector4 color) =>
            AddLine(
                objectGuideVertices,
                x1,
                y1,
                z1,
                x2,
                y2,
                z2,
                color);

        void AddSplineLine(
            double x1,
            double y1,
            double z1,
            double x2,
            double y2,
            double z2,
            Vector4 color) =>
            AddLine(
                splineGuideVertices,
                x1,
                y1,
                z1,
                x2,
                y2,
                z2,
                color);

        foreach (var tile in scene.Tiles)
        {
            var originX =
                tile.Reference.X *
                OmsiTileGrid.TileSize;

            var originZ =
                tile.Reference.Y *
                OmsiTileGrid.TileSize;

            AddTerrainLine(tile, 0, 0, OmsiTileGrid.TileSize, 0, originX, originZ, TerrainColor, AddGridLine);
            AddTerrainLine(tile, OmsiTileGrid.TileSize, 0, OmsiTileGrid.TileSize, OmsiTileGrid.TileSize, originX, originZ, TerrainColor, AddGridLine);
            AddTerrainLine(tile, OmsiTileGrid.TileSize, OmsiTileGrid.TileSize, 0, OmsiTileGrid.TileSize, originX, originZ, TerrainColor, AddGridLine);
            AddTerrainLine(tile, 0, OmsiTileGrid.TileSize, 0, 0, originX, originZ, TerrainColor, AddGridLine);

            var terrain =
                tile.Content.Terrain;

            if (
                terrain is null ||
                terrain.CellCount <= 0)
            {
                continue;
            }

            var step =
                Math.Max(
                    1,
                    terrain.CellCount /
                    16);

            for (
                var cell = step;
                cell < terrain.CellCount;
                cell += step)
            {
                var offset =
                    OmsiTileGrid.TileSize *
                    cell /
                    terrain.CellCount;

                AddTerrainLine(tile, offset, 0, offset, OmsiTileGrid.TileSize, originX, originZ, TerrainColor, AddGridLine);
                AddTerrainLine(tile, 0, offset, OmsiTileGrid.TileSize, offset, originX, originZ, TerrainColor, AddGridLine);
            }
        }

        foreach (
            var item in scene.Objects)
        {
            const double marker = 3.5;

            var baseHeight =
                item.WorldY +
                NativeTerrainSampler
                    .GetHeightAtObject(
                        scene,
                        item) +
                0.35;

            AddObjectLine(
                item.WorldX - marker,
                baseHeight,
                item.WorldZ,
                item.WorldX + marker,
                baseHeight,
                item.WorldZ,
                ObjectColor);

            AddObjectLine(
                item.WorldX,
                baseHeight,
                item.WorldZ - marker,
                item.WorldX,
                baseHeight,
                item.WorldZ + marker,
                ObjectColor);
        }

        foreach (
            var item in scene.Splines)
        {
            AddSpline(
                item,
                AddSplineLine);
        }

        return new NativeMapGeometry(
            gridVertices.ToArray(),
            objectGuideVertices.ToArray(),
            splineGuideVertices.ToArray());
    }

    private static void AddTerrainLine(
        NativeSceneTile tile,
        double localX1,
        double localZ1,
        double localX2,
        double localZ2,
        double originX,
        double originZ,
        Vector4 color,
        Action<
            double,
            double,
            double,
            double,
            double,
            double,
            Vector4> addLine)
    {
        var height1 =
            NativeTerrainSampler
                .GetHeightAtLocalPoint(
                    tile,
                    localX1,
                    localZ1) +
            0.20;

        var height2 =
            NativeTerrainSampler
                .GetHeightAtLocalPoint(
                    tile,
                    localX2,
                    localZ2) +
            0.20;

        addLine(
            originX + localX1,
            height1,
            originZ + localZ1,
            originX + localX2,
            height2,
            originZ + localZ2,
            color);
    }

    private static void AddSpline(
        NativeSplineEntity entity,
        Action<
            double,
            double,
            double,
            double,
            double,
            double,
            Vector4> addLine)
    {
        var length =
            Math.Max(
                0.0,
                entity.Spline.Length);

        if (length < 0.01)
        {
            return;
        }

        var segmentCount =
            Math.Clamp(
                (int)Math.Ceiling(
                    length / 8.0),
                2,
                64);

        var previous =
            NativeSplinePathMath
                .GetFrame(
                    entity,
                    0)
                .Center +
            Vector3.UnitY *
            0.30f;

        for (
            var index = 1;
            index <= segmentCount;
            index++)
        {
            var current =
                NativeSplinePathMath
                    .GetFrame(
                        entity,
                        length *
                        index /
                        segmentCount)
                    .Center +
                Vector3.UnitY *
                0.30f;

            addLine(
                previous.X,
                previous.Y,
                previous.Z,
                current.X,
                current.Y,
                current.Z,
                SplineColor);

            previous = current;
        }
    }
}
