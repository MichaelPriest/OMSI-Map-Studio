using System.Numerics;
using MapStudio.Core.Generation.Roads;

namespace MapStudio.Renderer.Scene;

public sealed record NativeProceduralRoadPreviewGeometry(
    NativeMapVertex[] Vertices,
    int RenderedSegmentCount,
    int RenderedJunctionCount);

public sealed class NativeProceduralRoadPreviewGeometryBuilder
{
    private static readonly Vector4
        RoadColor =
            new(
                0.12f,
                0.82f,
                1.0f,
                1.0f);

    private static readonly Vector4
        JunctionColor =
            new(
                1.0f,
                0.34f,
                0.14f,
                1.0f);

    public NativeProceduralRoadPreviewGeometry Build(
        NativeSceneSnapshot scene,
        MapStudioRoadGraph graph)
    {
        ArgumentNullException.ThrowIfNull(
            scene);

        ArgumentNullException.ThrowIfNull(
            graph);

        var vertices =
            new List<NativeMapVertex>();

        var renderedSegments =
            0;

        foreach (
            var segment in graph.Segments)
        {
            if (
                !TryWorldPoint(
                    scene,
                    segment.Start,
                    out var start) ||
                !TryWorldPoint(
                    scene,
                    segment.End,
                    out var end))
            {
                continue;
            }

            AddLine(
                vertices,
                start,
                end,
                RoadColor);

            renderedSegments++;
        }

        var renderedJunctions =
            0;

        foreach (
            var junction in
                graph.Junctions)
        {
            if (
                !TryWorldPoint(
                    scene,
                    junction.Position,
                    out var center))
            {
                continue;
            }

            var radius =
                1.25f +
                Math.Min(
                    2.0f,
                    Math.Max(
                        0,
                        junction.Degree -
                        3) *
                    0.35f);

            AddLine(
                vertices,
                center +
                    new Vector3(
                        -radius,
                        0,
                        0),
                center +
                    new Vector3(
                        radius,
                        0,
                        0),
                JunctionColor);

            AddLine(
                vertices,
                center +
                    new Vector3(
                        0,
                        0,
                        -radius),
                center +
                    new Vector3(
                        0,
                        0,
                        radius),
                JunctionColor);

            AddLine(
                vertices,
                center +
                    new Vector3(
                        -radius *
                        0.70f,
                        0,
                        -radius *
                        0.70f),
                center +
                    new Vector3(
                        radius *
                        0.70f,
                        0,
                        radius *
                        0.70f),
                JunctionColor);

            AddLine(
                vertices,
                center +
                    new Vector3(
                        -radius *
                        0.70f,
                        0,
                        radius *
                        0.70f),
                center +
                    new Vector3(
                        radius *
                        0.70f,
                        0,
                        -radius *
                        0.70f),
                JunctionColor);

            renderedJunctions++;
        }

        return new NativeProceduralRoadPreviewGeometry(
            vertices.ToArray(),
            renderedSegments,
            renderedJunctions);
    }

    private static bool TryWorldPoint(
        NativeSceneSnapshot scene,
        MapStudioRoadPoint point,
        out Vector3 world)
    {
        world =
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

        world =
            new Vector3(
                (float)point.X,
                (float)height +
                    0.55f,
                (float)point.Z);

        return true;
    }

    private static void AddLine(
        ICollection<NativeMapVertex> output,
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
