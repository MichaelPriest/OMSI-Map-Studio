using System.Numerics;

namespace MapStudio.Core.Omsi.Maps;

public sealed record OmsiTerrainLevelResult(
    OmsiTerrainGrid Terrain,
    int ChangedSamples);

public sealed record OmsiTerrainSplinePreviewBand(
    IReadOnlyList<Vector3> Centerline,
    IReadOnlyList<Vector3> InnerLeft,
    IReadOnlyList<Vector3> InnerRight,
    IReadOnlyList<Vector3> OuterLeft,
    IReadOnlyList<Vector3> OuterRight);

public static class OmsiTerrainLeveler
{
    private const double TileSize = OmsiTileGrid.TileSize;

    public static OmsiTerrainLevelResult
        LevelCircularBrush(
            OmsiTerrainGrid terrain,
            double localX,
            double localY,
            double targetHeight,
            double radius,
            double feather)
    {
        ArgumentNullException.ThrowIfNull(
            terrain);

        if (
            !double.IsFinite(localX) ||
            !double.IsFinite(localY) ||
            !double.IsFinite(targetHeight) ||
            !double.IsFinite(radius) ||
            !double.IsFinite(feather) ||
            radius <= 0 ||
            feather < 0 ||
            feather > 1)
        {
            throw new InvalidDataException(
                "invalidTerrainBrush");
        }

        var cellCount =
            terrain.CellCount;

        if (cellCount <= 0)
        {
            throw new InvalidDataException(
                "invalidTerrainCellCount");
        }

        var sampleCount =
            cellCount + 1;

        if (
            terrain.Heights.Count !=
                sampleCount *
                sampleCount)
        {
            throw new InvalidDataException(
                "invalidTerrainHeightCount");
        }

        var spacing =
            TileSize /
            cellCount;

        var innerRadius =
            radius *
            (1 - feather);

        var next =
            terrain.Heights
                .ToArray();

        var changed = 0;

        for (
            var row = 0;
            row < sampleCount;
            row++)
        {
            var sampleY =
                row * spacing;

            for (
                var column = 0;
                column < sampleCount;
                column++)
            {
                var sampleX =
                    column * spacing;

                var distance =
                    Math.Sqrt(
                        Math.Pow(
                            sampleX - localX,
                            2) +
                        Math.Pow(
                            sampleY - localY,
                            2));

                if (distance > radius)
                {
                    continue;
                }

                double weight;

                if (
                    feather <= 0 ||
                    distance <=
                        innerRadius)
                {
                    weight = 1;
                }
                else
                {
                    var featherWidth =
                        Math.Max(
                            0.000001,
                            radius -
                            innerRadius);

                    weight =
                        1 -
                        (
                            distance -
                            innerRadius
                        ) /
                        featherWidth;

                    weight =
                        Math.Clamp(
                            weight,
                            0,
                            1);
                }

                var index =
                    row *
                    sampleCount +
                    column;

                var current =
                    next[index];

                var updated =
                    current +
                    (
                        targetHeight -
                        current
                    ) *
                    weight;

                var asFloat =
                    (float)updated;

                if (
                    Math.Abs(
                        asFloat -
                        current) >
                    0.00001f)
                {
                    next[index] =
                        asFloat;
                    changed++;
                }
            }
        }

        return new OmsiTerrainLevelResult(
            new OmsiTerrainGrid(
                terrain.CellCount,
                next),
            changed);
    }

