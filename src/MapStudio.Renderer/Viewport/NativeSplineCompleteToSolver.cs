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

        NativeSplinePlacementShape? second = null;
        if (!TrySolveShape(start, end, maximumRadius, out var shape, out status))
        {
            var originalStatus = status;
            if (!TrySolveBiarc(start, end, maximumRadius, out shape, out second))
            {
                status = originalStatus + " Não foi possível ajustar dois arcos tangentes.";
                return false;
            }
        }

        if (shape is null)
        {
            return false;
        }

        NativeSplinePlacementRequest? ToRequest(NativeSplinePlacementShape part)
        {
            var tileX = (int)Math.Floor(part.Start.X / 300.0);
            var tileY = (int)Math.Floor(part.Start.Z / 300.0);
            var tile = scene.Tiles.FirstOrDefault(t =>
                t.Reference.X == tileX && t.Reference.Y == tileY);
            if (tile is null)
            {
                return null;
            }

            return new NativeSplinePlacementRequest(tile.Reference,
                source.Spline.SplinePath, -1, part.Start.X - tileX * 300.0,
                part.Start.Z - tileY * 300.0, part.Start.Y,
                part.Rotation, part.Length, part.Radius,
                part.GradientStart, part.GradientEnd, part.IsCurved,
                part.Start, part.End);
        }

        var firstRequest = ToRequest(shape);
        var secondRequest = second is null ? null : ToRequest(second);
        if (firstRequest is null || (second is not null && secondRequest is null))
        {
            status = "Complete to: início de um segmento fora dos tiles carregados.";
            return false;
        }

        request = firstRequest with
        {
            PreviousSplineId = sourceSplineId,
            NextSplineId = targetSplineId,
            Continuation = secondRequest
        };
        status = second is not null
            ? $"Complete to: dois arcos tangentes · comprimento {shape.Length + second.Length:F2} m."
            : $"Complete to: conexão pronta · comprimento {shape.Length:F2} m · raio {shape.Radius:F2} m.";
        return true;
    }

    // Equal tangent-distance biarc construction. The meeting point is
    // obtained from the intersection of the two tangent-circle constraints.
    // Only forward, non-degenerate arcs within the automatic radius bounds
    // are accepted. Both pieces share the same longitudinal grade.
    public static bool TrySolveBiarc(
        NativeSplineFrame start, NativeSplineFrame end, double maximumRadius,
        out NativeSplinePlacementShape? first, out NativeSplinePlacementShape? second)
    {
        first = null;
        second = null;
        if (!double.IsFinite(maximumRadius) || maximumRadius <= 0)
        {
            return false;
        }
        var a0 = Math.Atan2(start.Forward.X, start.Forward.Z);
        var a1 = Math.Atan2(end.Forward.X, end.Forward.Z);
        var tx = Math.Sin(a0);
        var tz = Math.Cos(a0);
        var ux = Math.Sin(a1);
        var uz = Math.Cos(a1);
        var dx = (double)end.Center.X - start.Center.X;
        var dz = (double)end.Center.Z - start.Center.Z;
        var squared = dx * dx + dz * dz;
        var a = 2 * (1 - tx * ux - tz * uz);
        var b = 2 * (dx * (tx + ux) + dz * (tz + uz));
        if (!double.IsFinite(squared) || squared < Epsilon ||
            !double.IsFinite(a) || !double.IsFinite(b) ||
            start.Forward.LengthSquared() < Epsilon || end.Forward.LengthSquared() < Epsilon)
        {
            return false;
        }

        var d = a < Epsilon
            ? (b > Epsilon ? squared / b : double.NaN)
            : (Math.Sqrt(b * b + 4 * a * squared) - b) / (2 * a);
        if (!double.IsFinite(d) || d <= 0)
        {
            return false;
        }

        var joint = new Vector3(
            (float)(start.Center.X + (dx + d * (tx - ux)) / 2),
            start.Center.Y,
            (float)(start.Center.Z + (dz + d * (tz - uz)) / 2));
        if (!NativeSplinePlacementMath.TryCreateTangentArc(
                start.Center, joint, start.Forward, out var left) || left is null ||
            !NativeSplinePlacementMath.TryCreateTangentArc(
                end.Center, joint, -end.Forward, out var backwards) || backwards is null)
        {
            return false;
        }

        var leftHeading = left.Rotation + (left.IsCurved
            ? left.Length / left.Radius * 180 / Math.PI : 0);
        var rightHeading = NormalizeDegrees(backwards.Rotation + 180 +
            (backwards.IsCurved ? backwards.Length / backwards.Radius * 180 / Math.PI : 0));
        if (Math.Abs(NormalizeSignedRadians((rightHeading - leftHeading) * Math.PI / 180)) > 0.008 ||
            left.Length < 1 || backwards.Length < 1 ||
            Math.Abs(left.Radius) > maximumRadius || Math.Abs(backwards.Radius) > maximumRadius)
        {
            return false;
        }

        var grade = ((double)end.Center.Y - start.Center.Y) /
            (left.Length + backwards.Length) * 100;
        joint.Y = (float)(start.Center.Y + left.Length * grade / 100);
        if (!double.IsFinite(grade) || !float.IsFinite(joint.Y))
        {
            return false;
        }

        first = left with { End = joint, GradientStart = grade, GradientEnd = grade };
        second = new NativeSplinePlacementShape(joint, end.Center,
            rightHeading, backwards.Length, -backwards.Radius,
            grade, grade, backwards.IsCurved);
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
