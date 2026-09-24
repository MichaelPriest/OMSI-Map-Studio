using System.Numerics;

namespace MapStudio.Renderer.Viewport;

public static class NativeSplineCurveHandleMath
{
    private const double Epsilon =
        0.0001;

    public static Vector3 CreateHandlePoint(
        Vector3 start,
        Vector3 end,
        double curveOffset)
    {
        var dx =
            (double)end.X -
            start.X;

        var dz =
            (double)end.Z -
            start.Z;

        var length =
            Math.Sqrt(
                dx * dx +
                dz * dz);

        var midpoint =
            new Vector3(
                (
                    start.X +
                    end.X
                ) *
                0.5f,
                (
                    start.Y +
                    end.Y
                ) *
                0.5f,
                (
                    start.Z +
                    end.Z
                ) *
                0.5f);

        if (
            !double.IsFinite(
                length) ||
            length <
                Epsilon ||
            !double.IsFinite(
                curveOffset))
        {
            return midpoint;
        }

        var normalX =
            dz /
            length;

        var normalZ =
            -dx /
            length;

        return new Vector3(
            midpoint.X +
                (float)(
                    normalX *
                    curveOffset),
            midpoint.Y,
            midpoint.Z +
                (float)(
                    normalZ *
                    curveOffset));
    }

    public static double SnapOffset(
        double curveOffset,
        bool enabled,
        double step)
    {
        if (
            !enabled ||
            !double.IsFinite(
                curveOffset) ||
            !double.IsFinite(
                step) ||
            step <=
                Epsilon)
        {
            return curveOffset;
        }

        return
            Math.Round(
                curveOffset /
                step) *
            step;
    }
}
