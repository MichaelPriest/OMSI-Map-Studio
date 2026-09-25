namespace MapStudio.Core.Generation.Roads;

public readonly record struct MapStudioRoadPoint(
    double X,
    double Z)
{
    public double DistanceTo(
        MapStudioRoadPoint other)
    {
        var dx =
            other.X - X;

        var dz =
            other.Z - Z;

        return Math.Sqrt(
            dx * dx +
            dz * dz);
    }

    public static MapStudioRoadPoint Lerp(
        MapStudioRoadPoint from,
        MapStudioRoadPoint to,
        double t) =>
        new(
            from.X +
            (
                to.X -
                from.X
            ) *
            t,
            from.Z +
            (
                to.Z -
                from.Z
            ) *
            t);
}

public sealed record MapStudioRoadTrace(
    string Id,
    IReadOnlyList<MapStudioRoadPoint> Points,
    string ProfileId,
    int? LaneCount = null,
    bool? OneWay = null,
    double? WidthMeters = null,
    int? ForwardLaneCount = null,
    int? BackwardLaneCount = null,
    int? Layer = null,
    bool Bridge = false,
    bool Tunnel = false,
    bool SourceTopologyAuthoritative = false);

public sealed record MapStudioRoadGraphNode(
    int Id,
    MapStudioRoadPoint Position,
    int Degree,
    bool IsJunction,
    IReadOnlySet<string> TraceIds);

public sealed record MapStudioRoadGraphSegment(
    int Id,
    int FromNodeId,
    int ToNodeId,
    string TraceId,
    string ProfileId,
    MapStudioRoadPoint Start,
    MapStudioRoadPoint End,
    double LengthMeters,
    int? LaneCount,
    bool? OneWay,
    double? WidthMeters,
    int? ForwardLaneCount = null,
    int? BackwardLaneCount = null,
    int? Layer = null,
    bool Bridge = false,
    bool Tunnel = false,
    bool SourceTopologyAuthoritative = false);

public sealed record MapStudioRoadJunction(
    int NodeId,
    MapStudioRoadPoint Position,
    int Degree,
    IReadOnlySet<string> TraceIds);

public sealed record MapStudioRoadGraph(
    IReadOnlyList<MapStudioRoadGraphNode> Nodes,
    IReadOnlyList<MapStudioRoadGraphSegment> Segments,
    IReadOnlyList<MapStudioRoadJunction> Junctions);

public sealed class MapStudioRoadGraphBuilder
{
    private const double Epsilon =
        1e-8;

    public MapStudioRoadGraph Build(
        IReadOnlyList<MapStudioRoadTrace> traces,
        double snapToleranceMeters =
            0.25,
        double minimumSegmentLengthMeters =
            0.10)
    {
        ArgumentNullException.ThrowIfNull(
            traces);

        if (
            !double.IsFinite(
                snapToleranceMeters) ||
            snapToleranceMeters <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(snapToleranceMeters));
        }

