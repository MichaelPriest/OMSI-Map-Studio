using System.Globalization;
using System.Numerics;
using MapStudio.Core.Omsi.Scenery;

namespace MapStudio.Renderer.Scene;

public sealed record NativeTimetableRouteGeometry(
    NativeMapVertex[] Vertices,
    int ResolvedReferenceCount);

public sealed class NativeTimetableRouteGeometryBuilder
{
    private static readonly Vector4 RouteColor =
        new(
            1.0f,
            0.86f,
            0.16f,
            1.0f);

    public NativeTimetableRouteGeometry Build(
        NativeSceneSnapshot scene,
        IReadOnlyDictionary<
            string,
            NativeSplineAsset> splineAssets,
        IReadOnlyDictionary<
            string,
            NativeSceneryAsset> sceneryAssets,
        IReadOnlyList<
            NativeTimetablePathReference>
            references)
    {
        ArgumentNullException.ThrowIfNull(
            scene);

        var vertices =
            new List<NativeMapVertex>();

        var resolved = 0;

        foreach (
            var reference in
                references)
        {
            if (
                !int.TryParse(
                    reference.PathIndexText,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var pathIndex) ||
                pathIndex < 0)
            {
                continue;
            }

            var spline =
                scene.Splines
                    .FirstOrDefault(
                        item =>
                            item.Spline
                                .SplineId ==
                            reference.EntityId);

            if (
                spline is not null &&
                splineAssets.TryGetValue(
                    spline.Spline
                        .SplinePath,
                    out var splineAsset) &&
                pathIndex <
                    splineAsset.Definition
                        .Paths.Count)
            {
                AppendSplinePath(
                    spline,
                    splineAsset.Definition
                        .Paths[pathIndex],
                    reference.Length,
                    vertices);

                resolved++;
                continue;
            }

            var obj =
                scene.Objects
                    .FirstOrDefault(
                        item =>
                            item.Object
                                .ObjectId ==
                            reference.EntityId);

            if (
                obj is not null &&
                sceneryAssets.TryGetValue(
                    obj.Object
                        .SceneryObjectPath,
                    out var sceneryAsset) &&
                pathIndex <
                    sceneryAsset.Paths.Count)
            {
                var terrainOffset =
                    sceneryAsset
                        .UsesAbsoluteHeight
                        ? 0.0
                        : NativeTerrainSampler
                            .GetHeightAtObject(
                                scene,
                                obj);

                AppendObjectPath(
                    obj,
                    sceneryAsset
                        .Paths[pathIndex],
                    terrainOffset,
                    reference.Length,
                    vertices);

                resolved++;
            }
        }

        return new NativeTimetableRouteGeometry(
            vertices.ToArray(),
            resolved);
    }

    private static void AppendSplinePath(
        NativeSplineEntity entity,
        MapStudio.Core.Omsi.Splines
            .OmsiSplinePathDefinition path,
        double? requestedLength,
        List<NativeMapVertex> output)
    {
        var length =
            Math.Min(
                entity.Spline.Length,
                requestedLength is
                    > 0
                    ? requestedLength.Value
                    : entity.Spline.Length);

        if (length <= 0.01)
        {
            return;
        }

        var segments =
            Math.Clamp(
                (int)Math.Ceiling(
                    length / 3.0),
                2,
                128);

        var previous =
            GetSplinePoint(
                entity,
                path,
                0);

        for (
            var index = 1;
            index <= segments;
            index++)
        {
            var current =
                GetSplinePoint(
                    entity,
                    path,
                    length *
                    index /
                    segments);

            AddLine(
                output,
                previous,
                current);

            previous =
                current;
        }
    }

    private static Vector3 GetSplinePoint(
        NativeSplineEntity entity,
        MapStudio.Core.Omsi.Splines
            .OmsiSplinePathDefinition path,
        double distance)
    {
        var frame =
            NativeSplinePathMath
                .GetFrame(
                    entity,
                    distance);

        return
            frame.Center +
            frame.Lateral *
                (float)path.X +
            Vector3.UnitY *
                (float)(
                    path.Z +
                    0.38);
    }

    private static void AppendObjectPath(
        NativeObjectEntity entity,
        OmsiSceneryPathDefinition path,
        double terrainOffset,
        double? requestedLength,
        List<NativeMapVertex> output)
    {
        var length =
            Math.Min(
                path.Length,
                requestedLength is
                    > 0
                    ? requestedLength.Value
                    : path.Length);

        if (length <= 0.01)
        {
            return;
        }

        var transform =
            Matrix4x4.CreateFromYawPitchRoll(
                DegreesToRadians(
                    entity.Object.Rotation),
                DegreesToRadians(
                    entity.Object.Pitch),
                DegreesToRadians(
                    entity.Object.Bank)) *
            Matrix4x4.CreateTranslation(
                entity.WorldX,
                entity.WorldY +
                    (float)terrainOffset,
                entity.WorldZ);

        var segments =
            Math.Clamp(
                (int)Math.Ceiling(
                    length / 2.0),
                2,
                128);

        var previous =
            GetObjectPathPoint(
                path,
                0,
                transform);

        for (
            var index = 1;
            index <= segments;
            index++)
        {
            var current =
                GetObjectPathPoint(
                    path,
                    length *
                    index /
                    segments,
                    transform);

            AddLine(
                output,
                previous,
                current);

            previous =
                current;
        }
    }

    private static Vector3 GetObjectPathPoint(
        OmsiSceneryPathDefinition path,
        double distance,
        Matrix4x4 transform)
    {
        var clamped =
            Math.Clamp(
                distance,
                0,
                path.Length);

        var heading =
            DegreesToRadians(
                path.Rotation);

        var curved =
            Math.Abs(
                path.Radius) >
            0.001;

        var angle =
            curved
                ? clamped /
                    path.Radius
                : 0.0;

        var curveX =
            curved
                ? path.Radius *
                    (
                        1 -
                        Math.Cos(angle)
                    )
                : 0.0;

        var curveY =
            curved
                ? path.Radius *
                    Math.Sin(angle)
                : clamped;

        var cos =
            Math.Cos(heading);

        var sin =
            Math.Sin(heading);

        var localX =
            path.X +
            curveX * cos +
            curveY * sin;

        var localY =
            path.Y -
            curveX * sin +
            curveY * cos;

        var rise =
            GetGradientRise(
                path.GradientStart,
                path.GradientEnd,
                path.Length,
                clamped);

        return Vector3.Transform(
            new Vector3(
                (float)localX,
                (float)(
                    path.Z +
                    rise +
                    0.38),
                (float)localY),
            transform);
    }

    private static double GetGradientRise(
        double start,
        double end,
        double length,
        double distance)
    {
        if (length <= 0)
        {
            return 0;
        }

        var startSlope =
            start / 100.0;

        var delta =
            (
                end -
                start
            ) /
            100.0;

        return
            startSlope *
                distance +
            0.5 *
                delta *
                distance *
                distance /
                length;
    }

    private static void AddLine(
        List<NativeMapVertex> output,
        Vector3 from,
        Vector3 to)
    {
        output.Add(
            new NativeMapVertex(
                from,
                RouteColor));

        output.Add(
            new NativeMapVertex(
                to,
                RouteColor));
    }

    private static float DegreesToRadians(
        double value) =>
        (float)(
            value *
            Math.PI /
            180.0);
}
