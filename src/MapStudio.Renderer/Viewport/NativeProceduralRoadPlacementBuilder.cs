using System.Numerics;
using MapStudio.Core.Generation.Roads;
using MapStudio.Renderer.Scene;

namespace MapStudio.Renderer.Viewport;

public sealed record NativeProceduralRoadPlacementLink(
    int PreviousRequestIndex,
    int NextRequestIndex);

public sealed record NativeProceduralRoadPlacementBuildResult(
    IReadOnlyList<NativeSplinePlacementRequest> Requests,
    IReadOnlyList<NativeProceduralRoadPlacementLink> Links,
    int SkippedSegments);

public sealed class NativeProceduralRoadPlacementBuilder
{
    public NativeProceduralRoadPlacementBuildResult Build(
        NativeSceneSnapshot scene,
        MapStudioRoadGraph graph)
    {
        ArgumentNullException.ThrowIfNull(
            scene);

        ArgumentNullException.ThrowIfNull(
            graph);

        var requests =
            new List<
                NativeSplinePlacementRequest>(
                    graph.Segments.Count);

        var requestSegmentIds =
            new List<int>(
                graph.Segments.Count);

        var skipped =
            0;

        foreach (
            var segment in
                graph.Segments)
        {
            if (
                !NativeTerrainSampler
                    .TryGetHeightAtWorldPoint(
                        scene,
                        segment.Start.X,
                        segment.Start.Z,
                        out var startHeight) ||
                !NativeTerrainSampler
                    .TryGetHeightAtWorldPoint(
                        scene,
                        segment.End.X,
                        segment.End.Z,
                        out var endHeight))
            {
                skipped++;
                continue;
            }

            var start =
                new Vector3(
                    (float)segment.Start.X,
                    (float)startHeight,
                    (float)segment.Start.Z);

            var end =
                new Vector3(
                    (float)segment.End.X,
                    (float)endHeight,
                    (float)segment.End.Z);

            if (
                !NativeSplinePlacementMath
                    .TryCreateStraight(
                        start,
                        end,
                        out var shape) ||
                shape is null)
            {
                skipped++;
                continue;
            }

            var tileX =
                (int)Math.Floor(
                    start.X /
                    300.0f);

            var tileY =
                (int)Math.Floor(
                    start.Z /
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
                skipped++;
                continue;
            }

            requestSegmentIds.Add(
                segment.Id);

            requests.Add(
                new NativeSplinePlacementRequest(
                    tile.Reference,
                    segment.ProfileId,
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
                    0.0,
                    shape.GradientStart,
                    shape.GradientEnd,
                    false,
                    shape.Start,
                    shape.End,
                    -1,
                    false));
        }

        var requestIndexBySegmentId =
            requestSegmentIds
                .Select(
                    (segmentId, index) =>
                        (
                            segmentId,
                            index
                        ))
                .ToDictionary(
                    item =>
                        item.segmentId,
                    item =>
                        item.index);

        var links =
            new List<
                NativeProceduralRoadPlacementLink>();

        var nodeById =
            graph.Nodes.ToDictionary(
                node =>
                    node.Id);

        foreach (
            var current in
                graph.Segments)
        {
            if (
                !requestIndexBySegmentId
                    .TryGetValue(
                        current.Id,
                        out var currentIndex) ||
                !nodeById.TryGetValue(
                    current.ToNodeId,
                    out var node) ||
                node.IsJunction ||
                node.Degree !=
                    2 ||
                node.TraceIds.Count !=
                    1)
            {
                continue;
            }

            var next =
                graph.Segments
                    .FirstOrDefault(
                        candidate =>
                            candidate.Id !=
                                current.Id &&
                            candidate.TraceId ==
                                current.TraceId &&
                            candidate.FromNodeId ==
                                current.ToNodeId);

            if (
                next is null ||
                !requestIndexBySegmentId
                    .TryGetValue(
                        next.Id,
                        out var nextIndex))
            {
                continue;
            }

            links.Add(
                new NativeProceduralRoadPlacementLink(
                    currentIndex,
                    nextIndex));
        }

        return new NativeProceduralRoadPlacementBuildResult(
            requests,
            links,
            skipped);
    }
}
