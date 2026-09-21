using System.Numerics;
using MapStudio.Core.Omsi.Splines;

namespace MapStudio.Renderer.Scene;

public sealed record NativeTrafficPathGeometry(
    NativeMapVertex[] Vertices,
    int PathCount)
{
    public int LineCount =>
        Vertices.Length / 2;
}

public sealed class NativeTrafficPathGeometryBuilder
{
    private static readonly Vector4 RoadColor =
        new(0.92f, 0.58f, 0.16f, 1.0f);

    private static readonly Vector4 PedestrianColor =
        new(0.30f, 0.92f, 0.42f, 1.0f);

    private static readonly Vector4 RailColor =
        new(0.24f, 0.82f, 1.0f, 1.0f);

    private static readonly Vector4 AirColor =
        new(0.80f, 0.46f, 1.0f, 1.0f);

    public NativeTrafficPathGeometry Build(
        NativeSceneSnapshot scene,
        IReadOnlyDictionary<
            string,
            NativeSplineAsset> assets)
    {
        ArgumentNullException.ThrowIfNull(
            scene);

        ArgumentNullException.ThrowIfNull(
            assets);

        var vertices =
            new List<NativeMapVertex>(
                Math.Max(
                    256,
                    scene.Splines.Count *
                        32));

        var pathCount = 0;

        foreach (
            var entity in
                scene.Splines)
        {
            if (
                !assets.TryGetValue(
                    entity.Spline
                        .SplinePath,
                    out var asset) ||
                !asset.Definition.Exists ||
                asset.Definition
                    .Paths.Count == 0)
            {
                continue;
            }

            var length =
                Math.Max(
                    0.0,
                    entity.Spline.Length);

            if (length < 0.01)
            {
                continue;
            }

            foreach (
                var path in
                    asset.Definition
                        .Paths)
            {
                AppendPath(
                    entity,
                    path,
                    vertices);

                pathCount++;
            }
        }

        return new NativeTrafficPathGeometry(
            vertices.ToArray(),
            pathCount);
    }

    private static void AppendPath(
        NativeSplineEntity entity,
        OmsiSplinePathDefinition path,
        List<NativeMapVertex> output)
    {
        var length =
            entity.Spline.Length;

        var segmentCount =
            Math.Clamp(
                (int)Math.Ceiling(
                    length /
                    5.0),
                2,
                128);

        var color =
            path.Type switch
            {
                0 => RoadColor,
                1 => PedestrianColor,
                2 => RailColor,
                3 => AirColor,
                _ => RoadColor
            };

        AppendPathStrip(
            entity,
            path,
            0,
            segmentCount,
            color,
            output);

        if (path.Width > 0.05)
        {
            var halfWidth =
                path.Width /
                2.0;

            AppendPathStrip(
                entity,
                path,
                -halfWidth,
                segmentCount,
                color,
                output);

            AppendPathStrip(
                entity,
                path,
                halfWidth,
                segmentCount,
                color,
                output);
        }

        if (path.Direction is 0 or 2)
        {
            AppendDirectionArrow(
                entity,
                path,
                length * 0.62,
                true,
                color,
                output);
        }

        if (path.Direction is 1 or 2)
        {
            AppendDirectionArrow(
                entity,
                path,
                length * 0.38,
                false,
                color,
                output);
        }
    }

    private static void AppendPathStrip(
        NativeSplineEntity entity,
        OmsiSplinePathDefinition path,
        double lateralOffset,
        int segmentCount,
        Vector4 color,
        List<NativeMapVertex> output)
    {
        var length =
            entity.Spline.Length;

        var previous =
            GetPathPoint(
                NativeSplinePathMath
                    .GetFrame(
                        entity,
                        0),
                path,
                lateralOffset);

        for (
            var index = 1;
            index <= segmentCount;
            index++)
        {
            var current =
                GetPathPoint(
                    NativeSplinePathMath
                        .GetFrame(
                            entity,
                            length *
                            index /
                            segmentCount),
                    path,
                    lateralOffset);

            AddLine(
                output,
                previous,
                current,
                color);

            previous =
                current;
        }
    }

    private static void AppendDirectionArrow(
        NativeSplineEntity entity,
        OmsiSplinePathDefinition path,
        double distance,
        bool forward,
        Vector4 color,
        List<NativeMapVertex> output)
    {
        var frame =
            NativeSplinePathMath
                .GetFrame(
                    entity,
                    distance);

        var center =
            GetPathPoint(
                frame,
                path,
                0);

        var direction =
            forward
                ? frame.Forward
                : -frame.Forward;

        var point =
            center +
            direction *
            1.4f;

        var basePoint =
            center -
            direction *
            0.7f;

        var wing =
            frame.Lateral *
            0.65f;

        AddLine(
            output,
            point,
            basePoint + wing,
            color);

        AddLine(
            output,
            point,
            basePoint - wing,
            color);
    }

    private static Vector3 GetPathPoint(
        NativeSplineFrame frame,
        OmsiSplinePathDefinition path,
        double extraLateral) =>
        frame.Center +
        frame.Lateral *
            (float)(
                path.X +
                extraLateral) +
        Vector3.UnitY *
            (float)(
                path.Z +
                0.08);

    private static void AddLine(
        List<NativeMapVertex> output,
        Vector3 from,
        Vector3 to,
        Vector4 color)
    {
        output.Add(
            new NativeMapVertex(
                from,
                color));

        output.Add(
            new NativeMapVertex(
                to,
                color));
    }
}
