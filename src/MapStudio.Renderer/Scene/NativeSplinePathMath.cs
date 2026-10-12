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

        if (
            !double.IsFinite(distance) ||
            !double.IsFinite(entity.WorldX) ||
            !double.IsFinite(entity.WorldY) ||
            !double.IsFinite(entity.WorldZ) ||
            !double.IsFinite(spline.Length) ||
            !double.IsFinite(spline.Rotation) ||
            !double.IsFinite(spline.Radius) ||
            !double.IsFinite(spline.GradientStart) ||
            !double.IsFinite(spline.GradientEnd))
        {
            return CreateSafeFallbackFrame(entity);
        }

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

        var center = new Vector3(
            (float)worldX,
            (float)worldY,
            (float)worldZ);

        if (
            !IsFinite(center) ||
            !IsFinite(forward) ||
            !IsFinite(lateral))
        {
            return CreateSafeFallbackFrame(entity);
        }

        return new NativeSplineFrame(
            center,
            lateral,
            forward);
    }

    public static Vector3 TransformProfilePoint(
        NativeSplineFrame frame,
        OmsiSplineProfilePoint point)
    {
        ArgumentNullException.ThrowIfNull(
            point);

        if (
            !IsFinite(frame.Center) ||
            !IsFinite(frame.Lateral) ||
            !double.IsFinite(point.X) ||
            !double.IsFinite(point.Z) ||
            Math.Abs(point.X) > float.MaxValue ||
            Math.Abs(point.Z) > float.MaxValue)
        {
            return IsFinite(frame.Center)
                ? frame.Center
                : Vector3.Zero;
        }

        var transformed =
            frame.Center +
            frame.Lateral *
            (float)point.X +
            Vector3.UnitY *
            (float)point.Z;

        return IsFinite(transformed)
            ? transformed
            : frame.Center;
    }

    public static double GetGradientRise(
        double gradientStart,
        double gradientEnd,
        double length,
        double distance)
    {
        if (
            !double.IsFinite(gradientStart) ||
            !double.IsFinite(gradientEnd) ||
            !double.IsFinite(length) ||
            !double.IsFinite(distance) ||
            length <= 0)
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

        var rise =
            startSlope *
            clampedDistance +
            0.5 *
            slopeDelta *
            clampedDistance *
            clampedDistance /
            length;

        return double.IsFinite(rise)
            ? rise
            : 0;
    }

    private static NativeSplineFrame CreateSafeFallbackFrame(
        NativeSplineEntity entity)
    {
        var center = new Vector3(
            float.IsFinite(entity.WorldX) ? entity.WorldX : 0,
            float.IsFinite(entity.WorldY) ? entity.WorldY : 0,
            float.IsFinite(entity.WorldZ) ? entity.WorldZ : 0);

        return new NativeSplineFrame(
            center,
            Vector3.UnitX,
            Vector3.UnitZ);
    }

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) &&
        float.IsFinite(value.Y) &&
        float.IsFinite(value.Z);

    private static double DegreesToRadians(
        double value) =>
        value *
        Math.PI /
        180.0;
}
