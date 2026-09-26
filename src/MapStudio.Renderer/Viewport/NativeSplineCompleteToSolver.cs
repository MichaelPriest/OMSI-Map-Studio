using System.Numerics;
using MapStudio.Renderer.Scene;

namespace MapStudio.Renderer.Viewport;

public static class NativeSplineCompleteToSolver
{
    private const double Epsilon = 0.0001;

    public static bool TryCreateRequest(
        NativeSceneSnapshot scene,
        int sourceSplineId,
        int targetSplineId,
        double maximumRadius,
        out NativeSplinePlacementRequest? request,
        out string status)
    {
        ArgumentNullException.ThrowIfNull(scene);

        request = null;
        status = string.Empty;

        if (
            sourceSplineId == targetSplineId ||
            !double.IsFinite(maximumRadius) ||
            maximumRadius <= 0)
        {
            status = "Complete to: origem/destino ou raio máximo inválido.";
            return false;
        }

        var sourceMatches =
            scene.Splines
                .Where(item =>
                    item.Spline.SplineId ==
                        sourceSplineId)
                .ToArray();

        var targetMatches =
            scene.Splines
                .Where(item =>
                    item.Spline.SplineId ==
                        targetSplineId)
                .ToArray();

        if (
            sourceMatches.Length != 1 ||
            targetMatches.Length != 1)
        {
            status =
                "Complete to: as duas splines precisam estar carregadas e possuir IDs únicos.";
            return false;
        }

        var source =
            sourceMatches[0];

        var target =
            targetMatches[0];

        if (
            source.Spline.NextSplineId >= 0)
        {
            status =
                $"Complete to: o fim da spline #{sourceSplineId} já está conectado.";
            return false;
        }

        if (
            target.Spline.PreviousSplineId >= 0)
        {
            status =
                $"Complete to: o início da spline #{targetSplineId} já está conectado.";
            return false;
        }

        if (
            source.Spline.IsHeightSpline ||
            target.Spline.IsHeightSpline)
        {
            status =
                "Complete to: spline_h ainda não é aceita por esta ferramenta.";
            return false;
        }

        if (
            !string.Equals(
                source.Spline.SplinePath,
                target.Spline.SplinePath,
                StringComparison.OrdinalIgnoreCase))
        {
            status =
                "Complete to: nesta primeira versão segura, origem e destino precisam usar a mesma SLI para manter os paths compatíveis.";
            return false;
        }

        var start =
            NativeSplinePathMath.GetFrame(
                source,
                source.Spline.Length);

        var end =
            NativeSplinePathMath.GetFrame(
                target,
                0);

        if (
            !TrySolveShape(
                start,
                end,
                maximumRadius,
                out var shape,
                out status) ||
            shape is null)
        {
            return false;
        }

        var tileX =
            (int)Math.Floor(
                shape.Start.X /
                300.0f);

        var tileY =
            (int)Math.Floor(
                shape.Start.Z /
                300.0f);

        var tile =
            scene.Tiles
                .FirstOrDefault(item =>
                    item.Reference.X == tileX &&
                    item.Reference.Y == tileY);

        if (tile is null)
        {
            status =
                "Complete to: o ponto inicial da conexão está fora dos tiles carregados.";
            return false;
        }

        request =
            new NativeSplinePlacementRequest(
                tile.Reference,
                source.Spline.SplinePath,
                sourceSplineId,
                shape.Start.X -
                    tileX *
                    300.0,
                shape.Start.Z -
                    tileY *
                    300.0,
                shape.Start.Y,
                shape.Rotation,
                shape.Length,
                shape.Radius,
                shape.GradientStart,
                shape.GradientEnd,
                shape.IsCurved,
                shape.Start,
                shape.End,
                targetSplineId,
                IsHeightSpline: false);

        status =
            shape.IsCurved
                ? $"Complete to: conexão curva pronta · raio {shape.Radius:F2} m · comprimento {shape.Length:F2} m."
                : $"Complete to: conexão reta pronta · comprimento {shape.Length:F2} m.";

        return true;
    }