    public static OmsiTerrainLevelResult
        OffsetCircularBrush(
            OmsiTerrainGrid terrain,
            double localX,
            double localY,
            double deltaHeight,
            double radius,
            double feather)
    {
        ArgumentNullException.ThrowIfNull(
            terrain);

        if (
            !double.IsFinite(localX) ||
            !double.IsFinite(localY) ||
            !double.IsFinite(deltaHeight) ||
            Math.Abs(deltaHeight) <
                0.000001 ||
            !double.IsFinite(radius) ||
            !double.IsFinite(feather) ||
            radius <= 0 ||
            feather < 0 ||
            feather > 1)
        {
            throw new InvalidDataException(
                "invalidTerrainOffsetBrush");
        }

        var cellCount =
            terrain.CellCount;

        if (cellCount <= 0)
        {
            throw new InvalidDataException(
                "invalidTerrainCellCount");
        }

        var sampleCount =
            cellCount + 1;

        if (
            terrain.Heights.Count !=
                sampleCount *
                sampleCount)
        {
            throw new InvalidDataException(
                "invalidTerrainHeightCount");
        }

        var spacing =
            TileSize /
            cellCount;

        var innerRadius =
            radius *
            (1 - feather);

        var next =
            terrain.Heights
                .ToArray();

        var changed =
            0;

        for (
            var row = 0;
            row < sampleCount;
            row++)
        {
            var sampleY =
                row *
                spacing;

            for (
                var column = 0;
                column < sampleCount;
                column++)
            {
                var sampleX =
                    column *
                    spacing;

                var distance =
                    Math.Sqrt(
                        Math.Pow(
                            sampleX - localX,
                            2) +
                        Math.Pow(
                            sampleY - localY,
                            2));

                if (distance > radius)
                {
                    continue;
                }

                double weight;

                if (
                    feather <= 0 ||
                    distance <=
                        innerRadius)
                {
                    weight =
                        1;
                }
                else
                {
                    var featherWidth =
                        Math.Max(
                            0.000001,
                            radius -
                            innerRadius);

                    weight =
                        1 -
                        (
                            distance -
                            innerRadius
                        ) /
                        featherWidth;

                    weight =
                        Math.Clamp(
                            weight,
                            0,
                            1);
                }

                var index =
                    row *
                    sampleCount +
                    column;

                var current =
                    next[index];

                var updated =
                    current +
                    deltaHeight *
                    weight;

                var asFloat =
                    (float)updated;

                if (
                    Math.Abs(
                        asFloat -
                        current) >
                    0.00001f)
                {
                    next[index] =
                        asFloat;

                    changed++;
                }
            }
        }

        return new OmsiTerrainLevelResult(
            new OmsiTerrainGrid(
                terrain.CellCount,
                next),
            changed);
    }


    public static OmsiTileWorldBounds
        GetSplineInfluenceBounds(
            double splineWorldX,
            double splineWorldY,
            double splineWorldZ,
            double rotationDegrees,
            double length,
            double radius,
            double gradientStart,
            double gradientEnd,
            double influenceWidth)
    {
        ValidateSplineConformParameters(
            splineWorldX,
            splineWorldY,
            splineWorldZ,
            rotationDegrees,
            length,
            radius,
            gradientStart,
            gradientEnd,
            halfWidth:
                Math.Max(
                    0.001,
                    influenceWidth),
            featherWidth:
                0,
            verticalOffset:
                0);

        if (
            !double.IsFinite(
                influenceWidth) ||
            influenceWidth < 0)
        {
            throw new InvalidDataException(
                "invalidTerrainSplineInfluence");
        }

        var samples =
            BuildSplineSamples(
                splineWorldX,
                splineWorldY,
                splineWorldZ,
                rotationDegrees,
                length,
                radius,
                gradientStart,
                gradientEnd);

        return new OmsiTileWorldBounds(
            samples.Min(
                sample =>
                    sample.X) -
                influenceWidth,
            samples.Min(
                sample =>
                    sample.Z) -
                influenceWidth,
            samples.Max(
                sample =>
                    sample.X) +
                influenceWidth,
            samples.Max(
                sample =>
                    sample.Z) +
                influenceWidth);
    }

