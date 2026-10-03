namespace MapStudio.Core.Generation.Roads;

public sealed record MapStudioRoadArcPlacement(
    double RotationDegrees,
    double LengthMeters,
    double RadiusMeters,
    double SweepDegrees);

public static class MapStudioRoadArcGeometry
{
    private const double Epsilon =
        0.0001;

    public static bool TryCreate(
        MapStudioRoadPoint start,
        MapStudioRoadPoint control,
        MapStudioRoadPoint end,
        out MapStudioRoadArcPlacement? placement)
    {
        placement =
            null;

        if (
            !IsFinite(start) ||
            !IsFinite(control) ||
            !IsFinite(end))
        {
            return false;
        }

        var ax =
            start.X;

        var az =
            start.Z;

        var bx =
            control.X;

        var bz =
            control.Z;

        var cx =
            end.X;

        var cz =
            end.Z;

        var determinant =
            2.0 *
            (
                ax *
                    (
                        bz -
                        cz
                    ) +
                bx *
                    (
                        cz -
                        az
                    ) +
                cx *
                    (
                        az -
                        bz
                    )
            );

        if (
            Math.Abs(
                determinant) <
            Epsilon)
        {
            return false;
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
                aa *
                    (
                        bz -
                        cz
                    ) +
                bb *
                    (
                        cz -
                        az
                    ) +
                cc *
                    (
                        az -
                        bz
                    )
            ) /
            determinant;

        var centerZ =
            (
                aa *
                    (
                        cx -
                        bx
                    ) +
                bb *
                    (
                        ax -
                        cx
                    ) +
                cc *
                    (
                        bx -
                        ax
                    )
            ) /
            determinant;

        var radiusMagnitude =
            Math.Sqrt(
                Math.Pow(
                    ax -
                    centerX,
                    2) +
                Math.Pow(
                    az -
                    centerZ,
                    2));

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

        var counterClockwiseEnd =
            PositiveAngle(
                endAngle -
                    startAngle);

        var counterClockwiseControl =
            PositiveAngle(
                controlAngle -
                    startAngle);

        var counterClockwise =
            counterClockwiseControl <=
                counterClockwiseEnd +
                1e-8;

        var signedSweep =
            counterClockwise
                ? counterClockwiseEnd
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
            counterClockwise
                ? -radiusMagnitude
                : radiusMagnitude;

        var tangentX =
            counterClockwise
                ? -Math.Sin(
                    startAngle)
                : Math.Sin(
                    startAngle);

        var tangentZ =
            counterClockwise
                ? Math.Cos(
                    startAngle)
                : -Math.Cos(
                    startAngle);

        var rotation =
            NormalizeDegrees(
                Math.Atan2(
                    tangentX,
                    tangentZ) *
                180.0 /
                Math.PI);

        var arcLength =
            radiusMagnitude *
            Math.Abs(
                signedSweep);

        var sweepDegrees =
            Math.Abs(
                signedSweep) *
            180.0 /
            Math.PI;

        if (
            !double.IsFinite(
                arcLength) ||
            arcLength <
                Epsilon ||
            !double.IsFinite(
                sweepDegrees))
        {
            return false;
        }

        placement =
            new MapStudioRoadArcPlacement(
                rotation,
                arcLength,
                signedRadius,
                sweepDegrees);

        return true;
    }

    public static MapStudioRoadPoint ResolveEndPoint(
        MapStudioRoadPoint start,
        double rotationDegrees,
        double lengthMeters,
        double radiusMeters)
    {
        var yaw =
            rotationDegrees *
            Math.PI /
            180.0;

        if (
            Math.Abs(
                radiusMeters) <=
            Epsilon)
        {
            return new MapStudioRoadPoint(
                start.X +
                    Math.Sin(
                        yaw) *
                    lengthMeters,
                start.Z +
                    Math.Cos(
                        yaw) *
                    lengthMeters);
        }

        var curveAngle =
            lengthMeters /
            radiusMeters;

        var localX =
            radiusMeters *
            (
                1.0 -
                Math.Cos(
                    curveAngle)
            );

        var localZ =
            radiusMeters *
            Math.Sin(
                curveAngle);

        return new MapStudioRoadPoint(
            start.X +
                localX *
                    Math.Cos(
                        yaw) +
                localZ *
                    Math.Sin(
                        yaw),
            start.Z -
                localX *
                    Math.Sin(
                        yaw) +
                localZ *
                    Math.Cos(
                        yaw));
    }

    private static bool IsFinite(
        MapStudioRoadPoint point) =>
        double.IsFinite(
            point.X) &&
        double.IsFinite(
            point.Z);

    private static double PositiveAngle(
        double radians)
    {
        var value =
            radians %
            (
                Math.PI *
                2.0
            );

        return value <
            0
                ? value +
                    Math.PI *
                    2.0
                : value;
    }

    private static double NormalizeDegrees(
        double value)
    {
        value %=
            360.0;

        if (value < 0)
        {
            value +=
                360.0;
        }

        return value;
    }
}
