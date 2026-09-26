using System.Numerics;
using MapStudio.Renderer.Scene;

namespace MapStudio.Renderer.Viewport;

public static class NativeSplineSplitMath
{
    public static bool TryCreateRequest(
        NativeSceneSnapshot scene,
        NativeSplineEntity entity,
        NativeSelectionInfo selection,
        Vector3 pointerWorld,
        out NativeSplineSplitRequest? request,
        out string status)
    {
        ArgumentNullException.ThrowIfNull(
            scene);

        ArgumentNullException.ThrowIfNull(
            entity);

        ArgumentNullException.ThrowIfNull(
            selection);

        request =
            null;

        status =
            string.Empty;

        var source =
            entity.Spline;

        if (
            source.Length <=
                2.0 ||
            selection.Kind !=
                Picking.PickingKind.Spline ||
            selection.EntityId !=
                source.SplineId)
        {
            status =
                "Dividir: spline inválida ou curta demais.";
            return false;
        }

        var splitDistance =
            FindClosestDistance(
                entity,
                pointerWorld);

        var minimumSegment =
            Math.Min(
                2.0,
                source.Length *
                    0.15);

        minimumSegment =
            Math.Max(
                0.75,
                minimumSegment);

        if (
            splitDistance <
                minimumSegment ||
            source.Length -
                splitDistance <
                minimumSegment)
        {
            status =
                $"Dividir: clique mais longe das extremidades (mín. {minimumSegment:F1} m).";
            return false;
        }

        var splitFrame =
            NativeSplinePathMath
                .GetFrame(
                    entity,
                    splitDistance);

        var endFrame =
            NativeSplinePathMath
                .GetFrame(
                    entity,
                    source.Length);

        var dx =
            splitFrame.Center.X -
            pointerWorld.X;

        var dz =
            splitFrame.Center.Z -
            pointerWorld.Z;

        var pointerDistance =
            Math.Sqrt(
                dx * dx +
                dz * dz);

        var tolerance =
            Math.Clamp(
                source.Length *
                    0.10,
                4.0,
                12.0);

        if (
            pointerDistance >
                tolerance)
        {
            status =
                $"Dividir: clique mais perto da spline (distância {pointerDistance:F1} m).";
            return false;
        }

        var ratio =
            splitDistance /
            source.Length;

        var middleGradient =
            source.GradientStart +
            (
                source.GradientEnd -
                source.GradientStart
            ) *
            ratio;

        var secondLength =
            source.Length -
            splitDistance;

        var secondRotation =
            NormalizeDegrees(
                Math.Atan2(
                    splitFrame.Forward.X,
                    splitFrame.Forward.Z) *
                180.0 /
                Math.PI);

        var tileX =
            (int)Math.Floor(
                splitFrame.Center.X /
                300.0f);

        var tileY =
            (int)Math.Floor(
                splitFrame.Center.Z /
                300.0f);

        var tile =
            scene.Tiles
                .FirstOrDefault(
                    item =>
                        item.Reference.X ==
                            tileX &&
                        item.Reference.Y ==
                            tileY);

        if (tile is null)
        {
            status =
                "Dividir: ponto de corte fora dos tiles carregados.";
            return false;
        }

        request =
            new NativeSplineSplitRequest(
                selection,
                splitDistance,
                splitDistance,
                source.GradientStart,
                middleGradient,
                tile.Reference,
                splitFrame.Center.X -
                    tileX *
                    300.0,
                splitFrame.Center.Y,
                splitFrame.Center.Z -
                    tileY *
                    300.0,
                secondRotation,
                secondLength,
                source.Radius,
                middleGradient,
                source.GradientEnd,
                splitFrame.Center,
                endFrame.Center);

        status =
            $"Dividir: corte em {splitDistance:F1} m · " +
            $"segmentos {splitDistance:F1} m + {secondLength:F1} m.";

        return true;
    }

    private static double FindClosestDistance(
        NativeSplineEntity entity,
        Vector3 point)
    {
        var length =
            entity.Spline.Length;

        var samples =
            Math.Clamp(
                (int)Math.Ceiling(
                    length /
                    2.0),
                32,
                256);

        var bestDistance =
            0.0;

        var bestError =
            double.PositiveInfinity;

        for (
            var index = 0;
            index <= samples;
            index++)
        {
            var distance =
                length *
                index /
                samples;

            var error =
                DistanceSquared(
                    NativeSplinePathMath
                        .GetFrame(
                            entity,
                            distance)
                        .Center,
                    point);

            if (
                error <
                bestError)
            {
                bestError =
                    error;

                bestDistance =
                    distance;
            }
        }

        var step =
            length /
            samples;

        var left =
            Math.Max(
                0,
                bestDistance -
                    step);

        var right =
            Math.Min(
                length,
                bestDistance +
                    step);

        for (
            var iteration = 0;
            iteration < 28;
            iteration++)
        {
            var first =
                left +
                (
                    right -
                    left
                ) /
                3.0;

            var second =
                right -
                (
                    right -
                    left
                ) /
                3.0;

            var firstError =
                DistanceSquared(
                    NativeSplinePathMath
                        .GetFrame(
                            entity,
                            first)
                        .Center,
                    point);

            var secondError =
                DistanceSquared(
                    NativeSplinePathMath
                        .GetFrame(
                            entity,
                            second)
                        .Center,
                    point);

            if (
                firstError <=
                secondError)
            {
                right =
                    second;
            }
            else
            {
                left =
                    first;
            }
        }

        return
            (
                left +
                right
            ) *
            0.5;
    }

    private static double DistanceSquared(
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
            dx * dx +
            dz * dz;
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
