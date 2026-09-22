using System.Numerics;
using MapStudio.Core.Omsi.Splines;
using MapStudio.Core.Omsi.Scenery;

namespace MapStudio.Renderer.Scene;

public sealed record NativeTrafficPathGeometry(
    NativeMapVertex[] Vertices,
    int PathCount,
    int VehiclePathCount = 0,
    int PedestrianPathCount = 0,
    int RailPathCount = 0,
    int AirPathCount = 0)
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
            NativeSplineAsset> assets,
        IReadOnlyDictionary<
            string,
            NativeSceneryAsset>? sceneryAssets =
                null,
        NativeTrafficPathDisplayOptions?
            displayOptions = null,
        int? focusedPathIndex = null)
    {
        ArgumentNullException.ThrowIfNull(
            scene);

        ArgumentNullException.ThrowIfNull(
            assets);

        var options =
            displayOptions ??
            NativeTrafficPathDisplayOptions
                .AllDetailed;

        var vertices =
            new List<NativeMapVertex>(
                Math.Max(
                    256,
                    scene.Splines.Count *
                        32));

        var pathCount = 0;
        var vehiclePathCount = 0;
        var pedestrianPathCount = 0;
        var railPathCount = 0;
        var airPathCount = 0;

        void CountPath(
            int type)
        {
            pathCount++;

            switch (type)
            {
                case 1:
                    pedestrianPathCount++;
                    break;

                case 2:
                    railPathCount++;
                    break;

                case 3:
                    airPathCount++;
                    break;

                default:
                    vehiclePathCount++;
                    break;
            }
        }

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

            for (
                var pathIndex = 0;
                pathIndex <
                    asset.Definition
                        .Paths.Count;
                pathIndex++)
            {
                if (
                    focusedPathIndex.HasValue &&
                    pathIndex !=
                        focusedPathIndex.Value)
                {
                    continue;
                }

                var path =
                    asset.Definition
                        .Paths[pathIndex];

                if (!options.IncludesType(
                        path.Type))
                {
                    continue;
                }

                AppendPath(
                    entity,
                    path,
                    options,
                    vertices);

                CountPath(
                    path.Type);
            }
        }

        if (sceneryAssets is not null)
        {
            foreach (
                var entity in
                    scene.Objects)
            {
                if (
                    !sceneryAssets.TryGetValue(
                        entity.Object
                            .SceneryObjectPath,
                        out var asset) ||
                    asset.Paths.Count == 0)
                {
                    continue;
                }

                var terrainOffset =
                    asset.UsesAbsoluteHeight
                        ? 0.0
                        : NativeTerrainSampler
                            .GetHeightAtObject(
                                scene,
                                entity);

                for (
                    var pathIndex = 0;
                    pathIndex <
                        asset.Paths.Count;
                    pathIndex++)
                {
                    if (
                        focusedPathIndex.HasValue &&
                        pathIndex !=
                            focusedPathIndex.Value)
                    {
                        continue;
                    }

                    var path =
                        asset.Paths[pathIndex];

                    if (!options.IncludesType(
                            path.Type))
                    {
                        continue;
                    }

                    AppendSceneryPath(
                        entity,
                        path,
                        terrainOffset,
                        options,
                        vertices);

                    CountPath(
                        path.Type);
                }
            }
        }

        return new NativeTrafficPathGeometry(
            vertices.ToArray(),
            pathCount,
            vehiclePathCount,
            pedestrianPathCount,
            railPathCount,
            airPathCount);
    }

    private static void AppendPath(
        NativeSplineEntity entity,
        OmsiSplinePathDefinition path,
        NativeTrafficPathDisplayOptions
            options,
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

        if (
            options.ShowWidthEdges &&
            path.Width > 0.05)
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

        if (
            options.ShowDirectionArrows &&
            path.Direction is 0 or 2)
        {
            AppendDirectionArrow(
                entity,
                path,
                length * 0.62,
                true,
                color,
                output);
        }

        if (
            options.ShowDirectionArrows &&
            path.Direction is 1 or 2)
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
                GetOverlayHeight(
                    path.Type));

    private static void AppendSceneryPath(
        NativeObjectEntity entity,
        OmsiSceneryPathDefinition path,
        double terrainOffset,
        NativeTrafficPathDisplayOptions
            options,
        List<NativeMapVertex> output)
    {
        if (path.Length <= 0.01)
        {
            return;
        }

        var color =
            options.HighlightSignalControlled &&
            path.TrafficLightIndex.HasValue
                ? new Vector4(
                    1.0f,
                    0.25f,
                    0.12f,
                    1.0f)
                : path.Type switch
                {
                    0 => RoadColor,
                    1 => PedestrianColor,
                    2 => RailColor,
                    3 => AirColor,
                    _ => RoadColor
                };

        var segmentCount =
            Math.Clamp(
                (int)Math.Ceiling(
                    path.Length / 3.0),
                2,
                128);

        var objectTransform =
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

        AppendSceneryPathStrip(
            path,
            0,
            segmentCount,
            objectTransform,
            color,
            output);

        if (
            options.ShowWidthEdges &&
            path.Width > 0.05)
        {
            var halfWidth =
                path.Width / 2.0;

            AppendSceneryPathStrip(
                path,
                -halfWidth,
                segmentCount,
                objectTransform,
                color,
                output);

            AppendSceneryPathStrip(
                path,
                halfWidth,
                segmentCount,
                objectTransform,
                color,
                output);
        }

        if (
            options.ShowDirectionArrows &&
            path.Direction is 0 or 2)
        {
            AppendSceneryDirectionArrow(
                path,
                path.Length * 0.62,
                true,
                objectTransform,
                color,
                output);
        }

        if (
            options.ShowDirectionArrows &&
            path.Direction is 1 or 2)
        {
            AppendSceneryDirectionArrow(
                path,
                path.Length * 0.38,
                false,
                objectTransform,
                color,
                output);
        }
    }

    private static void AppendSceneryPathStrip(
        OmsiSceneryPathDefinition path,
        double lateralOffset,
        int segmentCount,
        Matrix4x4 objectTransform,
        Vector4 color,
        List<NativeMapVertex> output)
    {
        var previous =
            GetSceneryPathPoint(
                path,
                0,
                lateralOffset,
                objectTransform);

        for (
            var index = 1;
            index <= segmentCount;
            index++)
        {
            var distance =
                path.Length *
                index /
                segmentCount;

            var current =
                GetSceneryPathPoint(
                    path,
                    distance,
                    lateralOffset,
                    objectTransform);

            AddLine(
                output,
                previous,
                current,
                color);

            previous =
                current;
        }
    }

    private static Vector3 GetSceneryPathPoint(
        OmsiSceneryPathDefinition path,
        double distance,
        double lateralOffset,
        Matrix4x4 objectTransform)
    {
        var clamped =
            Math.Clamp(
                distance,
                0.0,
                path.Length);

        var heading =
            DegreesToRadians(
                path.Rotation);

        var hasCurve =
            Math.Abs(
                path.Radius) >
            0.001;

        var curveAngle =
            hasCurve
                ? clamped /
                    path.Radius
                : 0.0;

        var localXCurve =
            hasCurve
                ? path.Radius *
                    (
                        1.0 -
                        Math.Cos(
                            curveAngle)
                    )
                : 0.0;

        var localYCurve =
            hasCurve
                ? path.Radius *
                    Math.Sin(
                        curveAngle)
                : clamped;

        var cos =
            Math.Cos(
                heading);

        var sin =
            Math.Sin(
                heading);

        var forwardX =
            localXCurve *
                cos +
            localYCurve *
                sin;

        var forwardY =
            -localXCurve *
                sin +
            localYCurve *
                cos;

        var currentHeading =
            heading +
            curveAngle;

        var lateralX =
            Math.Cos(
                currentHeading);

        var lateralY =
            -Math.Sin(
                currentHeading);

        var rise =
            GetGradientRise(
                path.GradientStart,
                path.GradientEnd,
                path.Length,
                clamped);

        var local =
            new Vector3(
                (float)(
                    path.X +
                    forwardX +
                    lateralX *
                        lateralOffset),
                (float)(
                    path.Z +
                    rise +
                    GetOverlayHeight(
                        path.Type)),
                (float)(
                    path.Y +
                    forwardY +
                    lateralY *
                        lateralOffset));

        return Vector3.Transform(
            local,
            objectTransform);
    }

    private static void AppendSceneryDirectionArrow(
        OmsiSceneryPathDefinition path,
        double distance,
        bool forward,
        Matrix4x4 objectTransform,
        Vector4 color,
        List<NativeMapVertex> output)
    {
        var center =
            GetSceneryPathPoint(
                path,
                distance,
                0,
                objectTransform);

        var near =
            GetSceneryPathPoint(
                path,
                Math.Clamp(
                    distance +
                    (forward ? 0.5 : -0.5),
                    0,
                    path.Length),
                0,
                objectTransform);

        var direction =
            Vector3.Normalize(
                near -
                center);

        if (!forward)
        {
            direction =
                -direction;
        }

        var up =
            Vector3.UnitY;

        var lateral =
            Vector3.Normalize(
                Vector3.Cross(
                    up,
                    direction));

        if (
            !float.IsFinite(
                lateral.X))
        {
            lateral =
                Vector3.UnitX;
        }

        var tip =
            center +
            direction *
                1.2f;

        var basePoint =
            center -
            direction *
                0.5f;

        AddLine(
            output,
            tip,
            basePoint +
                lateral *
                0.55f,
            color);

        AddLine(
            output,
            tip,
            basePoint -
                lateral *
                0.55f,
            color);
    }

    private static double GetOverlayHeight(
        int type) =>
        type switch
        {
            1 => 0.16,
            2 => 0.20,
            3 => 0.24,
            _ => 0.12
        };

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

        var clamped =
            Math.Clamp(
                distance,
                0,
                length);

        var startSlope =
            start /
            100.0;

        var delta =
            (
                end -
                start
            ) /
            100.0;

        return
            startSlope *
                clamped +
            0.5 *
                delta *
                clamped *
                clamped /
                length;
    }

    private static float DegreesToRadians(
        double value) =>
        (float)(
            value *
            Math.PI /
            180.0);

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
