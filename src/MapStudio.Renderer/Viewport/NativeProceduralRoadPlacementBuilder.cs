using System.Numerics;
using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Omsi.Junctions;
using MapStudio.Core.Omsi.Splines;
using MapStudio.Renderer.Scene;

namespace MapStudio.Renderer.Viewport;

public sealed record NativeProceduralRoadPlacementLink(
    int PreviousRequestIndex,
    int NextRequestIndex);

public sealed record NativeProceduralRoadPlacementBuildResult(
    IReadOnlyList<NativeSplinePlacementRequest> Requests,
    IReadOnlyList<NativeProceduralRoadPlacementLink> Links,
    int SkippedSegments)
{
    public int CurvedRequestCount =>
        Requests.Count(
            request =>
                request.IsCurved);
}

public sealed class NativeProceduralRoadPlacementBuilder
{
    private const double MinimumCurveSweepDegrees =
        1.5;

    private const double MaximumCurveSweepDegrees =
        110.0;

    public NativeProceduralRoadPlacementBuildResult Build(
        NativeSceneSnapshot scene,
        MapStudioRoadGraph graph)
    {
        ArgumentNullException.ThrowIfNull(
            scene);

        ArgumentNullException.ThrowIfNull(
            graph);

        var nodeById =
            graph.Nodes.ToDictionary(
                node =>
                    node.Id);

        var segmentsByNode =
            graph.Segments
                .SelectMany(
                    segment =>
                        new[]
                        {
                            (
                                NodeId:
                                    segment.FromNodeId,
                                Segment:
                                    segment
                            ),
                            (
                                NodeId:
                                    segment.ToNodeId,
                                Segment:
                                    segment
                            )
                        })
                .GroupBy(
                    item =>
                        item.NodeId)
                .ToDictionary(
                    group =>
                        group.Key,
                    group =>
                        group
                            .Select(
                                item =>
                                    item.Segment)
                            .ToArray());

        var bindings =
            new List<RequestBinding>(
                graph.Segments.Count);

        var skipped =
            0;

        foreach (
            var traceGroup in
                graph.Segments
                    .GroupBy(
                        segment =>
                            segment.TraceId,
                        StringComparer
                            .OrdinalIgnoreCase))
        {
            var ordered =
                traceGroup
                    .OrderBy(
                        segment =>
                            segment.Id)
                    .ToArray();

            for (
                var index = 0;
                index < ordered.Length;)
            {
                var current =
                    ordered[index];

                if (
                    index + 1 <
                        ordered.Length)
                {
                    var next =
                        ordered[
                            index +
                                1];

                    if (
                        CanMergeAsCurve(
                            current,
                            next,
                            nodeById) &&
                        TryBuildCurveRequest(
                            scene,
                            current,
                            next,
                            nodeById,
                            segmentsByNode,
                            out var curvedRequest))
                    {
                        bindings.Add(
                            new RequestBinding(
                                curvedRequest!,
                                current.FromNodeId,
                                next.ToNodeId,
                                current.TraceId));

                        index +=
                            2;

                        continue;
                    }
                }

                if (
                    TryBuildStraightRequest(
                        scene,
                        current,
                        nodeById,
                        segmentsByNode,
                        out var straightRequest))
                {
                    bindings.Add(
                        new RequestBinding(
                            straightRequest!,
                            current.FromNodeId,
                            current.ToNodeId,
                            current.TraceId));
                }
                else
                {
                    skipped++;
                }

                index++;
            }
        }

        var requests =
            bindings
                .Select(
                    binding =>
                        binding.Request)
                .ToArray();

        var links =
            BuildLinks(
                bindings,
                nodeById);

        return new NativeProceduralRoadPlacementBuildResult(
            requests,
            links,
            skipped);
    }

    private static bool CanMergeAsCurve(
        MapStudioRoadGraphSegment first,
        MapStudioRoadGraphSegment second,
        IReadOnlyDictionary<
            int,
            MapStudioRoadGraphNode>
            nodeById)
    {
        if (
            first.ToNodeId !=
                second.FromNodeId ||
            !string.Equals(
                first.ProfileId,
                second.ProfileId,
                StringComparison
                    .OrdinalIgnoreCase) ||
            !nodeById.TryGetValue(
                first.ToNodeId,
                out var shared) ||
            shared.IsJunction ||
            shared.Degree !=
                2)
        {
            return false;
        }

        return true;
    }