    public static bool TrySolveShape(
        NativeSplineFrame start,
        NativeSplineFrame end,
        double maximumRadius,
        out NativeSplinePlacementShape? shape,
        out string status)
    {
        shape = null;
        status = string.Empty;

        if (
            !double.IsFinite(maximumRadius) ||
            maximumRadius <= 0)
        {
            status =
                "Complete to: raio máximo inválido.";
            return false;
        }

        var dx =
            (double)end.Center.X -
            start.Center.X;

        var dz =
            (double)end.Center.Z -
            start.Center.Z;

        var horizontalDistance =
            Math.Sqrt(
                dx * dx +
                dz * dz);

        if (
            !double.IsFinite(horizontalDistance) ||
            horizontalDistance <
                Epsilon)
        {
            status =
                "Complete to: as extremidades são coincidentes.";
            return false;
        }

        var startHeading =
            Math.Atan2(
                start.Forward.X,
                start.Forward.Z);

        var endHeading =
            Math.Atan2(
                end.Forward.X,
                end.Forward.Z);

        var rawDelta =
            NormalizeSignedRadians(
                endHeading -
                startHeading);

        var tolerance =
            Math.Max(
                0.03,
                horizontalDistance *
                    0.0015);

        if (
            Math.Abs(rawDelta) <
                0.0005)
        {
            var lateralError =
                Math.Abs(
                    dx *
                        start.Lateral.X +
                    dz *
                        start.Lateral.Z);

            var forwardDistance =
                dx *
                    start.Forward.X +
                dz *
                    start.Forward.Z;

            if (
                lateralError >
                    tolerance ||
                forwardDistance <=
                    Epsilon ||
                Vector3.Dot(
                    start.Forward,
                    end.Forward) <
                    0.9999f)
            {
                status =
                    "Complete to: extremidades paralelas, mas não estão alinhadas para uma conexão reta.";
                return false;
            }

            var straightGradient =
                (
                    end.Center.Y -
                    start.Center.Y
                ) /
                horizontalDistance *
                100.0;

            shape =
                new NativeSplinePlacementShape(
                    start.Center,
                    end.Center,
                    NormalizeDegrees(
                        RadiansToDegrees(
                            startHeading)),
                    horizontalDistance,
                    0,
                    straightGradient,
                    straightGradient,
                    false);

            return true;
        }

        var vx =
            (double)start.Lateral.X -
            end.Lateral.X;

        var vz =
            (double)start.Lateral.Z -
            end.Lateral.Z;

        var denominator =
            vx * vx +
            vz * vz;

        if (denominator < Epsilon)
        {
            status =
                "Complete to: não foi possível determinar o centro da curva.";
            return false;
        }

        var radius =
            (
                dx * vx +
                dz * vz
            ) /
            denominator;

        if (
            !double.IsFinite(radius) ||
            Math.Abs(radius) <
                0.05)
        {
            status =
                "Complete to: raio calculado inválido.";
            return false;
        }

        var residualX =
            dx -
            radius * vx;

        var residualZ =
            dz -
            radius * vz;

        var residual =
            Math.Sqrt(
                residualX * residualX +
                residualZ * residualZ);

        if (residual > tolerance)
        {
            status =
                $"Complete to: uma única curva circular não fecha as duas tangentes (erro {residual:F3} m).";
            return false;
        }

        if (
            Math.Abs(radius) >
                maximumRadius +
                0.001)
        {
            status =
                $"Complete to: raio necessário {Math.Abs(radius):F2} m excede o máximo {maximumRadius:F2} m.";
            return false;
        }

        var sweep =
            rawDelta;

        if (
            radius > 0 &&
            sweep <= 0)
        {
            sweep +=
                Math.PI *
                2.0;
        }
        else if (
            radius < 0 &&
            sweep >= 0)
        {
            sweep -=
                Math.PI *
                2.0;
        }

        if (
            Math.Abs(sweep) <
                Epsilon ||
            Math.Abs(sweep) >
                Math.PI *
                1.95)
        {
            status =
                "Complete to: a solução exigiria uma volta excessiva; conexão recusada.";
            return false;
        }

        var length =
            radius *
            sweep;

        if (
            !double.IsFinite(length) ||
            length <=
                Epsilon)
        {
            status =
                "Complete to: comprimento calculado inválido.";
            return false;
        }

        var localX =
            radius *
            (
                1.0 -
                Math.Cos(
                    sweep)
            );

        var localZ =
            radius *
            Math.Sin(
                sweep);

        var cosYaw =
            Math.Cos(
                startHeading);

        var sinYaw =
            Math.Sin(
                startHeading);

        var predictedX =
            start.Center.X +
            localX *
                cosYaw +
            localZ *
                sinYaw;

        var predictedZ =
            start.Center.Z -
            localX *
                sinYaw +
            localZ *
                cosYaw;

        var endpointError =
            Math.Sqrt(
                (
                    predictedX -
                    end.Center.X
                ) *
                (
                    predictedX -
                    end.Center.X
                ) +
                (
                    predictedZ -
                    end.Center.Z
                ) *
                (
                    predictedZ -
                    end.Center.Z
                ));

        if (endpointError > tolerance)
        {
            status =
                $"Complete to: erro de fechamento {endpointError:F3} m excede a tolerância.";
            return false;
        }

        var curveGradient =
            (
                end.Center.Y -
                start.Center.Y
            ) /
            length *
            100.0;

        shape =
            new NativeSplinePlacementShape(
                start.Center,
                end.Center,
                NormalizeDegrees(
                    RadiansToDegrees(
                        startHeading)),
                length,
                radius,
                curveGradient,
                curveGradient,
                true);

        return true;
    }

    private static double NormalizeSignedRadians(
        double value)
    {
        value %=
            Math.PI *
            2.0;

        if (value <= -Math.PI)
        {
            value +=
                Math.PI *
                2.0;
        }
        else if (value > Math.PI)
        {
            value -=
                Math.PI *
                2.0;
        }

        return value;
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
