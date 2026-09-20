using System.Numerics;
using MapStudio.Renderer.Viewport;

namespace MapStudio.Renderer.Scene;

public sealed class
    NativeSplinePlacementGeometryBuilder
{
    private static readonly Vector4
        PreviewColor =
            new(
                0.08f,
                0.72f,
                1.0f,
                1.0f);

    public NativeAssetPreviewGeometry Build(
        NativeSplineAsset asset,
        NativeSplinePlacementShape shape)
    {
        ArgumentNullException.ThrowIfNull(
            asset);

        ArgumentNullException.ThrowIfNull(
            shape);

        if (
            !asset.IsLoaded ||
            shape.Length <
                0.01)
        {
            return
                NativeAssetPreviewGeometry
                    .Error(
                        asset.ErrorCode ??
                        "splinePlacementNotRenderable");
        }

        var vertices =
            new List<NativeMapVertex>(
                4096);

        var segments =
            Math.Clamp(
                (int)Math.Ceiling(
                    shape.Length /
                    2.5),
                2,
                256);

        foreach (
            var surface in
                asset.Definition
                    .Surfaces)
        {
            for (
                var segment = 0;
                segment < segments;
                segment++)
            {
                var distance0 =
                    shape.Length *
                    segment /
                    segments;

                var distance1 =
                    shape.Length *
                    (segment + 1) /
                    segments;

                var frame0 =
                    GetFrame(
                        shape,
                        distance0);

                var frame1 =
                    GetFrame(
                        shape,
                        distance1);

                var left0 =
                    TransformProfilePoint(
                        frame0,
                        surface.From.X,
                        surface.From.Z);

                var right0 =
                    TransformProfilePoint(
                        frame0,
                        surface.To.X,
                        surface.To.Z);

                var left1 =
                    TransformProfilePoint(
                        frame1,
                        surface.From.X,
                        surface.From.Z);

                var right1 =
                    TransformProfilePoint(
                        frame1,
                        surface.To.X,
                        surface.To.Z);

                AppendQuad(
                    left0,
                    left1,
                    right1,
                    right0,
                    vertices);
            }
        }

        if (vertices.Count == 0)
        {
            return
                NativeAssetPreviewGeometry
                    .Error(
                        "splinePlacementNoTriangles");
        }

        var minimum =
            new Vector3(
                float.PositiveInfinity,
                float.PositiveInfinity,
                float.PositiveInfinity);

        var maximum =
            new Vector3(
                float.NegativeInfinity,
                float.NegativeInfinity,
                float.NegativeInfinity);

        foreach (
            var vertex in
                vertices)
        {
            minimum =
                Vector3.Min(
                    minimum,
                    vertex.Position);

            maximum =
                Vector3.Max(
                    maximum,
                    vertex.Position);
        }

        return new NativeAssetPreviewGeometry(
            vertices.ToArray(),
            minimum,
            maximum,
            asset.Definition
                .Surfaces.Count,
            null);
    }

    private static (
        Vector3 Center,
        Vector3 Lateral,
        Vector3 Forward)
        GetFrame(
            NativeSplinePlacementShape
                shape,
            double distance)
    {
        var clamped =
            Math.Clamp(
                distance,
                0.0,
                shape.Length);

        var yaw =
            shape.Rotation *
            Math.PI /
            180.0;

        var hasCurve =
            Math.Abs(
                shape.Radius) >
            0.001;

        var curveAngle =
            hasCurve
                ? clamped /
                  shape.Radius
                : 0.0;

        var localX =
            hasCurve
                ? shape.Radius *
                  (
                      1.0 -
                      Math.Cos(
                          curveAngle)
                  )
                : 0.0;

        var localZ =
            hasCurve
                ? shape.Radius *
                  Math.Sin(
                      curveAngle)
                : clamped;

        var cosYaw =
            Math.Cos(
                yaw);

        var sinYaw =
            Math.Sin(
                yaw);

        var worldX =
            shape.Start.X +
            localX * cosYaw +
            localZ * sinYaw;

        var worldZ =
            shape.Start.Z -
            localX * sinYaw +
            localZ * cosYaw;

        var heading =
            yaw +
            curveAngle;

        var forward =
            Vector3.Normalize(
                new Vector3(
                    (float)Math.Sin(
                        heading),
                    0,
                    (float)Math.Cos(
                        heading)));

        var lateral =
            new Vector3(
                forward.Z,
                0,
                -forward.X);

        var rise =
            (
                shape.GradientStart /
                100.0
            ) *
            clamped;

        var center =
            new Vector3(
                (float)worldX,
                shape.Start.Y +
                    (float)rise,
                (float)worldZ);

        return
            (
                center,
                lateral,
                forward
            );
    }

    private static Vector3
        TransformProfilePoint(
            (
                Vector3 Center,
                Vector3 Lateral,
                Vector3 Forward
            ) frame,
            double x,
            double z) =>
        frame.Center +
        frame.Lateral *
        (float)x +
        Vector3.UnitY *
        (float)z;

    private static void AppendQuad(
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Vector3 d,
        List<NativeMapVertex> output)
    {
        output.Add(
            new NativeMapVertex(
                a,
                PreviewColor));

        output.Add(
            new NativeMapVertex(
                b,
                PreviewColor));

        output.Add(
            new NativeMapVertex(
                c,
                PreviewColor));

        output.Add(
            new NativeMapVertex(
                a,
                PreviewColor));

        output.Add(
            new NativeMapVertex(
                c,
                PreviewColor));

        output.Add(
            new NativeMapVertex(
                d,
                PreviewColor));
    }
}
