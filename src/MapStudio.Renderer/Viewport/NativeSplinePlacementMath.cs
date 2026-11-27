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
    bool IsCurved)
{
    public NativeSplinePlacementShape? Continuation { get; init; }
    public double TotalLength => Length + (Continuation?.Length ?? 0);
}

public static class NativeSplinePlacementMath
{
    private const double Epsilon =
        0.0001;

    public static float ResolveRoadEndpointHeight(
        float terrainHeight,
        float startHeight,
        NativeRoadElevationMode mode,
        double elevationValue)
    {
        var safeTerrain =
            float.IsFinite(terrainHeight)
                ? terrainHeight
                : 0.0f;

        var safeStart =
            float.IsFinite(startHeight)
                ? startHeight
                : safeTerrain;

        var safeValue =
            double.IsFinite(elevationValue)
                ? Math.Clamp(elevationValue, -100.0, 300.0)
                : 0.0;

        var result =
            mode switch
            {
                NativeRoadElevationMode.Elevate =>
                    (double)safeStart + Math.Abs(safeValue),

                NativeRoadElevationMode.Level =>
                    safeStart,

                NativeRoadElevationMode.Lower =>
                    (double)safeStart - Math.Abs(safeValue),

                NativeRoadElevationMode.FollowTerrain =>
                    (double)safeTerrain + safeValue,

                // An unknown enum value must not silently behave like
                // FollowTerrain; keep the endpoint at a safe terrain height.
                _ =>
                    safeTerrain
            };

        // Keep the placement API finite even for malformed input or
        // float-range overflow; valid normal inputs retain their values.
        return (float)Math.Clamp(
            result,
            -float.MaxValue,
            float.MaxValue);
    }

    /// <summary>Exact horizontal arc length and signed OMSI radius, with an even grade.</summary>
    public static bool TryCreateNumeric(Vector3 start, double length, double radius, double headingDegrees,
        double endHeight, out NativeSplinePlacementShape? shape)
    {
        shape = null;
        if (!IsFinite(start) || !double.IsFinite(length) || !double.IsFinite(radius) ||
            !double.IsFinite(headingDegrees) || !double.IsFinite(endHeight) || length < 0.5 || length > 3000 ||
            Math.Abs(radius) > 100000 || (radius != 0 && (Math.Abs(radius) < 6 || Math.Abs(length / radius) > Math.PI))) return false;
        var heading = headingDegrees * Math.PI / 180;
        var dx = radius == 0 ? length * Math.Sin(heading) : radius * (Math.Cos(heading) - Math.Cos(heading + length / radius));
        var dz = radius == 0 ? length * Math.Cos(heading) : radius * (Math.Sin(heading + length / radius) - Math.Sin(heading));
        var end = new Vector3((float)(start.X + dx), (float)endHeight, (float)(start.Z + dz));
        var grade = (endHeight - start.Y) / length * 100;
        shape = new NativeSplinePlacementShape(start, end, NormalizeDegrees(headingDegrees), length, radius, grade, grade, radius != 0);
        if (!IsFiniteShape(shape)) { shape = null; return false; }
        return true;
    }

    public static bool TryCreateStraight(
        Vector3 start,
        Vector3 end,
        out NativeSplinePlacementShape?
            shape)
    {
        shape = null;

        if (!IsFinite(start) || !IsFinite(end))
        {
            return false;
        }

        var dx =
            (double)end.X -
            start.X;

        var dz =
            (double)end.Z -
            start.Z;

        var horizontalLength =
            Math.Sqrt(
                dx * dx +
                dz * dz);

        if (
            !double.IsFinite(horizontalLength) ||
            horizontalLength < Epsilon)
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
                (double)end.Y -
                start.Y
            ) /
            horizontalLength *
            100.0;

        if (!double.IsFinite(gradient))
        {
            return false;
        }

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

        if (!IsFiniteShape(shape))
        {
            shape = null;
            return false;
        }