    private static bool TryBuildCurveRequest(
        NativeSceneSnapshot scene,
        MapStudioRoadGraphSegment first,
        MapStudioRoadGraphSegment second,
        IReadOnlyDictionary<
            int,
            MapStudioRoadGraphNode>
            nodeById,
        IReadOnlyDictionary<
            int,
            MapStudioRoadGraphSegment[]>
            segmentsByNode,
        out NativeSplinePlacementRequest?
            request)
    {
        request =
            null;

        var firstPoints =
            ResolveEffectiveSegmentPoints(
                first,
                nodeById,
                segmentsByNode);

        var secondPoints =
            ResolveEffectiveSegmentPoints(
                second,
                nodeById,
                segmentsByNode);

        var startPoint =
            firstPoints.Start;

        var controlPoint =
            first.End;

        var endPoint =
            secondPoints.End;

        if (
            startPoint.DistanceTo(
                controlPoint) <
                0.25 ||
            controlPoint.DistanceTo(
                endPoint) <
                0.25)
        {
            return false;
        }

        if (
            !TryGetWorldPoint(
                scene,
                startPoint,
                out var start) ||
            !TryGetWorldPoint(
                scene,
                controlPoint,
                out var control) ||
            !TryGetWorldPoint(
                scene,
                endPoint,
                out var end) ||
            !NativeSplinePlacementMath
                .TryCreateArc(
                    start,
                    end,
                    control,
                    out var shape) ||
            shape is null ||
            !shape.IsCurved ||
            Math.Abs(
                shape.Radius) <
                ResolveMinimumCurveRadius(
                    first,
                    second))
        {
            return false;
        }

        var sweepDegrees =
            Math.Abs(
                shape.Length /
                shape.Radius) *
            180.0 /
            Math.PI;

        if (
            !double.IsFinite(
                sweepDegrees) ||
            sweepDegrees <
                MinimumCurveSweepDegrees ||
            sweepDegrees >
                MaximumCurveSweepDegrees)
        {
            return false;
        }

        return TryCreateRequest(
            scene,
            first.ProfileId,
            shape,
            out request);
    }

    private static bool TryBuildStraightRequest(
        NativeSceneSnapshot scene,
        MapStudioRoadGraphSegment segment,
        IReadOnlyDictionary<
            int,
            MapStudioRoadGraphNode>
            nodeById,
        IReadOnlyDictionary<
            int,
            MapStudioRoadGraphSegment[]>
            segmentsByNode,
        out NativeSplinePlacementRequest?
            request)
    {
        request =
            null;

        var points =
            ResolveEffectiveSegmentPoints(
                segment,
                nodeById,
                segmentsByNode);

        if (
            !TryGetWorldPoint(
                scene,
                points.Start,
                out var start) ||
            !TryGetWorldPoint(
                scene,
                points.End,
                out var end) ||
            !NativeSplinePlacementMath
                .TryCreateStraight(
                    start,
                    end,
                    out var shape) ||
            shape is null)
        {
            return false;
        }

        return TryCreateRequest(
            scene,
            segment.ProfileId,
            shape,
            out request);
    }

    private static bool TryCreateRequest(
        NativeSceneSnapshot scene,
        string profileId,
        NativeSplinePlacementShape shape,
        out NativeSplinePlacementRequest?
            request)
    {
        request =
            null;

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
                .FirstOrDefault(
                    item =>
                        item.Reference.X ==
                            tileX &&
                        item.Reference.Y ==
                            tileY);

        if (tile is null)
        {
            return false;
        }

        request =
            new NativeSplinePlacementRequest(
                tile.Reference,
                profileId,
                -1,
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
                -1,
                false);

        return true;
    }

    private static (
        MapStudioRoadPoint Start,
        MapStudioRoadPoint End
    ) ResolveEffectiveSegmentPoints(
        MapStudioRoadGraphSegment segment,
        IReadOnlyDictionary<
            int,
            MapStudioRoadGraphNode>
            nodeById,
        IReadOnlyDictionary<
            int,
            MapStudioRoadGraphSegment[]>
            segmentsByNode)
    {
        var start =
            segment.Start;

        var end =
            segment.End;

        var length =
            start.DistanceTo(
                end);

        if (
            !double.IsFinite(
                length) ||
            length <=
                0.01)
        {
            return
                (
                    start,
                    end
                );
        }

        var startTrim =
            ResolveJunctionTrim(
                segment.FromNodeId,
                nodeById,
                segmentsByNode);

        var endTrim =
            ResolveJunctionTrim(
                segment.ToNodeId,
                nodeById,
                segmentsByNode);

        if (
            startTrim <=
                0 &&
            endTrim <=
                0)
        {
            return
                (
                    start,
                    end
                );
        }

        var maximumCombinedTrim =
            length *
            0.78;

        var combined =
            startTrim +
            endTrim;

        if (
            combined >
                maximumCombinedTrim &&
            combined >
                0)
        {
            var scale =
                maximumCombinedTrim /
                combined;

            startTrim *=
                scale;

            endTrim *=
                scale;
        }

        if (startTrim > 0)
        {
            start =
                MoveToward(
                    start,
                    end,
                    startTrim);
        }

        if (endTrim > 0)
        {
            end =
                MoveToward(
                    end,
                    start,
                    endTrim);
        }

        return
            (
                start,
                end
            );
    }

