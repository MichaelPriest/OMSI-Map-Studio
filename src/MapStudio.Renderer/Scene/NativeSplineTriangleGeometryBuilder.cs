using System.Numerics;
using MapStudio.Renderer.Picking;

namespace MapStudio.Renderer.Scene;

public sealed record NativeSplineTriangleGeometry(
    NativeMapVertex[] Vertices,
    NativeMapVertex[] PickingVertices,
    IReadOnlyDictionary<
        PickingId,
        NativeTriangleRange> Ranges,
    IReadOnlyList<
        NativeMaterialBatch> MaterialBatches,
    int LoadedSplineCount,
    int RenderedSurfaceCount)
{
    public int TriangleCount =>
        Vertices.Length / 3;

    public int TexturedBatchCount =>
        MaterialBatches.Count(
            batch =>
                !string.IsNullOrWhiteSpace(
                    batch.TexturePath));
}

public sealed class NativeSplineTriangleGeometryBuilder
{
    private static readonly Vector4
        SurfaceColor =
            new(
                1.0f,
                1.0f,
                1.0f,
                1.0f);

    public NativeSplineTriangleGeometry Build(
        NativeSceneSnapshot scene,
        IReadOnlyDictionary<
            string,
            NativeSplineAsset> assets)
    {
        ArgumentNullException.ThrowIfNull(
            scene);

        ArgumentNullException.ThrowIfNull(
            assets);

        var vertices =
            new List<NativeMapVertex>(
                32_768);

        var pickingVertices =
            new List<NativeMapVertex>(
                32_768);

        var ranges =
            new Dictionary<
                PickingId,
                NativeTriangleRange>();

        var materialBatches =
            new List<
                NativeMaterialBatch>();

        var loadedSplines = 0;
        var renderedSurfaces = 0;

        foreach (
            var entity in scene.Splines)
        {
            if (
                !assets.TryGetValue(
                    entity.Spline
                        .SplinePath,
                    out var asset) ||
                !asset.IsLoaded)
            {
                continue;
            }

            var length =
                Math.Max(
                    0.0,
                    entity.Spline.Length);

            if (length < 0.01)
            {
                continue;
            }

            var start =
                vertices.Count;

            var segmentCount =
                Math.Clamp(
                    (int)Math.Ceiling(
                        length /
                        4.0),
                    1,
                    256);

            var pickingColor =
                EncodePickingColor(
                    entity.PickingId);

            foreach (
                var surface in
                    asset.Definition
                        .Surfaces)
            {
                var surfaceStart =
                    vertices.Count;

                for (
                    var segment = 0;
                    segment < segmentCount;
                    segment++)
                {
                    var distance0 =
                        length *
                        segment /
                        segmentCount;

                    var distance1 =
                        length *
                        (segment + 1) /
                        segmentCount;

                    var frame0 =
                        NativeSplinePathMath
                            .GetFrame(
                                entity,
                                distance0);

                    var frame1 =
                        NativeSplinePathMath
                            .GetFrame(
                                entity,
                                distance1);

                    var left0 =
                        NativeSplinePathMath
                            .TransformProfilePoint(
                                frame0,
                                surface.From);

                    var right0 =
                        NativeSplinePathMath
                            .TransformProfilePoint(
                                frame0,
                                surface.To);

                    var left1 =
                        NativeSplinePathMath
                            .TransformProfilePoint(
                                frame1,
                                surface.From);

                    var right1 =
                        NativeSplinePathMath
                            .TransformProfilePoint(
                                frame1,
                                surface.To);

                    var leftUv0 =
                        new Vector2(
                            (float)
                                surface.From
                                    .TextureX,
                            (float)(
                                distance0 *
                                surface.From
                                    .TextureScale));

                    var leftUv1 =
                        new Vector2(
                            (float)
                                surface.From
                                    .TextureX,
                            (float)(
                                distance1 *
                                surface.From
                                    .TextureScale));

                    var rightUv0 =
                        new Vector2(
                            (float)
                                surface.To
                                    .TextureX,
                            (float)(
                                distance0 *
                                surface.To
                                    .TextureScale));

                    var rightUv1 =
                        new Vector2(
                            (float)
                                surface.To
                                    .TextureX,
                            (float)(
                                distance1 *
                                surface.To
                                    .TextureScale));

                    AppendQuad(
                        left0,
                        left1,
                        right1,
                        right0,
                        leftUv0,
                        leftUv1,
                        rightUv1,
                        rightUv0,
                        SurfaceColor,
                        pickingColor,
                        vertices,
                        pickingVertices);
                }

                var surfaceVertexCount =
                    vertices.Count -
                    surfaceStart;

                if (surfaceVertexCount <= 0)
                {
                    continue;
                }

                renderedSurfaces++;

                materialBatches.Add(
                    new NativeMaterialBatch(
                        surfaceStart,
                        surfaceVertexCount,
                        ResolveTexturePath(
                            asset,
                            surface.TextureIndex),
                        AlphaMode:
                            surface.AlphaMode,
                        DoubleSided:
                            true));
            }

            if (vertices.Count > start)
            {
                loadedSplines++;

                ranges[
                    entity.PickingId] =
                    new NativeTriangleRange(
                        start,
                        vertices.Count -
                            start);
            }
        }

        return new NativeSplineTriangleGeometry(
            vertices.ToArray(),
            pickingVertices.ToArray(),
            ranges,
            materialBatches.ToArray(),
            loadedSplines,
            renderedSurfaces);
    }

