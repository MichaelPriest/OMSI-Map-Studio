using System.Numerics;
using MapStudio.Core.Omsi.Splines;

namespace MapStudio.Renderer.Scene;

public readonly record struct NativeSplineFrame(
    Vector3 Center,
    Vector3 Lateral,
    Vector3 Forward);

public static class NativeSplinePathMath
{
    public static NativeSplineFrame GetFrame(
        NativeSplineEntity entity,
        double distance)
    {
        ArgumentNullException.ThrowIfNull(
            entity);

        var spline =
            entity.Spline;

        var length =
            Math.Max(
                0.0,
                spline.Length);

        var clampedDistance =
            Math.Clamp(
                distance,
                0.0,
                length);

        var yaw =
            DegreesToRadians(
                spline.Rotation);

        var hasCurve =
            Math.Abs(
                spline.Radius) >
            0.001;

        var curveAngle =
            hasCurve
                ? clampedDistance /
                  spline.Radius
                : 0.0;

        var localX =
            hasCurve
                ? spline.Radius *
                  (
                      1.0 -
                      Math.Cos(
                          curveAngle)
                  )
                : 0.0;

        var localZ =
            hasCurve
                ? spline.Radius *
                  Math.Sin(
                      curveAngle)
                : clampedDistance;

        var cosYaw =
            Math.Cos(yaw);

        var sinYaw =
            Math.Sin(yaw);

        var worldX =
            entity.WorldX +
            localX * cosYaw +
            localZ * sinYaw;

        var worldZ =
            entity.WorldZ -
            localX * sinYaw +
            localZ * cosYaw;

        var heading =
            yaw +
            curveAngle;

        var forward =
            Vector3.Normalize(
                new Vector3(
                    (float)
                        Math.Sin(
                            heading),
                    0,
                    (float)
                        Math.Cos(
                            heading)));

        var lateral =
            new Vector3(
                forward.Z,
                0,
                -forward.X);

        var worldY =
            entity.WorldY +
            GetGradientRise(
                spline.GradientStart,
                spline.GradientEnd,
                length,
                clampedDistance);

        return new NativeSplineFrame(
            new Vector3(
                (float)worldX,
                (float)worldY,
                (float)worldZ),
            lateral,
            forward);
    }

    public static Vector3 TransformProfilePoint(
        NativeSplineFrame frame,
        OmsiSplineProfilePoint point)
    {
        ArgumentNullException.ThrowIfNull(
            point);

        return
            frame.Center +
            frame.Lateral *
            (float)point.X +
            Vector3.UnitY *
            (float)point.Z;
    }

    public static double GetGradientRise(
        double gradientStart,
        double gradientEnd,
        double length,
        double distance)
    {
        if (length <= 0)
        {
            return 0;
        }

        var clampedDistance =
            Math.Clamp(
                distance,
                0.0,
                length);

        var startSlope =
            gradientStart /
            100.0;

        var slopeDelta =
            (
                gradientEnd -
                gradientStart
            ) /
            100.0;

        return
            startSlope *
            clampedDistance +
            0.5 *
            slopeDelta *
            clampedDistance *
            clampedDistance /
            length;
    }

    private static double DegreesToRadians(
        double value) =>
        value *
        Math.PI /
        180.0;
}