    private static double ResolveJunctionTrim(
        int nodeId,
        IReadOnlyDictionary<
            int,
            MapStudioRoadGraphNode>
            nodeById,
        IReadOnlyDictionary<
            int,
            MapStudioRoadGraphSegment[]>
            segmentsByNode)
    {
        if (
            !nodeById.TryGetValue(
                nodeId,
                out var node) ||
            !node.IsJunction ||
            !segmentsByNode.TryGetValue(
                nodeId,
                out var incident))
        {
            return 0;
        }

        var surfaceExtent =
            MapStudioJunctionGeometrySizing
                .ResolveSurfaceExtentMeters(
                    incident.Select(
                        ResolvePhysicalWidth));

        var overlap =
            Math.Min(
                0.45,
                surfaceExtent *
                    0.08);

        return Math.Max(
            0,
            surfaceExtent -
            overlap);
    }

    private static double ResolveMinimumCurveRadius(
        MapStudioRoadGraphSegment first,
        MapStudioRoadGraphSegment second)
    {
        var width =
            Math.Max(
                ResolvePhysicalWidth(
                    first),
                ResolvePhysicalWidth(
                    second));

        return Math.Max(
            3.0,
            width *
                0.60);
    }

    private static double ResolvePhysicalWidth(
        MapStudioRoadGraphSegment segment)
    {
        var profile =
            MapStudioStandardRoadCatalog
                .Profiles
                .FirstOrDefault(
                    candidate =>
                        string.Equals(
                            candidate.RelativePath,
                            segment.ProfileId,
                            StringComparison
                                .OrdinalIgnoreCase));

        if (profile is not null)
        {
            return profile
                .TotalWidthMeters;
        }

        return
            segment.WidthMeters is > 0 &&
            double.IsFinite(
                segment.WidthMeters.Value)
                ? segment.WidthMeters.Value
                : 7.0;
    }

    private static MapStudioRoadPoint MoveToward(
        MapStudioRoadPoint origin,
        MapStudioRoadPoint target,
        double distance)
    {
        var total =
            origin.DistanceTo(
                target);

        if (
            total <=
                0.0001 ||
            distance <=
                0)
        {
            return origin;
        }

        var factor =
            Math.Clamp(
                distance /
                total,
                0.0,
                0.95);

        return MapStudioRoadPoint
            .Lerp(
                origin,
                target,
                factor);
    }

    private static bool TryGetWorldPoint(
        NativeSceneSnapshot scene,
        MapStudioRoadPoint point,
        out Vector3 worldPoint)
    {
        worldPoint =
            default;

        if (
            !NativeTerrainSampler
                .TryGetHeightAtWorldPoint(
                    scene,
                    point.X,
                    point.Z,
                    out var height))
        {
            return false;
        }

        worldPoint =
            new Vector3(
                (float)point.X,
                (float)height,
                (float)point.Z);

        return true;
    }

    private static IReadOnlyList<
        NativeProceduralRoadPlacementLink>
        BuildLinks(
            IReadOnlyList<RequestBinding>
                bindings,
            IReadOnlyDictionary<
                int,
                MapStudioRoadGraphNode>
                nodeById)
    {
        var links =
            new List<
                NativeProceduralRoadPlacementLink>();

        for (
            var currentIndex = 0;
            currentIndex <
                bindings.Count;
            currentIndex++)
        {
            var current =
                bindings[
                    currentIndex];

            if (
                !nodeById.TryGetValue(
                    current.EndNodeId,
                    out var node) ||
                node.IsJunction ||
                node.Degree !=
                    2)
            {
                continue;
            }

            var nextIndex =
                -1;

            for (
                var candidateIndex = 0;
                candidateIndex <
                    bindings.Count;
                candidateIndex++)
            {
                if (
                    candidateIndex ==
                        currentIndex ||
                    bindings[
                        candidateIndex]
                        .StartNodeId !=
                    current.EndNodeId)
                {
                    continue;
                }

                nextIndex =
                    candidateIndex;

                break;
            }

            if (nextIndex < 0)
            {
                continue;
            }

            links.Add(
                new NativeProceduralRoadPlacementLink(
                    currentIndex,
                    nextIndex));
        }

        return links;
    }

    private sealed record RequestBinding(
        NativeSplinePlacementRequest Request,
        int StartNodeId,
        int EndNodeId,
        string TraceId);
}
