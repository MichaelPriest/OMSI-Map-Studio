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

        var minX =
            scene.Tiles.Min(
                tile =>
                    tile.Reference.X *
                    300.0);

        var maxX =
            scene.Tiles.Max(
                tile =>
                    tile.Reference.X *
                    300.0 +
                    300.0);

        var minZ =
            scene.Tiles.Min(
                tile =>
                    tile.Reference.Y *
                    300.0);

        var maxZ =
            scene.Tiles.Max(
                tile =>
                    tile.Reference.Y *
                    300.0 +
                    300.0);

        var centerX =
            (minX + maxX) * 0.5;

        var centerZ =
            (minZ + maxZ) * 0.5;

        var halfWidth =
            Math.Max(
                1.0,
                (maxX - minX) *
                0.55);

        var halfHeight =
            Math.Max(
                1.0,
                (maxZ - minZ) *
                0.55);

        var vertices =
            new List<NativeMapVertex>(
                8192);

        Vector3 Project(
            double worldX,
            double worldZ)
        {
            return new Vector3(
                (float)(
                    (worldX - centerX) /
                    halfWidth),
                (float)(
                    (worldZ - centerZ) /
                    halfHeight),
                0.0f);
        }

        void AddLine(
            double x1,
            double z1,
            double x2,
            double z2,
            Vector4 color)
        {
            vertices.Add(
                new NativeMapVertex(
                    Project(x1, z1),
                    color));

            vertices.Add(
                new NativeMapVertex(
                    Project(x2, z2),
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

            AddLine(
                originX,
                originZ,
                originX + 300,
                originZ,
                TerrainColor);

            AddLine(
                originX + 300,
                originZ,
                originX + 300,
                originZ + 300,
                TerrainColor);

            AddLine(
                originX + 300,
                originZ + 300,
                originX,
                originZ + 300,
                TerrainColor);

            AddLine(
                originX,
                originZ + 300,
                originX,
                originZ,
                TerrainColor);

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

                AddLine(
                    originX + offset,
                    originZ,
                    originX + offset,
                    originZ + 300,
                    TerrainColor);

                AddLine(
                    originX,
                    originZ + offset,
                    originX + 300,
                    originZ + offset,
                    TerrainColor);
            }
        }

        foreach (
            var item in scene.Objects)
        {
            const double marker = 3.5;

            AddLine(
                item.WorldX - marker,
                item.WorldZ,
                item.WorldX + marker,
                item.WorldZ,
                ObjectColor);

            AddLine(
                item.WorldX,
                item.WorldZ - marker,
                item.WorldX,
                item.WorldZ + marker,
                ObjectColor);
        }

        foreach (
            var item in scene.Splines)
        {
            AddSpline(
                item,
                AddLine);
        }

        return new NativeMapGeometry(
            vertices.ToArray());
    }

    private static void AddSpline(
        NativeSplineEntity entity,
        Action<
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

        var yaw =
            spline.Rotation *
            Math.PI /
            180.0;

        (double X, double Z) Point(
            double distance)
        {
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

            return (
                entity.WorldX +
                localX * cosYaw +
                localZ * sinYaw,
                entity.WorldZ -
                localX * sinYaw +
                localZ * cosYaw);
        }

        var previous =
            Point(0);

        for (
            var index = 1;
            index <= segmentCount;
            index++)
        {
            var current =
                Point(
                    length *
                    index /
                    segmentCount);

            addLine(
                previous.X,
                previous.Z,
                current.X,
                current.Z,
                SplineColor);

            previous = current;
        }
    }
}