        if (
            !double.IsFinite(
                minimumSegmentLengthMeters) ||
            minimumSegmentLengthMeters <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(
                    minimumSegmentLengthMeters));
        }

        var rawSegments =
            BuildRawSegments(
                traces,
                minimumSegmentLengthMeters);

        var splitPoints =
            rawSegments
                .Select(
                    segment =>
                        new List<SplitPoint>
                        {
                            new(
                                0,
                                segment.Start),
                            new(
                                1,
                                segment.End)
                        })
                .ToArray();

        for (
            var leftIndex = 0;
            leftIndex <
                rawSegments.Count;
            leftIndex++)
        {
            for (
                var rightIndex =
                    leftIndex + 1;
                rightIndex <
                    rawSegments.Count;
                rightIndex++)
            {
                AddIntersections(
                    rawSegments[
                        leftIndex],
                    rawSegments[
                        rightIndex],
                    splitPoints[
                        leftIndex],
                    splitPoints[
                        rightIndex],
                    snapToleranceMeters);
            }
        }

        var nodeRegistry =
            new NodeRegistry(
                snapToleranceMeters);

        var pendingSegments =
            new List<
                PendingSegment>();

        for (
            var segmentIndex = 0;
            segmentIndex <
                rawSegments.Count;
            segmentIndex++)
        {
            var raw =
                rawSegments[
                    segmentIndex];

            var ordered =
                NormalizeSplits(
                    splitPoints[
                        segmentIndex],
                    raw,
                    snapToleranceMeters);

            for (
                var splitIndex = 0;
                splitIndex <
                    ordered.Count - 1;
                splitIndex++)
            {
                var start =
                    ordered[
                        splitIndex];

                var end =
                    ordered[
                        splitIndex + 1];

                var length =
                    start.Point
                        .DistanceTo(
                            end.Point);

                if (
                    length <
                    minimumSegmentLengthMeters)
                {
                    continue;
                }

                var fromNode =
                    nodeRegistry
                        .GetOrCreate(
                            start.Point,
                            raw.Trace.Id);

                var toNode =
                    nodeRegistry
                        .GetOrCreate(
                            end.Point,
                            raw.Trace.Id);

                if (
                    fromNode ==
                    toNode)
                {
                    continue;
                }

                pendingSegments.Add(
                    new PendingSegment(
                        fromNode,
                        toNode,
                        raw.Trace,
                        start.Point,
                        end.Point,
                        length));
            }
        }

        var degree =
            new Dictionary<int, int>();

        foreach (
            var segment in
                pendingSegments)
        {
            degree[segment.FromNodeId] =
                degree.GetValueOrDefault(
                    segment.FromNodeId) +
                1;

            degree[segment.ToNodeId] =
                degree.GetValueOrDefault(
                    segment.ToNodeId) +
                1;
        }

        var nodes =
            nodeRegistry.Nodes
                .Select(
                    node =>
                    {
                        var nodeDegree =
                            degree.GetValueOrDefault(
                                node.Id);

                        var isJunction =
                            nodeDegree >=
                                3;

                        return new MapStudioRoadGraphNode(
                            node.Id,
                            node.Position,
                            nodeDegree,
                            isJunction,
                            new HashSet<string>(
                                node.TraceIds,
                                StringComparer
                                    .OrdinalIgnoreCase));
                    })
                .OrderBy(
                    node =>
                        node.Id)
                .ToArray();

        var segments =
            pendingSegments
                .Select(
                    (segment, index) =>
                        new MapStudioRoadGraphSegment(
                            index + 1,
                            segment
                                .FromNodeId,
                            segment
                                .ToNodeId,
                            segment
                                .Trace
                                .Id,
                            segment
                                .Trace
                                .ProfileId,
                            segment.Start,
                            segment.End,
                            segment.Length,
                            segment
                                .Trace
                                .LaneCount,
                            segment
                                .Trace
                                .OneWay,
                            segment
                                .Trace
                                .WidthMeters,
                            segment
                                .Trace
                                .ForwardLaneCount,
                            segment
                                .Trace
                                .BackwardLaneCount,
                            segment
                                .Trace
                                .Layer,
                            segment
                                .Trace
                                .Bridge,
                            segment
                                .Trace
                                .Tunnel,
                            segment
                                .Trace
                                .SourceTopologyAuthoritative))
                .ToArray();

        var junctions =
            nodes
                .Where(
                    node =>
                        node.IsJunction)
                .Select(
                    node =>
                        new MapStudioRoadJunction(
                            node.Id,
                            node.Position,
                            node.Degree,
                            node.TraceIds))
                .ToArray();

        return new MapStudioRoadGraph(
            nodes,
            segments,
            junctions);
    }

    private static List<RawSegment>
        BuildRawSegments(
            IReadOnlyList<
                MapStudioRoadTrace>
                traces,
            double minimumLength)
    {
        var result =
            new List<RawSegment>();

        var ids =
            new HashSet<string>(
                StringComparer
                    .OrdinalIgnoreCase);

        foreach (
            var trace in traces)
        {
            ArgumentNullException.ThrowIfNull(
                trace);

            if (
                string.IsNullOrWhiteSpace(
                    trace.Id))
            {
                throw new InvalidDataException(
                    "roadTraceIdRequired");
            }

            if (
                !ids.Add(
                    trace.Id))
            {
                throw new InvalidDataException(
                    "duplicateRoadTraceId");
            }

            if (
                string.IsNullOrWhiteSpace(
                    trace.ProfileId))
            {
                throw new InvalidDataException(
                    "roadProfileIdRequired");
            }

            if (
                trace.Points is null ||
                trace.Points.Count <
                    2)
            {
                continue;
            }

            for (
                var pointIndex = 0;
                pointIndex <
                    trace.Points.Count -
                    1;
                pointIndex++)
            {
                var start =
                    trace.Points[
                        pointIndex];

                var end =
                    trace.Points[
                        pointIndex + 1];

                if (
                    !IsFinite(start) ||
                    !IsFinite(end) ||
                    start.DistanceTo(
                        end) <
                    minimumLength)
                {
                    continue;
                }

                result.Add(
                    new RawSegment(
                        result.Count,
                        trace,
                        pointIndex,
                        start,
                        end));
            }
        }

        return result;
    }

    private static void AddIntersections(
        RawSegment left,
        RawSegment right,
        ICollection<SplitPoint>
            leftSplits,
        ICollection<SplitPoint>
            rightSplits,
        double tolerance)
    {
        if (
            CanCreateInteriorIntersection(
                left,
                right) &&
            TryIntersectSegments(
                left.Start,
                left.End,
                right.Start,
                right.End,
                out var leftT,
                out var rightT,
                out var intersection))
        {
            leftSplits.Add(
                new SplitPoint(
                    Clamp01(leftT),
                    intersection));

            rightSplits.Add(
                new SplitPoint(
                    Clamp01(rightT),
                    intersection));
        }

        AddEndpointProjection(
            left.Start,
            right,
            rightSplits,
            leftSplits,
            isSourceStart: true,
            tolerance);

        AddEndpointProjection(
            left.End,
            right,
            rightSplits,
            leftSplits,
            isSourceStart: false,
            tolerance);

        AddEndpointProjection(
            right.Start,
            left,
            leftSplits,
            rightSplits,
            isSourceStart: true,
            tolerance);

        AddEndpointProjection(
            right.End,
            left,
            leftSplits,
            rightSplits,
            isSourceStart: false,
            tolerance);
    }

    private static bool CanCreateInteriorIntersection(
        RawSegment left,
        RawSegment right)
    {
        if (
            string.Equals(
                left.Trace.Id,
                right.Trace.Id,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (
            left.Trace.SourceTopologyAuthoritative ||
            right.Trace.SourceTopologyAuthoritative)
        {
            return false;
        }

        var leftLayer =
            left.Trace.Layer ??
            0;

        var rightLayer =
            right.Trace.Layer ??
            0;

        if (leftLayer != rightLayer)
        {
            return false;
        }

        if (
            left.Trace.Bridge !=
                right.Trace.Bridge ||
            left.Trace.Tunnel !=
                right.Trace.Tunnel)
        {
            return false;
        }

        return true;
    }

    private static void AddEndpointProjection(
        MapStudioRoadPoint sourcePoint,
        RawSegment target,
        ICollection<SplitPoint>
            targetSplits,
        ICollection<SplitPoint>
            sourceSplits,
        bool isSourceStart,
        double tolerance)
    {
        var targetT =
            ProjectParameter(
                sourcePoint,
                target.Start,
                target.End);

        if (
            targetT <
                -Epsilon ||
            targetT >
                1 + Epsilon)
        {
            return;
        }

        targetT =
            Clamp01(
                targetT);

        var projected =
            MapStudioRoadPoint
                .Lerp(
                    target.Start,
                    target.End,
                    targetT);

        if (
            projected.DistanceTo(
                sourcePoint) >
            tolerance)
        {
            return;
        }

        var snapped =
            new MapStudioRoadPoint(
                (
                    projected.X +
                    sourcePoint.X
                ) /
                2.0,
                (
                    projected.Z +
                    sourcePoint.Z
                ) /
                2.0);

        targetSplits.Add(
            new SplitPoint(
                targetT,
                snapped));

        sourceSplits.Add(
            new SplitPoint(
                isSourceStart
                    ? 0
                    : 1,
                snapped));
    }

    private static IReadOnlyList<SplitPoint>
        NormalizeSplits(
            IEnumerable<SplitPoint> source,
            RawSegment segment,
            double tolerance)
    {
        var parameterTolerance =
            tolerance /
            Math.Max(
                tolerance,
                segment.Start
                    .DistanceTo(
                        segment.End));

        var ordered =
            source
                .OrderBy(
                    split =>
                        split.T)
                .ToArray();

        var result =
            new List<SplitPoint>();

        foreach (
            var split in ordered)
        {
            if (
                result.Count == 0)
            {
                result.Add(
                    split);

                continue;
            }

            var previous =
                result[^1];

            if (
                Math.Abs(
                    split.T -
                    previous.T) <=
                    parameterTolerance ||
                split.Point
                    .DistanceTo(
                        previous.Point) <=
                    tolerance)
            {
                var mergedT =
                    (
                        previous.T +
                        split.T
                    ) /
                    2.0;

                result[^1] =
                    new SplitPoint(
                        Clamp01(
                            mergedT),
                        MapStudioRoadPoint
                            .Lerp(
                                segment.Start,
                                segment.End,
                                Clamp01(
                                    mergedT)));

                continue;
            }

            result.Add(
                split);
        }

        return result;
    }

    private static bool TryIntersectSegments(
        MapStudioRoadPoint a,
        MapStudioRoadPoint b,
        MapStudioRoadPoint c,
        MapStudioRoadPoint d,
        out double aT,
        out double cT,
        out MapStudioRoadPoint
            intersection)
    {
        aT = 0;
        cT = 0;
        intersection =
            default;

        var rX =
            b.X -
            a.X;

        var rZ =
            b.Z -
            a.Z;

        var sX =
            d.X -
            c.X;

        var sZ =
            d.Z -
            c.Z;

        var denominator =
            Cross(
                rX,
                rZ,
                sX,
                sZ);

        if (
            Math.Abs(
                denominator) <
            Epsilon)
        {
            return false;
        }

        var cax =
            c.X -
            a.X;

        var caz =
            c.Z -
            a.Z;

        aT =
            Cross(
                cax,
                caz,
                sX,
                sZ) /
            denominator;

        cT =
            Cross(
                cax,
                caz,
                rX,
                rZ) /
            denominator;

        if (
            aT <
                -Epsilon ||
            aT >
                1 + Epsilon ||
            cT <
                -Epsilon ||
            cT >
                1 + Epsilon)
        {
            return false;
        }

        aT =
            Clamp01(aT);

        cT =
            Clamp01(cT);

        intersection =
            MapStudioRoadPoint
                .Lerp(
                    a,
                    b,
                    aT);

        return true;
    }

    private static double ProjectParameter(
        MapStudioRoadPoint point,
        MapStudioRoadPoint start,
        MapStudioRoadPoint end)
    {
        var dx =
            end.X -
            start.X;

        var dz =
            end.Z -
            start.Z;

        var lengthSquared =
            dx * dx +
            dz * dz;

        if (
            lengthSquared <
            Epsilon)
        {
            return 0;
        }

        return
            (
                (
                    point.X -
                    start.X
                ) *
                dx +
                (
                    point.Z -
                    start.Z
                ) *
                dz
            ) /
            lengthSquared;
    }

    private static double Cross(
        double ax,
        double az,
        double bx,
        double bz) =>
        ax * bz -
        az * bx;

    private static double Clamp01(
        double value) =>
        Math.Clamp(
            value,
            0,
            1);

    private static bool IsFinite(
        MapStudioRoadPoint point) =>
        double.IsFinite(
            point.X) &&
        double.IsFinite(
            point.Z);

    private sealed record RawSegment(
        int Id,
        MapStudioRoadTrace Trace,
        int TraceSegmentIndex,
        MapStudioRoadPoint Start,
        MapStudioRoadPoint End);

    private sealed record SplitPoint(
        double T,
        MapStudioRoadPoint Point);

    private sealed record PendingSegment(
        int FromNodeId,
        int ToNodeId,
        MapStudioRoadTrace Trace,
        MapStudioRoadPoint Start,
        MapStudioRoadPoint End,
        double Length);

    private sealed class NodeRegistry(
        double tolerance)
    {
        private readonly List<
            MutableNode>
            _nodes = [];

        public IReadOnlyList<MutableNode>
            Nodes =>
            _nodes;

        public int GetOrCreate(
            MapStudioRoadPoint point,
            string traceId)
        {
            foreach (
                var node in
                    _nodes)
            {
                if (
                    node.Position
                        .DistanceTo(
                            point) <=
                    tolerance)
                {
                    node.TraceIds.Add(
                        traceId);

                    return node.Id;
                }
            }

            var created =
                new MutableNode(
                    _nodes.Count + 1,
                    point,
                    new HashSet<string>(
                        StringComparer
                            .OrdinalIgnoreCase)
                    {
                        traceId
                    });

            _nodes.Add(
                created);

            return created.Id;
        }
    }

    private sealed record MutableNode(
        int Id,
        MapStudioRoadPoint Position,
        HashSet<string> TraceIds);
}
