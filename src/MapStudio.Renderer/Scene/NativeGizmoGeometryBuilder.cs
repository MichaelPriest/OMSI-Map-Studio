using System.Numerics;
using MapStudio.Renderer.Picking;

namespace MapStudio.Renderer.Scene;

public sealed class NativeGizmoGeometryBuilder
{
    private static readonly Vector4 XColor =
        new(0.95f, 0.18f, 0.12f, 1.0f);

    private static readonly Vector4 YColor =
        new(0.20f, 0.86f, 0.28f, 1.0f);

    private static readonly Vector4 ZColor =
        new(0.12f, 0.42f, 1.0f, 1.0f);

    public NativeGizmoGeometry Build(
        Vector3 anchor,
        NativeGizmoMode mode,
        float size,
        bool fullRotation)
    {
        size =
            Math.Clamp(
                size,
                1.0f,
                250.0f);

        var vertices =
            new List<NativeMapVertex>(
                2048);

        var picking =
            new List<NativeMapVertex>(
                2048);

        if (mode == NativeGizmoMode.Move)
        {
            AppendMoveHandle(
                anchor,
                Vector3.UnitX,
                NativeGizmoHandle.MoveX,
                XColor,
                size,
                vertices,
                picking);

            AppendMoveHandle(
                anchor,
                Vector3.UnitY,
                NativeGizmoHandle.MoveY,
                YColor,
                size,
                vertices,
                picking);

            AppendMoveHandle(
                anchor,
                Vector3.UnitZ,
                NativeGizmoHandle.MoveZ,
                ZColor,
                size,
                vertices,
                picking);
        }
        else
        {
            if (fullRotation)
            {
                AppendRotationHandle(
                    anchor,
                    Vector3.UnitX,
                    Vector3.UnitY,
                    Vector3.UnitZ,
                    NativeGizmoHandle.RotateX,
                    XColor,
                    size,
                    vertices,
                    picking);
            }

            AppendRotationHandle(
                anchor,
                Vector3.UnitY,
                Vector3.UnitX,
                Vector3.UnitZ,
                NativeGizmoHandle.RotateY,
                YColor,
                size,
                vertices,
                picking);

            if (fullRotation)
            {
                AppendRotationHandle(
                    anchor,
                    Vector3.UnitZ,
                    Vector3.UnitX,
                    Vector3.UnitY,
                    NativeGizmoHandle.RotateZ,
                    ZColor,
                    size,
                    vertices,
                    picking);
            }
        }

        return new NativeGizmoGeometry(
            vertices.ToArray(),
            picking.ToArray());
    }

    private static void AppendMoveHandle(
        Vector3 anchor,
        Vector3 axis,
        NativeGizmoHandle handle,
        Vector4 color,
        float size,
        List<NativeMapVertex> vertices,
        List<NativeMapVertex> picking)
    {
        var pickingColor =
            EncodePickingColor(
                NativeGizmoIds
                    .ToPickingId(
                        handle));

        var shaftStart =
            anchor +
            axis *
            (size * 0.10f);

        var shaftEnd =
            anchor +
            axis *
            (size * 0.78f);

        AppendPrism(
            shaftStart,
            shaftEnd,
            size * 0.035f,
            color,
            vertices);

        AppendPrism(
            shaftStart,
            shaftEnd,
            size * 0.085f,
            pickingColor,
            picking);

        var headStart =
            anchor +
            axis *
            (size * 0.72f);

        var headEnd =
            anchor +
            axis *
            size;

        AppendPrism(
            headStart,
            headEnd,
            size * 0.085f,
            color,
            vertices);

        AppendPrism(
            headStart,
            headEnd,
            size * 0.13f,
            pickingColor,
            picking);
    }

    private static void AppendRotationHandle(
        Vector3 anchor,
        Vector3 normal,
        Vector3 basisA,
        Vector3 basisB,
        NativeGizmoHandle handle,
        Vector4 color,
        float size,
        List<NativeMapVertex> vertices,
        List<NativeMapVertex> picking)
    {
        _ = normal;

        const int segments = 48;

        var radius =
            size *
            0.76f;

        var visibleHalfWidth =
            size *
            0.018f;

        var pickingHalfWidth =
            size *
            0.055f;

        var pickingColor =
            EncodePickingColor(
                NativeGizmoIds
                    .ToPickingId(
                        handle));

        for (
            var index = 0;
            index < segments;
            index++)
        {
            var angle0 =
                index *
                MathF.Tau /
                segments;

            var angle1 =
                (index + 1) *
                MathF.Tau /
                segments;

            AppendRingSegment(
                anchor,
                basisA,
                basisB,
                radius,
                visibleHalfWidth,
                angle0,
                angle1,
                color,
                vertices);

            AppendRingSegment(
                anchor,
                basisA,
                basisB,
                radius,
                pickingHalfWidth,
                angle0,
                angle1,
                pickingColor,
                picking);
        }
    }

    private static void AppendRingSegment(
        Vector3 anchor,
        Vector3 basisA,
        Vector3 basisB,
        float radius,
        float halfWidth,
        float angle0,
        float angle1,
        Vector4 color,
        List<NativeMapVertex> output)
    {
        Vector3 Point(
            float angle,
            float r) =>
            anchor +
            basisA *
            (MathF.Cos(angle) * r) +
            basisB *
            (MathF.Sin(angle) * r);

        var inner =
            radius -
            halfWidth;

        var outer =
            radius +
            halfWidth;

        AppendQuad(
            Point(angle0, inner),
            Point(angle1, inner),
            Point(angle1, outer),
            Point(angle0, outer),
            color,
            output);
    }

    private static void AppendPrism(
        Vector3 start,
        Vector3 end,
        float halfWidth,
        Vector4 color,
        List<NativeMapVertex> output)
    {
        var axis =
            end -
            start;

        if (
            axis.LengthSquared() <
            0.000001f)
        {
            return;
        }

        axis =
            Vector3.Normalize(
                axis);

        var helper =
            Math.Abs(
                Vector3.Dot(
                    axis,
                    Vector3.UnitY)) <
            0.92f
                ? Vector3.UnitY
                : Vector3.UnitX;

        var sideA =
            Vector3.Normalize(
                Vector3.Cross(
                    axis,
                    helper)) *
            halfWidth;

        var sideB =
            Vector3.Normalize(
                Vector3.Cross(
                    axis,
                    sideA)) *
            halfWidth;

        var s0 = start - sideA - sideB;
        var s1 = start + sideA - sideB;
        var s2 = start + sideA + sideB;
        var s3 = start - sideA + sideB;

        var e0 = end - sideA - sideB;
        var e1 = end + sideA - sideB;
        var e2 = end + sideA + sideB;
        var e3 = end - sideA + sideB;

        AppendQuad(s0, s1, e1, e0, color, output);
        AppendQuad(s1, s2, e2, e1, color, output);
        AppendQuad(s2, s3, e3, e2, color, output);
        AppendQuad(s3, s0, e0, e3, color, output);
        AppendQuad(s0, s3, s2, s1, color, output);
        AppendQuad(e0, e1, e2, e3, color, output);
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
                .Encode(
                    pickingId);

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
