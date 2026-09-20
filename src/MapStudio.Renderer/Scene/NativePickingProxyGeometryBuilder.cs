using System.Numerics;
using MapStudio.Renderer.Picking;

namespace MapStudio.Renderer.Scene;

public sealed record NativePickingProxyGeometry(
    NativeMapVertex[] Vertices,
    IReadOnlyDictionary<
        PickingId,
        NativeTriangleRange> Ranges);

public sealed class NativePickingProxyGeometryBuilder
{
    public NativePickingProxyGeometry Build(
        NativeSceneSnapshot scene,
        IReadOnlyDictionary<
            string,
            NativeSceneryAsset>?
            assets = null)
    {
        ArgumentNullException.ThrowIfNull(
            scene);

        var vertices =
            new List<NativeMapVertex>(
                Math.Max(
                    1024,
                    scene.SelectableCount *
                    36));

        var ranges =
            new Dictionary<
                PickingId,
                NativeTriangleRange>();

        foreach (
            var entity in scene.Objects)
        {
            var start =
                vertices.Count;

            AppendObjectProxy(
                scene,
                entity,
                assets,
                vertices);

            ranges[
                entity.PickingId] =
                new NativeTriangleRange(
                    start,
                    vertices.Count -
                        start);
        }

        foreach (
            var entity in scene.Splines)
        {
            var start =
                vertices.Count;

            AppendSplineProxy(
                entity,
                vertices);

            if (
                vertices.Count > start)
            {
                ranges[
                    entity.PickingId] =
                    new NativeTriangleRange(
                        start,
                        vertices.Count -
                            start);
            }
        }

        return new NativePickingProxyGeometry(
            vertices.ToArray(),
            ranges);
    }

    private static void AppendObjectProxy(
        NativeSceneSnapshot scene,
        NativeObjectEntity entity,
        IReadOnlyDictionary<
            string,
            NativeSceneryAsset>?
            assets,
        List<NativeMapVertex> output)
    {
        var usesAbsoluteHeight =
            assets is not null &&
            assets.TryGetValue(
                entity.Object
                    .SceneryObjectPath,
                out var asset) &&
            asset.UsesAbsoluteHeight;

        var terrainOffset =
            usesAbsoluteHeight
                ? 0.0
                : NativeTerrainSampler
                    .GetHeightAtObject(
                        scene,
                        entity);

        var baseY =
            entity.WorldY +
            terrainOffset;

        const float halfSize =
            3.5f;

        const float proxyHeight =
            7.0f;

        var min =
            new Vector3(
                entity.WorldX -
                halfSize,
                (float)baseY +
                0.10f,
                entity.WorldZ -
                halfSize);

        var max =
            new Vector3(
                entity.WorldX +
                halfSize,
                (float)baseY +
                proxyHeight,
                entity.WorldZ +
                halfSize);

        AppendBox(
            min,
            max,
            EncodePickingColor(
                entity.PickingId),
            output);
    }

    private static void AppendSplineProxy(
        NativeSplineEntity entity,
        List<NativeMapVertex> output)
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

        var color =
            EncodePickingColor(
                entity.PickingId);

        var previous =
            GetSplinePoint(
                entity,
                0);

        for (
            var index = 1;
            index <= segmentCount;
            index++)
        {
            var current =
                GetSplinePoint(
                    entity,
                    length *
                    index /
                    segmentCount);

            AppendThickSegment(
                previous,
                current,
                color,
                output);

            previous = current;
        }
    }

    private static Vector3 GetSplinePoint(
        NativeSplineEntity entity,
        double distance)
    {
        var frame =
            NativeSplinePathMath
                .GetFrame(
                    entity,
                    distance);

        return
            frame.Center +
            Vector3.UnitY *
            0.45f;
    }


    private static void AppendThickSegment(
        Vector3 start,
        Vector3 end,
        Vector4 color,
        List<NativeMapVertex> output)
    {
        var dx =
            end.X -
            start.X;

        var dz =
            end.Z -
            start.Z;

        var length =
            Math.Sqrt(
                dx * dx +
                dz * dz);

        if (length < 0.0001)
        {
            return;
        }

        const float halfWidth =
            3.0f;

        var px =
            (float)(
                -dz /
                length *
                halfWidth);

        var pz =
            (float)(
                dx /
                length *
                halfWidth);

        AppendQuad(
            new Vector3(
                start.X + px,
                start.Y,
                start.Z + pz),
            new Vector3(
                end.X + px,
                end.Y,
                end.Z + pz),
            new Vector3(
                end.X - px,
                end.Y,
                end.Z - pz),
            new Vector3(
                start.X - px,
                start.Y,
                start.Z - pz),
            color,
            output);
    }

    private static void AppendBox(
        Vector3 min,
        Vector3 max,
        Vector4 color,
        List<NativeMapVertex> output)
    {
        var a = new Vector3(min.X, min.Y, min.Z);
        var b = new Vector3(max.X, min.Y, min.Z);
        var c = new Vector3(max.X, min.Y, max.Z);
        var d = new Vector3(min.X, min.Y, max.Z);
        var e = new Vector3(min.X, max.Y, min.Z);
        var f = new Vector3(max.X, max.Y, min.Z);
        var g = new Vector3(max.X, max.Y, max.Z);
        var h = new Vector3(min.X, max.Y, max.Z);

        AppendQuad(a, d, c, b, color, output);
        AppendQuad(e, f, g, h, color, output);
        AppendQuad(a, b, f, e, color, output);
        AppendQuad(b, c, g, f, color, output);
        AppendQuad(c, d, h, g, color, output);
        AppendQuad(d, a, e, h, color, output);
    }

    private static void AppendQuad(
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Vector3 d,
        Vector4 color,
        List<NativeMapVertex> output)
    {
        output.Add(new NativeMapVertex(a, color));
        output.Add(new NativeMapVertex(b, color));
        output.Add(new NativeMapVertex(c, color));
        output.Add(new NativeMapVertex(a, color));
        output.Add(new NativeMapVertex(c, color));
        output.Add(new NativeMapVertex(d, color));
    }

    private static Vector4 EncodePickingColor(
        PickingId pickingId)
    {
        var encoded =
            PickingColorCodec
                .Encode(pickingId);

        return new Vector4(
            (encoded & 0xFF) / 255f,
            (
                (encoded >> 8) &
                0xFF
            ) / 255f,
            (
                (encoded >> 16) &
                0xFF
            ) / 255f,
            (
                (encoded >> 24) &
                0xFF
            ) / 255f);
    }
}
