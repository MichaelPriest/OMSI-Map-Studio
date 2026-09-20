using System.Numerics;

namespace MapStudio.Renderer.Scene;

public sealed record NativeMapGeometry(
    NativeMapVertex[] Vertices)
{
    public int LineCount =>
        Vertices.Length / 2;
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
                Array.Empty<
                    NativeMapVertex>());
        }

        var vertices =
            new List<NativeMapVertex>(
                8192);

        void AddLine(
            double x1,
            double y1,
            double z1,
            double x2,
            double y2,
            double z2,
            Vector4 color)
        {
            vertices.Add(
                new NativeMapVertex(
                    new Vector3(
                        (float)x1,
                        (float)y1,
                        (float)z1),
                    color));

            vertices.Add(
                new NativeMapVertex(
                    new Vector3(
                        (float)x2,
                        (float)y2,
                        (float)z2),
                    color));
        }

        foreach (var tile in scene.Tiles)
        {
            var originX =
                tile.Reference.X *
                300.0;

            var originZ =
                tile.Reference.Y *
                300.0;

            AddTerrainLine(tile, 0, 0, 300, 0, originX, originZ, TerrainColor, AddLine);
            AddTerrainLine(tile, 300, 0, 300, 300, originX, originZ, TerrainColor, AddLine);
            AddTerrainLine(tile, 300, 300, 0, 300, originX, originZ, TerrainColor, AddLine);
            AddTerrainLine(tile, 0, 300, 0, 0, originX, originZ, TerrainColor, AddLine);

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
                    300.0 *
                    cell /
                    terrain.CellCount;

                AddTerrainLine(tile, offset, 0, offset, 300, originX, originZ, TerrainColor, AddLine);
                AddTerrainLine(tile, 0, offset, 300, offset, originX, originZ, TerrainColor, AddLine);
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

            AddLine(
                item.WorldX - marker,
                baseHeight,
                item.WorldZ,
                item.WorldX + marker,
                baseHeight,
                item.WorldZ,
                ObjectColor);

            AddLine(
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
                scene,
                item,
                AddLine);
        }

        return new NativeMapGeometry(
            vertices.ToArray());
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
        NativeSceneSnapshot scene,
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
        var spline =
            entity.Spline;

        var length =
            Math.Max(
                0.0,
                spline.Length);

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
            GetSplinePoint(
                scene,
                entity,
                0);

        for (
            var index = 1;
            index <= segmentCount;
            index++)
        {
            var current =
                GetSplinePoint(
                    scene,
                    entity,
                    length *
                    index /
                    segmentCount);

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

    private static Vector3 GetSplinePoint(
        NativeSceneSnapshot scene,
        NativeSplineEntity entity,
        double distance)
    {
        var spline =
            entity.Spline;

        var yaw =
            spline.Rotation *
            Math.PI /
            180.0;

        var hasCurve =
            Math.Abs(
                spline.Radius) >
            0.001;

        var angle =
            hasCurve
                ? distance /
                  spline.Radius
                : 0.0;

        var localX =
            hasCurve
                ? spline.Radius *
                  (
                      1.0 -
                      Math.Cos(
                          angle)
                  )
                : 0.0;

        var localZ =
            hasCurve
                ? spline.Radius *
                  Math.Sin(
                      angle)
                : distance;

        var cosYaw =
            Math.Cos(yaw);

        var sinYaw =
            Math.Sin(yaw);

        var worldX =
            entity.WorldX +
            localX * cosYaw +
            localZ * sinYaw;

        var worldZ =
            entity.WorldZ -
            localX * sinYaw +
            localZ * cosYaw;

        var worldY =
            entity.WorldY +
            NativeTerrainSampler
                .GetHeightAtWorldPoint(
                    scene,
                    worldX,
                    worldZ) +
            0.30;

        return new Vector3(
            (float)worldX,
            (float)worldY,
            (float)worldZ);
    }
}
