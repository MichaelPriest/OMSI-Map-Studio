using System.Numerics;

namespace MapStudio.Renderer.Viewport;

public enum NativeSplineEndpointEditHandle
{
    None = 0,
    Start = 1,
    End = 2
}

public static class NativeSplineEndpointEditMath
{
    private const double Epsilon =
        0.0001;

    public static double EstimateCurveOffset(
        NativeSplinePlacementShape shape)
    {
        ArgumentNullException.ThrowIfNull(
            shape);

        if (
            !shape.IsCurved ||
            !double.IsFinite(
                shape.Radius) ||
            Math.Abs(
                shape.Radius) <
                Epsilon)
        {
            return 0.0;
        }

        var dx =
            (double)shape.End.X -
            shape.Start.X;

        var dz =
            (double)shape.End.Z -
            shape.Start.Z;

        var chord =
            Math.Sqrt(
                dx * dx +
                dz * dz);

        var radius =
            Math.Abs(
                shape.Radius);

        if (
            !double.IsFinite(
                chord) ||
            chord <
                Epsilon ||
            radius <=
                chord *
                0.5)
        {
            return 0.0;
        }

        var halfChord =
            chord *
            0.5;

        var sagitta =
            radius -
            Math.Sqrt(
                Math.Max(
                    0.0,
                    radius *
                        radius -
                    halfChord *
                        halfChord));

        if (
            !double.IsFinite(
                sagitta))
        {
            return 0.0;
        }

        return
            Math.Sign(
                shape.Radius) *
            sagitta;
    }

    public static bool TryCreateShape(
        Vector3 start,
        Vector3 end,
        bool preserveCurve,
        double curveOffset,
        out NativeSplinePlacementShape?
            shape)
    {
        if (
            preserveCurve &&
            double.IsFinite(
                curveOffset) &&
            Math.Abs(
                curveOffset) >=
                0.05)
        {
            return
                NativeSplinePlacementMath
                    .TryCreateArcFromOffset(
                        start,
                        end,
                        curveOffset,
                        out shape);
        }

        return
            NativeSplinePlacementMath
                .TryCreateStraight(
                    start,
                    end,
                    out shape);
    }

    public static double HorizontalDistance(
        Vector3 a,
        Vector3 b)
    {
        var dx =
            (double)a.X -
            b.X;

        var dz =
            (double)a.Z -
            b.Z;

        return
            Math.Sqrt(
                dx * dx +
                dz * dz);
    }
}