    public static OmsiTerrainLevelResult
        ConformToSpline(
            OmsiTerrainGrid terrain,
            double tileOriginX,
            double tileOriginZ,
            double splineWorldX,
            double splineWorldY,
            double splineWorldZ,
            double rotationDegrees,
            double length,
            double radius,
            double gradientStart,
            double gradientEnd,
            double halfWidth,
            double featherWidth,
            double verticalOffset)
    {
        ArgumentNullException.ThrowIfNull(
            terrain);

        ValidateSplineConformParameters(
            splineWorldX,
            splineWorldY,
            splineWorldZ,
            rotationDegrees,
            length,
            radius,
            gradientStart,
            gradientEnd,
            halfWidth,
            featherWidth,
            verticalOffset);

        if (
            !double.IsFinite(
                tileOriginX) ||
            !double.IsFinite(
                tileOriginZ))
        {
            throw new InvalidDataException(
                "invalidTerrainTileOrigin");
        }

        var cellCount =
            terrain.CellCount;

        if (cellCount <= 0)
        {
            throw new InvalidDataException(
                "invalidTerrainCellCount");
        }

        var sampleCount =
            cellCount +
            1;

        if (
            terrain.Heights.Count !=
                sampleCount *
                sampleCount)
        {
            throw new InvalidDataException(
                "invalidTerrainHeightCount");
        }

        var path =
            BuildSplineSamples(
                splineWorldX,
                splineWorldY,
                splineWorldZ,
                rotationDegrees,
                length,
                radius,
                gradientStart,
                gradientEnd);

        var spacing =
            TileSize /
            cellCount;

        var outerWidth =
            halfWidth +
            featherWidth;

        var next =
            terrain.Heights
                .ToArray();

        var changed =
            0;

        for (
            var row = 0;
            row < sampleCount;
            row++)
        {
            var worldZ =
                tileOriginZ +
                row *
                spacing;

            for (
                var column = 0;
                column < sampleCount;
                column++)
            {
                var worldX =
                    tileOriginX +
                    column *
                    spacing;

                var bestDistanceSquared =
                    double.PositiveInfinity;

                var targetHeight =
                    0.0;

                for (
                    var index = 0;
                    index <
                        path.Count -
                        1;
                    index++)
                {
                    var start =
                        path[index];

                    var end =
                        path[
                            index +
                            1];

                    var dx =
                        end.X -
                        start.X;

                    var dz =
                        end.Z -
                        start.Z;

                    var lengthSquared =
                        dx *
                        dx +
                        dz *
                        dz;

                    var amount =
                        lengthSquared <=
                            0.0000001
                            ? 0.0
                            : Math.Clamp(
                                (
                                    (
                                        worldX -
                                        start.X
                                    ) *
                                    dx +
                                    (
                                        worldZ -
                                        start.Z
                                    ) *
                                    dz
                                ) /
                                lengthSquared,
                                0,
                                1);

                    var closestX =
                        start.X +
                        dx *
                        amount;

                    var closestZ =
                        start.Z +
                        dz *
                        amount;

                    var deltaX =
                        worldX -
                        closestX;

                    var deltaZ =
                        worldZ -
                        closestZ;

                    var distanceSquared =
                        deltaX *
                        deltaX +
                        deltaZ *
                        deltaZ;

                    if (
                        distanceSquared >=
                        bestDistanceSquared)
                    {
                        continue;
                    }

                    bestDistanceSquared =
                        distanceSquared;

                    targetHeight =
                        start.Y +
                        (
                            end.Y -
                            start.Y
                        ) *
                        amount;
                }

                var distance =
                    Math.Sqrt(
                        bestDistanceSquared);

                if (
                    distance >
                    outerWidth)
                {
                    continue;
                }

                var weight =
                    distance <=
                        halfWidth ||
                    featherWidth <=
                        0
                        ? 1.0
                        : Math.Clamp(
                            1.0 -
                            (
                                distance -
                                halfWidth
                            ) /
                            featherWidth,
                            0,
                            1);

                var terrainIndex =
                    row *
                    sampleCount +
                    column;

                var current =
                    next[
                        terrainIndex];

                var desired =
                    targetHeight +
                    verticalOffset;

                var updated =
                    current +
                    (
                        desired -
                        current
                    ) *
                    weight;

                var asFloat =
                    (float)updated;

                if (
                    Math.Abs(
                        asFloat -
                        current) <=
                    0.00001f)
                {
                    continue;
                }

                next[
                    terrainIndex] =
                    asFloat;

                changed++;
            }
        }

        return new OmsiTerrainLevelResult(
            new OmsiTerrainGrid(
                terrain.CellCount,
                next),
            changed);
    }


