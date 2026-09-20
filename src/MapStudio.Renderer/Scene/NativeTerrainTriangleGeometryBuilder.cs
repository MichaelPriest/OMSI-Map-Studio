using System.Numerics;

namespace MapStudio.Renderer.Scene;

public sealed record NativeTerrainTriangleGeometry(
    NativeMapVertex[] Vertices)
{
    public int TriangleCount => Vertices.Length / 3;
}

public sealed class NativeTerrainTriangleGeometryBuilder
{
    public NativeTerrainTriangleGeometry Build(
        NativeSceneSnapshot scene)
    {
        ArgumentNullException.ThrowIfNull(scene);

        var vertices =
            new List<NativeMapVertex>(
                32_768);

        foreach (var tile in scene.Tiles)
        {
            var terrain = tile.Content.Terrain;

            if (terrain is null || terrain.CellCount <= 0)
            {
                continue;
            }

            var cellCount = terrain.CellCount;
            var sampleCount = cellCount + 1;

            if (terrain.Heights.Count != sampleCount * sampleCount)
            {
                continue;
            }

            var spacing = 300.0 / cellCount;
            var originX = tile.Reference.X * 300.0;
            var originZ = tile.Reference.Y * 300.0;

            for (var row = 0; row < cellCount; row++)
            {
                for (var column = 0; column < cellCount; column++)
                {
                    var topLeft = row * sampleCount + column;
                    var topRight = topLeft + 1;
                    var bottomLeft = topLeft + sampleCount;
                    var bottomRight = bottomLeft + 1;

                    var h00 = terrain.Heights[topLeft];
                    var h10 = terrain.Heights[topRight];
                    var h01 = terrain.Heights[bottomLeft];
                    var h11 = terrain.Heights[bottomRight];

                    if (
                        !float.IsFinite(h00) ||
                        !float.IsFinite(h10) ||
                        !float.IsFinite(h01) ||
                        !float.IsFinite(h11))
                    {
                        continue;
                    }

                    var x0 = originX + column * spacing;
                    var x1 = x0 + spacing;
                    var z0 = originZ + row * spacing;
                    var z1 = z0 + spacing;

                    var averageHeight =
                        (h00 + h10 + h01 + h11) /
                        4.0f;

                    var color =
                        GetTerrainColor(
                            averageHeight);

                    AppendTriangle(
                        x0, z0, h00,
                        x1, z1, h11,
                        x1, z0, h10,
                        color,
                        vertices);

                    AppendTriangle(
                        x0, z0, h00,
                        x0, z1, h01,
                        x1, z1, h11,
                        color,
                        vertices);
                }
            }
        }

        return new NativeTerrainTriangleGeometry(
            vertices.ToArray());
    }

    private static void AppendTriangle(
        double x0,
        double z0,
        float height0,
        double x1,
        double z1,
        float height1,
        double x2,
        double z2,
        float height2,
        Vector4 color,
        List<NativeMapVertex> output)
    {
        output.Add(
            new NativeMapVertex(
                new Vector3(
                    (float)x0,
                    height0,
                    (float)z0),
                color));

        output.Add(
            new NativeMapVertex(
                new Vector3(
                    (float)x1,
                    height1,
                    (float)z1),
                color));

        output.Add(
            new NativeMapVertex(
                new Vector3(
                    (float)x2,
                    height2,
                    (float)z2),
                color));
    }

    private static Vector4 GetTerrainColor(
        float height)
    {
        var normalized =
            Math.Clamp(
                0.5f +
                height /
                120.0f,
                0.0f,
                1.0f);

        return new Vector4(
            0.10f + normalized * 0.08f,
            0.20f + normalized * 0.18f,
            0.10f + normalized * 0.08f,
            1.0f);
    }
}
