using System.Numerics;
using MapStudio.Core.Omsi.Splines;
using MapStudio.Core.Omsi.Scenery;

namespace MapStudio.Renderer.Scene;

public sealed record NativeTrafficPathGeometry(
    NativeMapVertex[] Vertices,
    NativeMapVertex[] TriangleVertices,
    int PathCount,
    int VehiclePathCount = 0,
    int PedestrianPathCount = 0,
    int RailPathCount = 0,
    int AirPathCount = 0)
{
    public int LineCount =>
        Vertices.Length / 2;

    public int TriangleCount =>
        TriangleVertices.Length / 3;
}

public sealed class NativeTrafficPathGeometryBuilder
{
    // High-contrast editor palette inspired by the OMSI path overlay.
    // Vehicle paths use the familiar red strip; pedestrian paths stay
    // nearly white so they remain readable on asphalt and grass.
    private static readonly Vector4 RoadColor =
        new(0.96f, 0.08f, 0.06f, 1.0f);

    private static readonly Vector4 PedestrianColor =
        new(0.94f, 0.95f, 0.96f, 1.0f);

    private static readonly Vector4 RailColor =
        new(0.12f, 0.76f, 1.0f, 1.0f);

    private static readonly Vector4 AirColor =
        new(0.74f, 0.34f, 1.0f, 1.0f);

    private static readonly Vector4 DarkMarkerColor =
        new(0.035f, 0.045f, 0.055f, 1.0f);

    private static readonly Vector4 LightMarkerColor =
        new(0.96f, 0.98f, 1.0f, 1.0f);

    private static readonly Vector4 FocusMarkerColor =
        new(1.0f, 0.92f, 0.12f, 1.0f);

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