    public static OmsiTerrainLevelResult
        LevelPolygon(
            OmsiTerrainGrid terrain,
            double tileOriginX,
            double tileOriginZ,
            IReadOnlyList<Vector2> worldPolygon,
            double targetHeight,
            double edgeFeatherMeters)
    {
        if (!double.IsFinite(targetHeight))
        {
            throw new InvalidDataException(
                "invalidTerrainPolygonTargetHeight");
        }

        return ApplyPolygon(
            terrain,
            tileOriginX,
            tileOriginZ,
            worldPolygon,
            edgeFeatherMeters,
            targetHeight,
            deltaHeight:
                null);
    }

    public static OmsiTerrainLevelResult
        OffsetPolygon(
            OmsiTerrainGrid terrain,
            double tileOriginX,
            double tileOriginZ,
            IReadOnlyList<Vector2> worldPolygon,
            double deltaHeight,
            double edgeFeatherMeters)
    {
        if (
            !double.IsFinite(deltaHeight) ||
            Math.Abs(deltaHeight) <
                0.000001)
        {
            throw new InvalidDataException(
                "invalidTerrainPolygonDeltaHeight");
        }

        return ApplyPolygon(
            terrain,
            tileOriginX,
            tileOriginZ,
            worldPolygon,
            edgeFeatherMeters,
            targetHeight:
                null,
            deltaHeight);
    }

    private static OmsiTerrainLevelResult
        ApplyPolygon(
            OmsiTerrainGrid terrain,
            double tileOriginX,
            double tileOriginZ,
            IReadOnlyList<Vector2> worldPolygon,
            double edgeFeatherMeters,
            double? targetHeight,
            double? deltaHeight)
    {
        ArgumentNullException.ThrowIfNull(
            terrain);

        ArgumentNullException.ThrowIfNull(
            worldPolygon);

        if (
            worldPolygon.Count < 3 ||
            worldPolygon.Any(
                point =>
                    !float.IsFinite(point.X) ||
                    !float.IsFinite(point.Y)) ||
            !double.IsFinite(tileOriginX) ||
            !double.IsFinite(tileOriginZ) ||
            !double.IsFinite(edgeFeatherMeters) ||
            edgeFeatherMeters < 0 ||
            (targetHeight is null) ==
                (deltaHeight is null))
        {
            throw new InvalidDataException(
                "invalidTerrainPolygon");
        }

        var cellCount =
            terrain.CellCount;

        if (cellCount <= 0)
        {
            throw new InvalidDataException(
                "invalidTerrainCellCount");
        }

        var sampleCount =
            cellCount +
            1;

        if (
            terrain.Heights.Count !=
                sampleCount *
                sampleCount)
        {
            throw new InvalidDataException(
                "invalidTerrainHeightCount");
        }

        var spacing =
            TileSize /
            cellCount;

        var next =
            terrain.Heights
                .ToArray();

        var changed =
            0;

        for (
            var row = 0;
            row < sampleCount;
            row++)
        {
            var worldZ =
                tileOriginZ +
                row *
                spacing;

            for (
                var column = 0;
                column < sampleCount;
                column++)
            {
                var worldX =
                    tileOriginX +
                    column *
                    spacing;

                var point =
                    new Vector2(
                        (float)worldX,
                        (float)worldZ);

                if (!ContainsPolygonPoint(
                        worldPolygon,
                        point))
                {
                    continue;
                }

                var weight =
                    edgeFeatherMeters <=
                        0
                        ? 1.0
                        : Math.Clamp(
                            DistanceToPolygonBoundary(
                                worldPolygon,
                                point) /
                            edgeFeatherMeters,
                            0,
                            1);

                var index =
                    row *
                    sampleCount +
                    column;

                var current =
                    next[index];

                var updated =
                    targetHeight is
                        double level
                        ? current +
                            (
                                level -
                                current
                            ) *
                            weight
                        : current +
                            deltaHeight!.Value *
                            weight;

                var asFloat =
                    (float)updated;

                if (
                    Math.Abs(
                        asFloat -
                        current) <=
                    0.00001f)
                {
                    continue;
                }

                next[index] =
                    asFloat;

                changed++;
            }
        }

        return new OmsiTerrainLevelResult(
            new OmsiTerrainGrid(
                terrain.CellCount,
                next),
            changed);
    }

