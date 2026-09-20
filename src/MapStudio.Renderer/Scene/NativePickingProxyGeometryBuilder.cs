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
        NativeSceneSnapshot scene)
    {
        ArgumentNullException.ThrowIfNull(
            scene);

        var projection =
            NativeSceneProjection
                .FromScene(scene);

        var vertices =
            new List<NativeMapVertex>(
                Math.Max(
                    1024,
                    scene.SelectableCount *
                    12));

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
                entity,
                projection,
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
                projection,
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
        NativeObjectEntity entity,
        NativeSceneProjection projection,
        List<NativeMapVertex> output)
    {
        const double halfSize = 4.0;

        var left =
            entity.WorldX -
            halfSize;

        var right =
            entity.WorldX +
            halfSize;

        var top =
            entity.WorldZ -
            halfSize;

        var bottom =
            entity.WorldZ +
            halfSize;

        var color =
            EncodePickingColor(
                entity.PickingId);

        AppendQuad(
            projection
                .ProjectTopDown(
                    left,
                    top,
                    0.80f),
            projection
                .ProjectTopDown(
                    right,
                    top,
                    0.80f),
            projection
                .ProjectTopDown(
                    right,
                    bottom,
                    0.80f),
            projection
                .ProjectTopDown(
                    left,
                    bottom,
                    0.80f),
            color,
            output);
    }

    private static void AppendSplineProxy(
        NativeSplineEntity entity,
        NativeSceneProjection projection,
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
                projection,
                color,
                output);

            previous = current;
        }
    }

    private static (
        double X,
        double Z)
        GetSplinePoint(
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

        return (
            entity.WorldX +
            localX * cosYaw +
            localZ * sinYaw,
            entity.WorldZ -
            localX * sinYaw +
            localZ * cosYaw);
    }

    private static void AppendThickSegment(
        (double X, double Z) start,
        (double X, double Z) end,
        NativeSceneProjection projection,
        Vector4 color,
        List<NativeMapVertex> output)
    {
        var dx =
            end.X - start.X;

        var dz =
            end.Z - start.Z;

        var length =
            Math.Sqrt(
                dx * dx +
                dz * dz);

        if (length < 0.0001)
        {
            return;
        }

        const double halfWidth =
            3.0;

        var px =
            -dz /
            length *
            halfWidth;

        var pz =
            dx /
            length *
            halfWidth;

        AppendQuad(
            projection
                .ProjectTopDown(
                    start.X + px,
                    start.Z + pz,
                    0.75f),
            projection
                .ProjectTopDown(
                    end.X + px,
                    end.Z + pz,
                    0.75f),
            projection
                .ProjectTopDown(
                    end.X - px,
                    end.Z - pz,
                    0.75f),
            projection
                .ProjectTopDown(
                    start.X - px,
                    start.Z - pz,
                    0.75f),
            color,
            output);
    }

    private static void AppendQuad(
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Vector3 d,
        Vector4 color,
        List<NativeMapVertex> output)
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

        output.Add(
            new NativeMapVertex(
                a,
                color));

        output.Add(
            new NativeMapVertex(
                c,
                color));

        output.Add(
            new NativeMapVertex(
                d,
                color));
    }

    private static Vector4
        EncodePickingColor(
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