        return true;
    }

    // Solve a circular arc from a prescribed departure tangent and an endpoint.
    // The chord decomposes into forward and lateral components; the circle
    // centre lies on the start normal. The height follows an even grade.
    public static bool TryCreateTangentArc(
        Vector3 start, Vector3 end, Vector3 forward,
        out NativeSplinePlacementShape? shape)
    {
        shape = null;
        if (!IsFinite(start) || !IsFinite(end) || !IsFinite(forward))
        {
            return false;
        }

        var heading = Math.Atan2(forward.X, forward.Z);
        var directionLength = Math.Sqrt((double)forward.X * forward.X +
            (double)forward.Z * forward.Z);
        if (directionLength < Epsilon)
        {
            return false;
        }

        var dx = (double)end.X - start.X;
        var dz = (double)end.Z - start.Z;
        var ahead = dx * Math.Sin(heading) + dz * Math.Cos(heading);
        var lateral = dx * Math.Cos(heading) - dz * Math.Sin(heading);
        var chord = Math.Sqrt(dx * dx + dz * dz);
        if (chord < Epsilon || !double.IsFinite(chord))
        {
            return false;
        }

        if (Math.Abs(lateral) <= Epsilon)
        {
            return ahead > Epsilon && TryCreateStraight(start, end, out shape);
        }

        var radius = chord * chord / (2 * lateral);
        var sweep = 2 * Math.Atan2(lateral, ahead);
        // A short departure that turns back into itself is not a useful road.
        // These conservative bounds apply only to automatic continuation.
        if (!double.IsFinite(radius) || Math.Abs(radius) < 6 ||
            Math.Abs(sweep) > Math.PI)
        {
            return false;
        }

        var length = radius * sweep;
        if (!double.IsFinite(length) || length < Epsilon)
        {
            return false;
        }

        var grade = ((double)end.Y - start.Y) / length * 100;
        shape = new NativeSplinePlacementShape(start, end,
            NormalizeDegrees(RadiansToDegrees(heading)), length, radius,
            grade, grade, true);
        if (!IsFiniteShape(shape))
        {
            shape = null;
            return false;
        }

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

        if (!IsFinite(start) || !IsFinite(end) || !IsFinite(control))
        {
            return false;
        }

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
                (double)end.Y -
                start.Y
            ) /
            arcLength *
            100.0;

        if (!double.IsFinite(gradient))
        {
            return false;
        }

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

        if (!IsFiniteShape(shape))
        {
            shape = null;
            return false;
        }

        return true;
    }

    public static bool TryGetCurveOffsetFromControlPoint(
        Vector3 start,
        Vector3 end,
        Vector3 control,
        out double curveOffset)
    {
        curveOffset =
            0;

        if (!IsFinite(start) || !IsFinite(end) || !IsFinite(control))
        {
            return false;
        }

        var dx =
            (double)end.X -
            start.X;

        var dz =
            (double)end.Z -
            start.Z;

        var chordLength =
            Math.Sqrt(
                dx * dx +
                dz * dz);

        if (
            !double.IsFinite(
                chordLength) ||
            chordLength <
                Epsilon)
        {
            return false;
        }

        var midX =
            (
                (double)start.X +
                end.X
            ) *
            0.5;

        var midZ =
            (
                (double)start.Z +
                end.Z
            ) *
            0.5;

        // Unit normal in the X/Z plane. The cursor only controls
        // lateral sagitta; movement along the chord is intentionally
        // ignored so the handle behaves predictably.
        var normalX =
            dz /
            chordLength;

        var normalZ =
            -dx /
            chordLength;

        curveOffset =
            (
                control.X -
                midX
            ) *
            normalX +
            (
                control.Z -
                midZ
            ) *
            normalZ;

        return double.IsFinite(
            curveOffset);
    }

    public static bool TryCreateArcFromOffset(
        Vector3 start,
        Vector3 end,
        double curveOffset,
        out NativeSplinePlacementShape?
            shape)
    {
        shape = null;

        if (!IsFinite(start) || !IsFinite(end))
        {
            return false;
        }

        var dx =
            (double)end.X -
            start.X;

        var dz =
            (double)end.Z -
            start.Z;

        var chordLength =
            Math.Sqrt(
                dx * dx +
                dz * dz);

        if (
            !double.IsFinite(chordLength) ||
            chordLength < Epsilon)
        {
            return false;
        }

        if (
            !double.IsFinite(
                curveOffset) ||
            Math.Abs(
                curveOffset) <
            0.05)
        {
            return TryCreateStraight(
                start,
                end,
                out shape);
        }

        var maximumOffset =
            Math.Max(
                0.05,
                chordLength *
                0.49);

        var sagitta =
            Math.Clamp(
                curveOffset,
                -maximumOffset,
                maximumOffset);

        var radiusMagnitude =
            (
                chordLength *
                chordLength
            ) /
            (
                8.0 *
                Math.Abs(
                    sagitta)
            ) +
            Math.Abs(
                sagitta) /
            2.0;

        if (
            !double.IsFinite(
                radiusMagnitude) ||
            radiusMagnitude <
            Epsilon)
        {
            return false;
        }

        // Sagitta is the side of the arc midpoint, opposite the
        // side of its circle centre (OMSI signed curvature).
        var signedRadius =
            -Math.Sign(sagitta) * radiusMagnitude;

        var halfAngle =
            Math.Asin(
                Math.Min(
                    1.0,
                    chordLength /
                    (
                        2.0 *
                        radiusMagnitude
                    )));

        var signedAngle =
            Math.Sign(signedRadius) * halfAngle * 2.0;

        var chordBearing =
            Math.Atan2(
                dx,
                dz);

        var rotation =
            RadiansToDegrees(
                chordBearing -
                signedAngle /
                2.0);

        var arcLength =
            radiusMagnitude *
            Math.Abs(
                signedAngle);

        if (
            !double.IsFinite(
                arcLength) ||
            arcLength <
            Epsilon)
        {
            return false;
        }

        var gradient =
            (
                (double)end.Y -
                start.Y
            ) /
            arcLength *
            100.0;

        if (!double.IsFinite(gradient))
        {
            return false;
        }

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

        if (!IsFiniteShape(shape))
        {
            shape = null;
            return false;
        }

        return true;
    }

    private static bool IsFiniteShape(
        NativeSplinePlacementShape? shape) =>
        shape is not null &&
        IsFinite(shape.Start) &&
        IsFinite(shape.End) &&
        double.IsFinite(shape.Rotation) &&
        double.IsFinite(shape.Length) &&
        double.IsFinite(shape.Radius) &&
        double.IsFinite(shape.GradientStart) &&
        double.IsFinite(shape.GradientEnd);

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) &&
        float.IsFinite(value.Y) &&
        float.IsFinite(value.Z);

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