    private static bool ContainsPolygonPoint(
        IReadOnlyList<Vector2> polygon,
        Vector2 point)
    {
        var inside =
            false;

        for (
            int current = 0,
                previous =
                    polygon.Count -
                    1;
            current < polygon.Count;
            previous = current++)
        {
            var a =
                polygon[current];

            var b =
                polygon[previous];

            if (
                DistanceToPolygonSegment(
                    point,
                    a,
                    b) <=
                0.0001f)
            {
                return true;
            }

            var intersects =
                (
                    a.Y >
                    point.Y
                ) !=
                (
                    b.Y >
                    point.Y
                ) &&
                point.X <
                    (
                        b.X -
                        a.X
                    ) *
                    (
                        point.Y -
                        a.Y
                    ) /
                    (
                        b.Y -
                        a.Y
                    ) +
                    a.X;

            if (intersects)
            {
                inside =
                    !inside;
            }
        }

        return inside;
    }

    private static double DistanceToPolygonBoundary(
        IReadOnlyList<Vector2> polygon,
        Vector2 point)
    {
        var best =
            double.PositiveInfinity;

        for (
            int current = 0,
                previous =
                    polygon.Count -
                    1;
            current < polygon.Count;
            previous = current++)
        {
            best =
                Math.Min(
                    best,
                    DistanceToPolygonSegment(
                        point,
                        polygon[previous],
                        polygon[current]));
        }

        return best;
    }

    private static float DistanceToPolygonSegment(
        Vector2 point,
        Vector2 start,
        Vector2 end)
    {
        var segment =
            end -
            start;

        var lengthSquared =
            segment.LengthSquared();

        if (
            lengthSquared <=
            0.0000001f)
        {
            return Vector2.Distance(
                point,
                start);
        }

        var amount =
            Math.Clamp(
                Vector2.Dot(
                    point -
                    start,
                    segment) /
                lengthSquared,
                0,
                1);

        return Vector2.Distance(
            point,
            start +
            segment *
            amount);
    }


    public static OmsiTerrainSplinePreviewBand
        BuildSplinePreviewBand(
            double splineWorldX,
            double splineWorldY,
            double splineWorldZ,
            double rotationDegrees,
            double length,
            double radius,
            double gradientStart,
            double gradientEnd,
            double halfWidth,
            double featherWidth,
            double verticalOffset)
    {
        ValidateSplineConformParameters(
            splineWorldX,
            splineWorldY,
            splineWorldZ,
            rotationDegrees,
            length,
            radius,
            gradientStart,
            gradientEnd,
            halfWidth,
            featherWidth,
            verticalOffset);

        var samples =
            BuildSplineSamples(
                splineWorldX,
                splineWorldY,
                splineWorldZ,
                rotationDegrees,
                length,
                radius,
                gradientStart,
                gradientEnd,
                sampleSpacingMeters:
                    8.0,
                maximumSegments:
                    96);

        var centerline = new Vector3[samples.Count];
        var innerLeft = new Vector3[samples.Count];
        var innerRight = new Vector3[samples.Count];
        var outerLeft = new Vector3[samples.Count];
        var outerRight = new Vector3[samples.Count];
        var outerWidth = halfWidth + featherWidth;

        for (var index = 0; index < samples.Count; index++)
        {
            var sample = samples[index];
            var previous = samples[Math.Max(0, index - 1)];
            var next = samples[Math.Min(samples.Count - 1, index + 1)];
            var dx = next.X - previous.X;
            var dz = next.Z - previous.Z;
            var tangentLength = Math.Sqrt(dx * dx + dz * dz);

            if (tangentLength <= 0.000001)
            {
                var yaw = rotationDegrees * Math.PI / 180.0;
                dx = Math.Sin(yaw);
                dz = Math.Cos(yaw);
                tangentLength = 1.0;
            }

            var lateralX = dz / tangentLength;
            var lateralZ = -dx / tangentLength;
            var center =
                new Vector3(
                    (float)sample.X,
                    (float)(sample.Y + verticalOffset),
                    (float)sample.Z);

            centerline[index] = center;
            innerLeft[index] =
                new Vector3(
                    (float)(sample.X + lateralX * halfWidth),
                    center.Y,
                    (float)(sample.Z + lateralZ * halfWidth));
            innerRight[index] =
                new Vector3(
                    (float)(sample.X - lateralX * halfWidth),
                    center.Y,
                    (float)(sample.Z - lateralZ * halfWidth));
            outerLeft[index] =
                new Vector3(
                    (float)(sample.X + lateralX * outerWidth),
                    center.Y,
                    (float)(sample.Z + lateralZ * outerWidth));
            outerRight[index] =
                new Vector3(
                    (float)(sample.X - lateralX * outerWidth),
                    center.Y,
                    (float)(sample.Z - lateralZ * outerWidth));
        }

        return new OmsiTerrainSplinePreviewBand(
            centerline,
            innerLeft,
            innerRight,
            outerLeft,
            outerRight);
    }