        var triangleVertices =
            new List<NativeMapVertex>(
                Math.Max(
                    384,
                    scene.Splines.Count *
                        48));

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
                    vertices,
                    triangleVertices,
                    focusedPathIndex.HasValue &&
                    pathIndex ==
                        focusedPathIndex.Value);

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
                        vertices,
                        triangleVertices,
                        focusedPathIndex.HasValue &&
                        pathIndex ==
                            focusedPathIndex.Value);

                    CountPath(
                        path.Type);
                }
            }
        }

        return new NativeTrafficPathGeometry(
            vertices.ToArray(),
            triangleVertices.ToArray(),
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
        List<NativeMapVertex> lineOutput,
        List<NativeMapVertex> triangleOutput,
        bool focused)
    {
        var length =
            entity.Spline.Length;

        var segmentCount =
            Math.Clamp(
                (int)Math.Ceiling(
                    length /
                    3.0),
                2,
                192);

        var color =
            GetBaseColor(
                path.Type);

        var markerColor =
            focused
                ? FocusMarkerColor
                : GetMarkerColor(
                    path.Type);

        var ribbonHalfWidth =
            GetRibbonHalfWidth(
                path.Type,
                path.Width,
                focused);

        AppendSplineRibbon(
            entity,
            path,
            segmentCount,
            ribbonHalfWidth,
            GetRibbonColor(
                color,
                focused),
            triangleOutput);

        // A thin center line and dark/light border make the path readable
        // over both bright pavement and dark terrain textures.
        AppendPathStrip(
            entity,
            path,
            0,
            segmentCount,
            markerColor,
            lineOutput);

        AppendPathStrip(
            entity,
            path,
            -ribbonHalfWidth,
            segmentCount,
            markerColor,
            lineOutput);

        AppendPathStrip(
            entity,
            path,
            ribbonHalfWidth,
            segmentCount,
            markerColor,
            lineOutput);

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
                lineOutput);

            AppendPathStrip(
                entity,
                path,
                halfWidth,
                segmentCount,
                color,
                lineOutput);
        }

        if (options.ShowDirectionArrows)
        {
            AppendRepeatedDirectionArrows(
                entity,
                path,
                length,
                markerColor,
                lineOutput);
        }
    }

    private static void AppendSplineRibbon(
        NativeSplineEntity entity,
        OmsiSplinePathDefinition path,
        int segmentCount,
        double halfWidth,
        Vector4 color,
        List<NativeMapVertex> output)
    {
        var length =
            entity.Spline.Length;

        var startFrame =
            NativeSplinePathMath
                .GetFrame(
                    entity,
                    0);

        var previousLeft =
            GetPathPoint(
                startFrame,
                path,
                -halfWidth);

        var previousRight =
            GetPathPoint(
                startFrame,
                path,
                halfWidth);

        for (
            var index = 1;
            index <= segmentCount;
            index++)
        {
            var frame =
                NativeSplinePathMath
                    .GetFrame(
                        entity,
                        length *
                        index /
                        segmentCount);

            var currentLeft =
                GetPathPoint(
                    frame,
                    path,
                    -halfWidth);

            var currentRight =
                GetPathPoint(
                    frame,
                    path,
                    halfWidth);

            AddQuad(
                output,
                previousLeft,
                previousRight,
                currentRight,
                currentLeft,
                color);

            previousLeft =
                currentLeft;

            previousRight =
                currentRight;
        }
    }

    private static void AppendRepeatedDirectionArrows(
        NativeSplineEntity entity,
        OmsiSplinePathDefinition path,
        double length,
        Vector4 color,
        List<NativeMapVertex> output)
    {
        if (length <= 0.5)
        {
            return;
        }

        var spacing =
            path.Type switch
            {
                1 => 4.5,
                2 => 7.0,
                3 => 9.0,
                _ => 6.0
            };

        var first =
            Math.Min(
                length * 0.5,
                spacing * 0.55);

        var count = 0;

        for (
            var distance = first;
            distance <
                length - 0.25 &&
            count < 48;
            distance += spacing,
            count++)
        {
            if (path.Direction is 0 or 2)
            {
                AppendDirectionArrow(
                    entity,
                    path,
                    distance,
                    true,
                    color,
                    output);
            }

            if (path.Direction is 1 or 2)
            {
                var reverseDistance =
                    path.Direction == 2
                        ? Math.Min(
                            length - 0.25,
                            distance +
                            spacing * 0.35)
                        : distance;

                AppendDirectionArrow(
                    entity,
                    path,
                    reverseDistance,
                    false,
                    color,
                    output);
            }
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
        List<NativeMapVertex> lineOutput,
        List<NativeMapVertex> triangleOutput,
        bool focused)
    {
        if (path.Length <= 0.01)
        {
            return;
        }

        var baseColor =
            options.HighlightSignalControlled &&
            path.TrafficLightIndex.HasValue
                ? new Vector4(
                    1.0f,
                    0.42f,
                    0.06f,
                    1.0f)
                : GetBaseColor(
                    path.Type);

        var markerColor =
            focused
                ? FocusMarkerColor
                : GetMarkerColor(
                    path.Type);

        var segmentCount =
            Math.Clamp(
                (int)Math.Ceiling(
                    path.Length / 2.5),
                2,
                192);

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

        var ribbonHalfWidth =
            GetRibbonHalfWidth(
                path.Type,
                path.Width,
                focused);

        AppendSceneryRibbon(
            path,
            segmentCount,
            ribbonHalfWidth,
            objectTransform,
            GetRibbonColor(
                baseColor,
                focused),
            triangleOutput);

        AppendSceneryPathStrip(
            path,
            0,
            segmentCount,
            objectTransform,
            markerColor,
            lineOutput);

        AppendSceneryPathStrip(
            path,
            -ribbonHalfWidth,
            segmentCount,
            objectTransform,
            markerColor,
            lineOutput);

        AppendSceneryPathStrip(
            path,
            ribbonHalfWidth,
            segmentCount,
            objectTransform,
            markerColor,
            lineOutput);

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
                baseColor,
                lineOutput);

            AppendSceneryPathStrip(
                path,
                halfWidth,
                segmentCount,
                objectTransform,
                baseColor,
                lineOutput);
        }

        if (options.ShowDirectionArrows)
        {
            AppendRepeatedSceneryDirectionArrows(
                path,
                objectTransform,
                markerColor,
                lineOutput);
        }
    }

    private static void AppendSceneryRibbon(
        OmsiSceneryPathDefinition path,
        int segmentCount,
        double halfWidth,
        Matrix4x4 objectTransform,
        Vector4 color,
        List<NativeMapVertex> output)
    {
        var previousLeft =
            GetSceneryPathPoint(
                path,
                0,
                -halfWidth,
                objectTransform);

        var previousRight =
            GetSceneryPathPoint(
                path,
                0,
                halfWidth,
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

            var currentLeft =
                GetSceneryPathPoint(
                    path,
                    distance,
                    -halfWidth,
                    objectTransform);

            var currentRight =
                GetSceneryPathPoint(
                    path,
                    distance,
                    halfWidth,
                    objectTransform);

            AddQuad(
                output,
                previousLeft,
                previousRight,
                currentRight,
                currentLeft,
                color);

            previousLeft =
                currentLeft;

            previousRight =
                currentRight;
        }
    }

    private static void AppendRepeatedSceneryDirectionArrows(
        OmsiSceneryPathDefinition path,
        Matrix4x4 objectTransform,
        Vector4 color,
        List<NativeMapVertex> output)
    {
        var spacing =
            path.Type switch
            {
                1 => 4.5,
                2 => 7.0,
                3 => 9.0,
                _ => 6.0
            };

        var first =
            Math.Min(
                path.Length * 0.5,
                spacing * 0.55);

        var count = 0;

        for (
            var distance = first;
            distance <
                path.Length - 0.25 &&
            count < 48;
            distance += spacing,
            count++)
        {
            if (path.Direction is 0 or 2)
            {
                AppendSceneryDirectionArrow(
                    path,
                    distance,
                    true,
                    objectTransform,
                    color,
                    output);
            }

            if (path.Direction is 1 or 2)
            {
                var reverseDistance =
                    path.Direction == 2
                        ? Math.Min(
                            path.Length - 0.25,
                            distance +
                            spacing * 0.35)
                        : distance;

                AppendSceneryDirectionArrow(
                    path,
                    reverseDistance,
                    false,
                    objectTransform,
                    color,
                    output);
            }
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

    private static Vector4 GetBaseColor(
        int type) =>
        type switch
        {
            1 => PedestrianColor,
            2 => RailColor,
            3 => AirColor,
            _ => RoadColor
        };

    private static Vector4 GetMarkerColor(
        int type) =>
        type switch
        {
            2 or 3 =>
                LightMarkerColor,
            _ =>
                DarkMarkerColor
        };

    private static Vector4 GetRibbonColor(
        Vector4 color,
        bool focused) =>
        new(
            color.X,
            color.Y,
            color.Z,
            focused
                ? 0.88f
                : 0.68f);

    private static double GetRibbonHalfWidth(
        int type,
        double declaredWidth,
        bool focused)
    {
        var width =
            Math.Max(
                0,
                declaredWidth);

        var halfWidth =
            type switch
            {
                1 =>
                    Math.Clamp(
                        width * 0.28,
                        0.34,
                        0.62),
                2 =>
                    Math.Clamp(
                        width * 0.22,
                        0.38,
                        0.66),
                3 =>
                    Math.Clamp(
                        width * 0.20,
                        0.34,
                        0.60),
                _ =>
                    Math.Clamp(
                        width * 0.20,
                        0.46,
                        0.76)
            };

        return focused
            ? halfWidth * 1.22
            : halfWidth;
    }

    private static void AddQuad(
        List<NativeMapVertex> output,
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Vector3 d,
        Vector4 color)
    {
        output.Add(
            new NativeMapVertex(
                a,
                color));

        output.Add(
            new NativeMapVertex(
                b,
                color));

        output.Add(
            new NativeMapVertex(
                c,
                color));

        output.Add(
            new NativeMapVertex(
                a,
                color));

        output.Add(
            new NativeMapVertex(
                c,
                color));

        output.Add(
            new NativeMapVertex(
                d,
                color));
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