    private static string?
        ResolveTexturePath(
            NativeSplineAsset asset,
            int textureIndex)
    {
        if (
            textureIndex < 0 ||
            textureIndex >=
                asset.TexturePaths
                    .Count)
        {
            return null;
        }

        return asset.TexturePaths[
            textureIndex];
    }

    private static void AppendQuad(
        Vector3 left0,
        Vector3 left1,
        Vector3 right1,
        Vector3 right0,
        Vector2 leftUv0,
        Vector2 leftUv1,
        Vector2 rightUv1,
        Vector2 rightUv0,
        Vector4 color,
        Vector4 pickingColor,
        List<NativeMapVertex> output,
        List<NativeMapVertex>
            pickingOutput)
    {
        AppendTriangle(
            left0,
            left1,
            right1,
            leftUv0,
            leftUv1,
            rightUv1,
            color,
            pickingColor,
            output,
            pickingOutput);

        AppendTriangle(
            left0,
            right1,
            right0,
            leftUv0,
            rightUv1,
            rightUv0,
            color,
            pickingColor,
            output,
            pickingOutput);
    }

    private static void AppendTriangle(
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Vector2 uvA,
        Vector2 uvB,
        Vector2 uvC,
        Vector4 color,
        Vector4 pickingColor,
        List<NativeMapVertex> output,
        List<NativeMapVertex>
            pickingOutput)
    {
        output.Add(
            new NativeMapVertex(
                a,
                color,
                uvA));

        output.Add(
            new NativeMapVertex(
                b,
                color,
                uvB));

        output.Add(
            new NativeMapVertex(
                c,
                color,
                uvC));

        pickingOutput.Add(
            new NativeMapVertex(
                a,
                pickingColor));

        pickingOutput.Add(
            new NativeMapVertex(
                b,
                pickingColor));

        pickingOutput.Add(
            new NativeMapVertex(
                c,
                pickingColor));
    }

    private static Vector4 EncodePickingColor(
        PickingId pickingId)
    {
        var encoded =
            PickingColorCodec
                .Encode(pickingId);

        return new Vector4(
            (encoded & 0xFF) /
                255f,
            (
                (encoded >> 8) &
                0xFF
            ) /
                255f,
            (
                (encoded >> 16) &
                0xFF
            ) /
                255f,
            (
                (encoded >> 24) &
                0xFF
            ) /
                255f);
    }
}
