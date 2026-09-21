using System.Numerics;

namespace MapStudio.Renderer.Scene;

public sealed record NativeWaterTriangleGeometry(
    NativeMapVertex[] Vertices,
    int RenderedTileCount)
{
    public int TriangleCount =>
        Vertices.Length /
        3;
}

public sealed class NativeWaterTriangleGeometryBuilder
{
    private static readonly Vector4
        WaterPreviewColor =
            new(
                0.08f,
                0.38f,
                0.70f,
                0.58f);

    public NativeWaterTriangleGeometry Build(
        NativeSceneSnapshot scene)
    {
        ArgumentNullException.ThrowIfNull(
            scene);

        var vertices =
            new List<NativeMapVertex>(
                scene.Tiles.Count *
                6);

        var renderedTiles =
            0;

        foreach (
            var tile in scene.Tiles)
        {
            var water =
                tile.Content.Water;

            if (
                water is null ||
                water.Heights.Count !=
                    4 ||
                water.Heights.Any(
                    height =>
                        !float.IsFinite(
                            height)))
            {
                continue;
            }

            var originX =
                tile.Reference.X *
                300.0f;

            var originZ =
                tile.Reference.Y *
                300.0f;

            var h00 =
                water.Heights[0];

            var h10 =
                water.Heights[1];

            var h01 =
                water.Heights[2];

            var h11 =
                water.Heights[3];

            AddVertex(
                vertices,
                originX,
                h00,
                originZ,
                0,
                0);

            AddVertex(
                vertices,
                originX +
                    300.0f,
                h11,
                originZ +
                    300.0f,
                1,
                1);

            AddVertex(
                vertices,
                originX +
                    300.0f,
                h10,
                originZ,
                1,
                0);

            AddVertex(
                vertices,
                originX,
                h00,
                originZ,
                0,
                0);

            AddVertex(
                vertices,
                originX,
                h01,
                originZ +
                    300.0f,
                0,
                1);

            AddVertex(
                vertices,
                originX +
                    300.0f,
                h11,
                originZ +
                    300.0f,
                1,
                1);

            renderedTiles++;
        }

        return new NativeWaterTriangleGeometry(
            vertices.ToArray(),
            renderedTiles);
    }

    private static void AddVertex(
        ICollection<NativeMapVertex> output,
        float x,
        float height,
        float z,
        float u,
        float v)
    {
        output.Add(
            new NativeMapVertex(
                new Vector3(
                    x,
                    height,
                    z),
                WaterPreviewColor,
                new Vector2(
                    u,
                    v)));
    }
}
