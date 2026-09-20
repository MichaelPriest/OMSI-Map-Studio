using System.Numerics;

namespace MapStudio.Renderer.Viewport;

public sealed record NativeSplinePlacementShape(
    Vector3 Start,
    Vector3 End,
    double Rotation,
    double Length,
    double Radius,
    double GradientStart,
    double GradientEnd,
    bool IsCurved);

public static class NativeSplinePlacementMath
{
    private const double Epsilon =
        0.0001;

    public static bool TryCreateStraight(
        Vector3 start,
        Vector3 end,
        out NativeSplinePlacementShape?
            shape)
    {
        shape = null;

        var dx =
            end.X -
            start.X;

        var dz =
            end.Z -
            start.Z;

        var horizontalLength =
            Math.Sqrt(
                dx * dx +
                dz * dz);

        if (
            horizontalLength <
            Epsilon)
        {
            return false;
        }

        var rotation =
            RadiansToDegrees(
                Math.Atan2(
                    dx,
                    dz));

        var gradient =
            (
                end.Y -
                start.Y
            ) /
            horizontalLength *
            100.0;

        shape =
            new NativeSplinePlacementShape(
                start,
                end,
                NormalizeDegrees(
                    rotation),
                horizontalLength,
                0.0,
                gradient,
                gradient,
                false);

        return true;
    }

    public static bool TryCreateArc(
        Vector3 start,
        Vector3 end,
        Vector3 control,
        out NativeSplinePlacementShape?
            shape)
    {
        shape = null;

        var ax = (double)start.X;
        var az = (double)start.Z;
        var bx = (double)control.X;
        var bz = (double)control.Z;
        var cx = (double)end.X;
        var cz = (double)end.Z;

        var determinant =
            2.0 *
            (
                ax * (bz - cz) +
                bx * (cz - az) +
                cx * (az - bz)
            );

        if (
            Math.Abs(
                determinant) <
            Epsilon)
        {
            return TryCreateStraight(
                start,
                end,
                out shape);
        }

        var aa =
            ax * ax +
            az * az;

        var bb =
            bx * bx +
            bz * bz;

        var cc =
            cx * cx +
            cz * cz;

        var centerX =
            (
                aa * (bz - cz) +
                bb * (cz - az) +
                cc * (az - bz)
            ) /
            determinant;

        var centerZ =
            (
                aa * (cx - bx) +
                bb * (ax - cx) +
                cc * (bx - ax)
            ) /
            determinant;

        var radiusMagnitude =
            Math.Sqrt(
                (
                    ax -
                    centerX
                ) *
                (
                    ax -
                    centerX
                ) +
                (
                    az -
                    centerZ
                ) *
                (
                    az -
                    centerZ
                ));

        if (
            !double.IsFinite(
                radiusMagnitude) ||
            radiusMagnitude <
                Epsilon)
        {
            return false;
        }

        var startAngle =
            Math.Atan2(
                az -
                centerZ,
                ax -
                centerX);

        var controlAngle =
            Math.Atan2(
                bz -
                centerZ,
                bx -
                centerX);

        var endAngle =
            Math.Atan2(
                cz -
                centerZ,
                cx -
                centerX);

        var ccwEnd =
            PositiveAngle(
                endAngle -
                startAngle);

        var ccwControl =
            PositiveAngle(
                controlAngle -
                startAngle);

        var useCounterClockwise =
            ccwControl <=
            ccwEnd +
            1e-8;

        var signedSweep =
            useCounterClockwise
                ? ccwEnd
                : -PositiveAngle(
                    startAngle -
                    endAngle);

        if (
            Math.Abs(
                signedSweep) <
                Epsilon ||
            Math.Abs(
                signedSweep) >
                Math.PI *
                1.95)
        {
            return false;
        }

        var signedRadius =
            useCounterClockwise
                ? -radiusMagnitude
                : radiusMagnitude;

        var tangentX =
            useCounterClockwise
                ? -Math.Sin(
                    startAngle)
                : Math.Sin(
                    startAngle);

        var tangentZ =
            useCounterClockwise
                ? Math.Cos(
                    startAngle)
                : -Math.Cos(
                    startAngle);

        var rotation =
            RadiansToDegrees(
                Math.Atan2(
                    tangentX,
                    tangentZ));

        var arcLength =
            radiusMagnitude *
            Math.Abs(
                signedSweep);

        var gradient =
            (
                end.Y -
                start.Y
            ) /
            arcLength *
            100.0;

        shape =
            new NativeSplinePlacementShape(
                start,
                end,
                NormalizeDegrees(
                    rotation),
                arcLength,
                signedRadius,
                gradient,
                gradient,
                true);

        return true;
    }

    private static double PositiveAngle(
        double radians)
    {
        var value =
            radians %
            (
                Math.PI *
                2.0
            );

        return
            value < 0
                ? value +
                  Math.PI *
                  2.0
                : value;
    }

    private static double RadiansToDegrees(
        double value) =>
        value *
        180.0 /
        Math.PI;

    private static double NormalizeDegrees(
        double value)
    {
        value %= 360.0;

        if (value < 0)
        {
            value += 360.0;
        }

        return value;
    }
}
