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

    public int BridgeRequestCount =>
        Requests.Count(
            request =>
                request.SourceBridge);

    public int TunnelRequestCount =>
        Requests.Count(
            request =>
                request.SourceTunnel);

    public int LayeredRequestCount =>
        Requests.Count(
            request =>
                (request.SourceLayer ?? 0) !=
                0);
}

public sealed class NativeProceduralRoadPlacementBuilder
{
    private const double MinimumCurveSweepDegrees =
        1.5;

    private const double MaximumCurveSweepDegrees =
        110.0;

    private const double
        MinimumLayerVerticalSeparationMeters =
            4.8;

    private const double
        MaximumLayerRampGradient =
            0.08;

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

        var structuralElevations =
            BuildStructuralEndpointElevations(
                scene,
                graph.Segments,
                nodeById,
                segmentsByNode);

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
                            structuralElevations,
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
                        structuralElevations,
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
            !HasCompatibleGradeSeparation(
                first,
                second) ||
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


    private static bool HasCompatibleGradeSeparation(
        MapStudioRoadGraphSegment first,
        MapStudioRoadGraphSegment second) =>
        first.Bridge ==
            second.Bridge &&
        first.Tunnel ==
            second.Tunnel &&
        (first.Layer ?? 0) ==
            (second.Layer ?? 0);

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
        IReadOnlyDictionary<
            int,
            StructuralEndpointElevation>
            structuralElevations,
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
                first,
                startPoint,
                structuralElevations,
                out var start) ||
            !TryGetWorldPoint(
                scene,
                first,
                controlPoint,
                structuralElevations,
                out var control) ||
            !TryGetWorldPoint(
                scene,
                second,
                endPoint,
                structuralElevations,
                out var end))
        {
            return false;
        }

        start =
            ApplyStructuralJunctionBoundaryHeight(
                scene,
                first.FromNodeId,
                first,
                start,
                nodeById,
                segmentsByNode);

        end =
            ApplyStructuralJunctionBoundaryHeight(
                scene,
                second.ToNodeId,
                second,
                end,
                nodeById,
                segmentsByNode);

        if (
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
            first,
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
        IReadOnlyDictionary<
            int,
            StructuralEndpointElevation>
            structuralElevations,
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
                segment,
                points.Start,
                structuralElevations,
                out var start) ||
            !TryGetWorldPoint(
                scene,
                segment,
                points.End,
                structuralElevations,
                out var end))
        {
            return false;
        }

        start =
            ApplyStructuralJunctionBoundaryHeight(
                scene,
                segment.FromNodeId,
                segment,
                start,
                nodeById,
                segmentsByNode);

        end =
            ApplyStructuralJunctionBoundaryHeight(
                scene,
                segment.ToNodeId,
                segment,
                end,
                nodeById,
                segmentsByNode);

        if (
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
            segment,
            out request);
    }

    private static bool TryCreateRequest(
        NativeSceneSnapshot scene,
        string profileId,
        NativeSplinePlacementShape shape,
        MapStudioRoadGraphSegment sourceSegment,
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

        var (
            placementBridge,
            placementTunnel
        ) =
            ResolvePlacementStructure(
                sourceSegment);

        var placementSplinePath =
            MapStudioStandardRoadCatalog
                .ResolvePlacementRelativePath(
                    profileId,
                    placementBridge,
                    placementTunnel);

        request =
            new NativeSplinePlacementRequest(
                tile.Reference,
                placementSplinePath,
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
                false,
                sourceSegment.Bridge,
                sourceSegment.Tunnel,
                sourceSegment.Layer);

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

    private static IReadOnlyDictionary<
        int,
        StructuralEndpointElevation>
        BuildStructuralEndpointElevations(
            NativeSceneSnapshot scene,
            IReadOnlyList<
                MapStudioRoadGraphSegment>
                segments,
            IReadOnlyDictionary<
                int,
                MapStudioRoadGraphNode>
                nodeById,
            IReadOnlyDictionary<
                int,
                MapStudioRoadGraphSegment[]>
                segmentsByNode)
    {
        var result =
            new Dictionary<
                int,
                StructuralEndpointElevation>();

        foreach (
            var traceGroup in
                segments
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
                index <
                    ordered.Length;)
            {
                if (!IsStructuralSegment(
                        ordered[index]))
                {
                    index++;

                    continue;
                }

                var runStart =
                    index;

                var runEnd =
                    index;

                while (
                    runEnd + 1 <
                        ordered.Length &&
                    ordered[runEnd]
                        .ToNodeId ==
                    ordered[
                        runEnd +
                        1]
                        .FromNodeId &&
                    HasCompatibleGradeSeparation(
                        ordered[runEnd],
                        ordered[
                            runEnd +
                            1]) &&
                    IsStructuralSegment(
                        ordered[
                            runEnd +
                            1]))
                {
                    runEnd++;
                }

                var first =
                    ordered[
                        runStart];

                var last =
                    ordered[
                        runEnd];

                if (
                    !NativeTerrainSampler
                        .TryGetHeightAtWorldPoint(
                            scene,
                            first.Start.X,
                            first.Start.Z,
                            out var startHeight) ||
                    !NativeTerrainSampler
                        .TryGetHeightAtWorldPoint(
                            scene,
                            last.End.X,
                            last.End.Z,
                            out var endHeight))
                {
                    index =
                        runEnd +
                        1;

                    continue;
                }

                startHeight +=
                    ResolveStructuralJunctionBoundaryOffset(
                        first.FromNodeId,
                        first,
                        nodeById,
                        segmentsByNode);

                endHeight +=
                    ResolveStructuralJunctionBoundaryOffset(
                        last.ToNodeId,
                        last,
                        nodeById,
                        segmentsByNode);

                var totalLength =
                    0.0;

                for (
                    var runIndex =
                        runStart;
                    runIndex <=
                        runEnd;
                    runIndex++)
                {
                    totalLength +=
                        Math.Max(
                            0,
                            ordered[
                                runIndex]
                                .LengthMeters);
                }

                if (
                    totalLength <=
                    0.0001)
                {
                    index =
                        runEnd +
                        1;

                    continue;
                }

                var layerDirection =
                    ResolveLayerVerticalDirection(
                        first);

                var targetSeparation =
                    ResolveLayerTargetSeparationMeters(
                        first);

                var traveled =
                    0.0;

                for (
                    var runIndex =
                        runStart;
                    runIndex <=
                        runEnd;
                    runIndex++)
                {
                    var segment =
                        ordered[
                            runIndex];

                    var segmentStartHeight =
                        LerpHeight(
                            startHeight,
                            endHeight,
                            traveled /
                            totalLength);

                    segmentStartHeight +=
                        ResolveLayerClearanceOffset(
                            scene,
                            segment.Start,
                            segmentStartHeight,
                            traveled,
                            totalLength,
                            layerDirection,
                            targetSeparation);

                    traveled +=
                        Math.Max(
                            0,
                            segment
                                .LengthMeters);

                    var segmentEndHeight =
                        LerpHeight(
                            startHeight,
                            endHeight,
                            traveled /
                            totalLength);

                    segmentEndHeight +=
                        ResolveLayerClearanceOffset(
                            scene,
                            segment.End,
                            segmentEndHeight,
                            traveled,
                            totalLength,
                            layerDirection,
                            targetSeparation);

                    result[
                        segment.Id] =
                        new StructuralEndpointElevation(
                            segmentStartHeight,
                            segmentEndHeight);
                }

                index =
                    runEnd +
                    1;
            }
        }

        return result;
    }

    private static bool IsStructuralSegment(
        MapStudioRoadGraphSegment segment) =>
        segment.Bridge ||
        segment.Tunnel ||
        (segment.Layer ?? 0) !=
            0;

    private static double
        ResolveStructuralJunctionBoundaryOffset(
            int nodeId,
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
        if (
            !IsStructuralSegment(
                segment) ||
            !nodeById.TryGetValue(
                nodeId,
                out var node) ||
            !node.IsJunction ||
            !segmentsByNode.TryGetValue(
                nodeId,
                out var incident) ||
            incident.Length <
                3 ||
            incident.Any(
                candidate =>
                    !IsStructuralSegment(
                        candidate) ||
                    !HasCompatibleGradeSeparation(
                        segment,
                        candidate)))
        {
            return 0;
        }

        var direction =
            ResolveLayerVerticalDirection(
                segment);

        var separation =
            ResolveLayerTargetSeparationMeters(
                segment);

        return direction *
            separation;
    }

    private static Vector3
        ApplyStructuralJunctionBoundaryHeight(
            NativeSceneSnapshot scene,
            int nodeId,
            MapStudioRoadGraphSegment segment,
            Vector3 point,
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
            !IsStructuralSegment(
                segment) ||
            !nodeById.TryGetValue(
                nodeId,
                out var node) ||
            !node.IsJunction ||
            !segmentsByNode.TryGetValue(
                nodeId,
                out var incident) ||
            incident.Length <
                3 ||
            incident.Any(
                candidate =>
                    !IsStructuralSegment(
                        candidate) ||
                    !HasCompatibleGradeSeparation(
                        segment,
                        candidate)) ||
            !NativeTerrainSampler
                .TryGetHeightAtWorldPoint(
                    scene,
                    node.Position.X,
                    node.Position.Z,
                    out var terrainHeight))
        {
            return point;
        }

        point.Y =
            (float)(
                terrainHeight +
                ResolveStructuralJunctionBoundaryOffset(
                    nodeId,
                    segment,
                    nodeById,
                    segmentsByNode));

        return point;
    }

    private static (
        bool Bridge,
        bool Tunnel
    ) ResolvePlacementStructure(
        MapStudioRoadGraphSegment segment)
    {
        if (
            segment.Bridge !=
            segment.Tunnel)
        {
            return
                (
                    segment.Bridge,
                    segment.Tunnel
                );
        }

        var layer =
            segment.Layer ??
            0;

        return layer switch
        {
            > 0 =>
                (
                    true,
                    false
                ),
            < 0 =>
                (
                    false,
                    true
                ),
            _ =>
                (
                    false,
                    false
                )
        };
    }

    private static int
        ResolveLayerVerticalDirection(
            MapStudioRoadGraphSegment segment)
    {
        var layer =
            segment.Layer ??
            0;

        if (layer == 0)
        {
            return 0;
        }

        if (
            segment.Bridge &&
            !segment.Tunnel)
        {
            return 1;
        }

        if (
            segment.Tunnel &&
            !segment.Bridge)
        {
            return -1;
        }

        return Math.Sign(
            layer);
    }

    private static double
        ResolveLayerTargetSeparationMeters(
            MapStudioRoadGraphSegment segment)
    {
        var layer =
            Math.Abs(
                segment.Layer ??
                0);

        return layer == 0
            ? 0
            : MinimumLayerVerticalSeparationMeters *
                layer;
    }

    private static double
        ResolveLayerClearanceOffset(
            NativeSceneSnapshot scene,
            MapStudioRoadPoint point,
            double baselineHeight,
            double traveled,
            double totalLength,
            int direction,
            double targetSeparation)
    {
        if (
            direction == 0 ||
            targetSeparation <=
                0 ||
            traveled <=
                0.0001 ||
            traveled >=
                totalLength -
                0.0001 ||
            !NativeTerrainSampler
                .TryGetHeightAtWorldPoint(
                    scene,
                    point.X,
                    point.Z,
                    out var terrainHeight))
        {
            return 0;
        }

        var currentSeparation =
            direction *
            (
                baselineHeight -
                terrainHeight
            );

        var needed =
            Math.Max(
                0,
                targetSeparation -
                currentSeparation);

        if (
            needed <=
            0.0001)
        {
            return 0;
        }

        var rampCapacity =
            Math.Min(
                traveled,
                totalLength -
                    traveled) *
            MaximumLayerRampGradient;

        return direction *
            Math.Min(
                needed,
                Math.Max(
                    0,
                    rampCapacity));
    }

    private static double LerpHeight(
        double start,
        double end,
        double amount) =>
        start +
        (
            end -
            start
        ) *
        Math.Clamp(
            amount,
            0,
            1);

    private static bool TryGetWorldPoint(
        NativeSceneSnapshot scene,
        MapStudioRoadGraphSegment segment,
        MapStudioRoadPoint point,
        IReadOnlyDictionary<
            int,
            StructuralEndpointElevation>
            structuralElevations,
        out Vector3 worldPoint)
    {
        worldPoint =
            default;

        double height;

        if (
            structuralElevations
                .TryGetValue(
                    segment.Id,
                    out var structural))
        {
            var dx =
                segment.End.X -
                segment.Start.X;

            var dz =
                segment.End.Z -
                segment.Start.Z;

            var lengthSquared =
                dx * dx +
                dz * dz;

            var amount =
                lengthSquared >
                    0.0000001
                    ? (
                        (
                            point.X -
                            segment.Start.X
                        ) *
                        dx +
                        (
                            point.Z -
                            segment.Start.Z
                        ) *
                        dz
                    ) /
                    lengthSquared
                    : 0.0;

            height =
                LerpHeight(
                    structural.StartHeight,
                    structural.EndHeight,
                    amount);
        }
        else if (
            !NativeTerrainSampler
                .TryGetHeightAtWorldPoint(
                    scene,
                    point.X,
                    point.Z,
                    out height))
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

    private readonly record struct StructuralEndpointElevation(
        double StartHeight,
        double EndHeight);

    private sealed record RequestBinding(
        NativeSplinePlacementRequest Request,
        int StartNodeId,
        int EndNodeId,
        string TraceId);
}