    private readonly record struct
        TerrainSplineSample(
            double X,
            double Y,
            double Z);

    private static IReadOnlyList<
        TerrainSplineSample>
        BuildSplineSamples(
            double splineWorldX,
            double splineWorldY,
            double splineWorldZ,
            double rotationDegrees,
            double length,
            double radius,
            double gradientStart,
            double gradientEnd,
            double sampleSpacingMeters =
                1.0,
            int maximumSegments =
                2048)
    {
        if (
            !double.IsFinite(sampleSpacingMeters) ||
            sampleSpacingMeters <= 0 ||
            maximumSegments <= 0)
        {
            throw new InvalidDataException(
                "invalidTerrainSplineSampling");
        }

        var segmentCount =
            Math.Clamp(
                (int)Math.Ceiling(
                    length /
                    sampleSpacingMeters),
                1,
                maximumSegments);

        var samples =
            new TerrainSplineSample[
                segmentCount +
                1];

        var yaw =
            rotationDegrees *
            Math.PI /
            180.0;

        var cosYaw =
            Math.Cos(
                yaw);

        var sinYaw =
            Math.Sin(
                yaw);

        var curved =
            Math.Abs(
                radius) >
            0.001;

        for (
            var index = 0;
            index <=
                segmentCount;
            index++)
        {
            var distance =
                length *
                index /
                segmentCount;

            var curveAngle =
                curved
                    ? distance /
                        radius
                    : 0.0;

            var localX =
                curved
                    ? radius *
                        (
                            1.0 -
                            Math.Cos(
                                curveAngle)
                        )
                    : 0.0;

            var localZ =
                curved
                    ? radius *
                        Math.Sin(
                            curveAngle)
                    : distance;

            var worldX =
                splineWorldX +
                localX *
                    cosYaw +
                localZ *
                    sinYaw;

            var worldZ =
                splineWorldZ -
                localX *
                    sinYaw +
                localZ *
                    cosYaw;

            var worldY =
                splineWorldY +
                GetSplineGradientRise(
                    gradientStart,
                    gradientEnd,
                    length,
                    distance);

            samples[index] =
                new TerrainSplineSample(
                    worldX,
                    worldY,
                    worldZ);
        }

        return samples;
    }

    private static double
        GetSplineGradientRise(
            double gradientStart,
            double gradientEnd,
            double length,
            double distance)
    {
        var startSlope =
            gradientStart /
            100.0;

        var slopeDelta =
            (
                gradientEnd -
                gradientStart
            ) /
            100.0;

        return
            startSlope *
            distance +
            0.5 *
            slopeDelta *
            distance *
            distance /
            length;
    }

