using System.Numerics;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Textures;

namespace MapStudio.Renderer.Scene;

public sealed record NativeTerrainTriangleGeometry(
    NativeMapVertex[] Vertices,
    IReadOnlyList<
        NativeMaterialBatch> MaterialBatches)
{
    public int TriangleCount =>
        Vertices.Length / 3;

    public int TexturedBatchCount =>
        MaterialBatches.Count(
            batch =>
                !string.IsNullOrWhiteSpace(
                    batch.TexturePath));
}

public sealed class NativeTerrainTriangleGeometryBuilder
{
    private static readonly Vector4
        TexturedColor =
            Vector4.One;

    public NativeTerrainTriangleGeometry Build(
        NativeSceneSnapshot scene) =>
        BuildCore(
            scene,
            baseTexturePath: null,
            mainRepeating: 1.0);

    public NativeTerrainTriangleGeometry Build(
        NativeSceneSnapshot scene,
        OmsiMapDescriptor map,
        string omsiRoot)
    {
        ArgumentNullException.ThrowIfNull(
            map);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            omsiRoot);

        string? baseTexturePath =
            null;

        var mainRepeating =
            1.0;

        if (
            map.GroundTextures.Count >
                0)
        {
            var ground =
                map.GroundTextures[0];

            mainRepeating =
                ground.MainTextureRepeating;

            if (
                OmsiTextureAssetPathResolver
                    .TryResolveGroundTexture(
                        omsiRoot,
                        map.DirectoryPath,
                        ground.MainTexturePath,
                        out var resolved))
            {
                baseTexturePath =
                    resolved;
            }
        }

        return BuildCore(
            scene,
            baseTexturePath,
            mainRepeating);
    }

    private static NativeTerrainTriangleGeometry BuildCore(
        NativeSceneSnapshot scene,
        string? baseTexturePath,
        double mainRepeating)
    {
        ArgumentNullException.ThrowIfNull(
            scene);

        var vertices =
            new List<NativeMapVertex>(
                32_768);

        var batches =
            new List<
                NativeMaterialBatch>();

        foreach (var tile in scene.Tiles)
        {
            var terrain =
                tile.Content.Terrain;

            if (
                terrain is null ||
                terrain.CellCount <= 0)
            {
                continue;
            }

            var cellCount =
                terrain.CellCount;

            var sampleCount =
                cellCount + 1;

            if (
                terrain.Heights.Count !=
                sampleCount *
                sampleCount)
            {
                continue;
            }

            var tileStart =
                vertices.Count;

            var spacing =
                300.0 /
                cellCount;

            var originX =
                tile.Reference.X *
                300.0;

            var originZ =
                tile.Reference.Y *
                300.0;

            for (
                var row = 0;
                row < cellCount;
                row++)
            {
                for (
                    var column = 0;
                    column < cellCount;
                    column++)
                {
                    var topLeft =
                        row *
                        sampleCount +
                        column;

                    var topRight =
                        topLeft + 1;

                    var bottomLeft =
                        topLeft +
                        sampleCount;

                    var bottomRight =
                        bottomLeft + 1;

                    var h00 =
                        terrain.Heights[
                            topLeft];

                    var h10 =
                        terrain.Heights[
                            topRight];

                    var h01 =
                        terrain.Heights[
                            bottomLeft];

                    var h11 =
                        terrain.Heights[
                            bottomRight];

                    if (
                        !float.IsFinite(h00) ||
                        !float.IsFinite(h10) ||
                        !float.IsFinite(h01) ||
                        !float.IsFinite(h11))
                    {
                        continue;
                    }

                    var localX0 =
                        column *
                        spacing;

                    var localX1 =
                        localX0 +
                        spacing;

                    var localZ0 =
                        row *
                        spacing;

                    var localZ1 =
                        localZ0 +
                        spacing;

                    var x0 =
                        originX +
                        localX0;

                    var x1 =
                        originX +
                        localX1;

                    var z0 =
                        originZ +
                        localZ0;

                    var z1 =
                        originZ +
                        localZ1;

                    var averageHeight =
                        (
                            h00 +
                            h10 +
                            h01 +
                            h11
                        ) /
                        4.0f;

                    var color =
                        baseTexturePath is null
                            ? GetTerrainColor(
                                averageHeight)
                            : TexturedColor;

                    var uv00 =
                        CreateUv(
                            localX0,
                            localZ0,
                            mainRepeating);

                    var uv10 =
                        CreateUv(
                            localX1,
                            localZ0,
                            mainRepeating);

                    var uv01 =
                        CreateUv(
                            localX0,
                            localZ1,
                            mainRepeating);

                    var uv11 =
                        CreateUv(
                            localX1,
                            localZ1,
                            mainRepeating);

                    AppendTriangle(
                        x0,
                        z0,
                        h00,
                        uv00,
                        x1,
                        z1,
                        h11,
                        uv11,
                        x1,
                        z0,
                        h10,
                        uv10,
                        color,
                        vertices);

                    AppendTriangle(
                        x0,
                        z0,
                        h00,
                        uv00,
                        x0,
                        z1,
                        h01,
                        uv01,
                        x1,
                        z1,
                        h11,
                        uv11,
                        color,
                        vertices);
                }
            }

            var tileVertexCount =
                vertices.Count -
                tileStart;

            if (tileVertexCount > 0)
            {
                AppendBatch(
                    batches,
                    tileStart,
                    tileVertexCount,
                    baseTexturePath);
            }
        }

        return new NativeTerrainTriangleGeometry(
            vertices.ToArray(),
            batches.ToArray());
    }

    private static Vector2 CreateUv(
        double localX,
        double localZ,
        double repeating)
    {
        var safeRepeating =
            double.IsFinite(
                repeating) &&
            repeating > 0
                ? repeating
                : 1.0;

        return new Vector2(
            (float)(
                localX /
                300.0 *
                safeRepeating),
            (float)(
                localZ /
                300.0 *
                safeRepeating));
    }

    private static void AppendBatch(
        List<NativeMaterialBatch> batches,
        int startVertex,
        int vertexCount,
        string? texturePath)
    {
        if (
            batches.Count > 0)
        {
            var previous =
                batches[^1];

            if (
                previous.StartVertex +
                    previous.VertexCount ==
                    startVertex &&
                string.Equals(
                    previous.TexturePath,
                    texturePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                batches[^1] =
                    previous with
                    {
                        VertexCount =
                            previous.VertexCount +
                            vertexCount
                    };

                return;
            }
        }

        batches.Add(
            new NativeMaterialBatch(
                startVertex,
                vertexCount,
                texturePath));
    }

    private static void AppendTriangle(
        double x0,
        double z0,
        float height0,
        Vector2 uv0,
        double x1,
        double z1,
        float height1,
        Vector2 uv1,
        double x2,
        double z2,
        float height2,
        Vector2 uv2,
        Vector4 color,
        List<NativeMapVertex> output)
    {
        output.Add(
            new NativeMapVertex(
                new Vector3(
                    (float)x0,
                    height0,
                    (float)z0),
                color,
                uv0));

        output.Add(
            new NativeMapVertex(
                new Vector3(
                    (float)x1,
                    height1,
                    (float)z1),
                color,
                uv1));

        output.Add(
            new NativeMapVertex(
                new Vector3(
                    (float)x2,
                    height2,
                    (float)z2),
                color,
                uv2));
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
            0.10f +
                normalized *
                0.08f,
            0.20f +
                normalized *
                0.18f,
            0.10f +
                normalized *
                0.08f,
            1.0f);
    }
}
