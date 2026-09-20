using System.Numerics;
using MapStudio.Renderer.Picking;

namespace MapStudio.Renderer.Scene;

public sealed record NativeSplineTriangleGeometry(
    NativeMapVertex[] Vertices,
    NativeMapVertex[] PickingVertices,
    IReadOnlyDictionary<
        PickingId,
        NativeTriangleRange> Ranges,
    int LoadedSplineCount,
    int RenderedSurfaceCount)
{
    public int TriangleCount =>
        Vertices.Length / 3;
}

public sealed class NativeSplineTriangleGeometryBuilder
{
    private static readonly Vector4
        SurfaceColor =
            new(
                0.36f,
                0.39f,
                0.41f,
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
                var contributed =
                    false;

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

                    AppendQuad(
                        left0,
                        left1,
                        right1,
                        right0,
                        SurfaceColor,
                        pickingColor,
                        vertices,
                        pickingVertices);

                    contributed = true;
                }

                if (contributed)
                {
                    renderedSurfaces++;
                }
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
            loadedSplines,
            renderedSurfaces);
    }

    private static void AppendQuad(
        Vector3 left0,
        Vector3 left1,
        Vector3 right1,
        Vector3 right0,
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
            color,
            pickingColor,
            output,
            pickingOutput);

        AppendTriangle(
            left0,
            right1,
            right0,
            color,
            pickingColor,
            output,
            pickingOutput);
    }

    private static void AppendTriangle(
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Vector4 color,
        Vector4 pickingColor,
        List<NativeMapVertex> output,
        List<NativeMapVertex>
            pickingOutput)
    {
        output.Add(
            new NativeMapVertex(
                a,
                color));

        output.Add(
            new NativeMapVertex(
                b,
                color));

        output.Add(
            new NativeMapVertex(
                c,
                color));

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