    private static void
        ValidateSplineConformParameters(
            double splineWorldX,
            double splineWorldY,
            double splineWorldZ,
            double rotationDegrees,
            double length,
            double radius,
            double gradientStart,
            double gradientEnd,
            double halfWidth,
            double featherWidth,
            double verticalOffset)
    {
        if (
            !double.IsFinite(
                splineWorldX) ||
            !double.IsFinite(
                splineWorldY) ||
            !double.IsFinite(
                splineWorldZ) ||
            !double.IsFinite(
                rotationDegrees) ||
            !double.IsFinite(
                length) ||
            length <= 0 ||
            !double.IsFinite(
                radius) ||
            !double.IsFinite(
                gradientStart) ||
            !double.IsFinite(
                gradientEnd) ||
            !double.IsFinite(
                halfWidth) ||
            halfWidth <= 0 ||
            !double.IsFinite(
                featherWidth) ||
            featherWidth < 0 ||
            !double.IsFinite(
                verticalOffset))
        {
            throw new InvalidDataException(
                "invalidTerrainSplineConform");
        }
    }

    public static OmsiTerrainLevelResult
        ApplyElevationGrid(
            OmsiTerrainGrid terrain,
            int rows,
            int columns,
            IReadOnlyList<double> elevations,
            double verticalOffset)
    {
        ArgumentNullException.ThrowIfNull(
            terrain);
        ArgumentNullException.ThrowIfNull(
            elevations);

        if (
            rows < 2 ||
            columns < 2 ||
            rows > 257 ||
            columns > 257 ||
            elevations.Count !=
                rows * columns ||
            !double.IsFinite(
                verticalOffset) ||
            elevations.Any(
                elevation =>
                    !double.IsFinite(
                        elevation)))
        {
            throw new InvalidDataException(
                "invalidElevationGrid");
        }

        var terrainSampleCount =
            terrain.CellCount + 1;

        if (
            terrain.CellCount <= 0 ||
            terrain.Heights.Count !=
                terrainSampleCount *
                terrainSampleCount)
        {
            throw new InvalidDataException(
                "invalidTerrainHeightCount");
        }

        var next =
            new float[
                terrain.Heights.Count];

        var changed = 0;

        for (
            var row = 0;
            row < terrainSampleCount;
            row++)
        {
            var sourceY =
                terrainSampleCount == 1
                    ? 0
                    : (double)row /
                      (terrainSampleCount - 1) *
                      (rows - 1);

            var y0 =
                Math.Clamp(
                    (int)Math.Floor(
                        sourceY),
                    0,
                    rows - 1);

            var y1 =
                Math.Min(
                    rows - 1,
                    y0 + 1);

            var fy =
                sourceY - y0;

            for (
                var column = 0;
                column < terrainSampleCount;
                column++)
            {
                var sourceX =
                    terrainSampleCount == 1
                        ? 0
                        : (double)column /
                          (terrainSampleCount - 1) *
                          (columns - 1);

                var x0 =
                    Math.Clamp(
                        (int)Math.Floor(
                            sourceX),
                        0,
                        columns - 1);

                var x1 =
                    Math.Min(
                        columns - 1,
                        x0 + 1);

                var fx =
                    sourceX - x0;

                var topLeft =
                    elevations[
                        y0 *
                        columns +
                        x0];

                var topRight =
                    elevations[
                        y0 *
                        columns +
                        x1];

                var bottomLeft =
                    elevations[
                        y1 *
                        columns +
                        x0];

                var bottomRight =
                    elevations[
                        y1 *
                        columns +
                        x1];

                var top =
                    topLeft +
                    (
                        topRight -
                        topLeft
                    ) *
                    fx;

                var bottom =
                    bottomLeft +
                    (
                        bottomRight -
                        bottomLeft
                    ) *
                    fx;

                var sampled =
                    top +
                    (
                        bottom -
                        top
                    ) *
                    fy +
                    verticalOffset;

                var asFloat =
                    (float)sampled;

                var index =
                    row *
                    terrainSampleCount +
                    column;

                next[index] =
                    asFloat;

                if (
                    Math.Abs(
                        asFloat -
                        terrain.Heights[index]) >
                    0.00001f)
                {
                    changed++;
                }
            }
        }

        return new OmsiTerrainLevelResult(
            new OmsiTerrainGrid(
                terrain.CellCount,
                next),
            changed);
    }
}
