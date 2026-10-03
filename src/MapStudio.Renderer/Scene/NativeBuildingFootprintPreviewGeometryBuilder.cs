using System.Numerics;
using MapStudio.Core.Generation.Buildings;

namespace MapStudio.Renderer.Scene;

public sealed record NativeBuildingFootprintPreviewGeometry(
    NativeMapVertex[] Vertices,
    int RenderedBuildingCount,
    int SkippedBuildingCount);

public sealed class NativeBuildingFootprintPreviewGeometryBuilder
{
    private static readonly Vector4
        FootprintColor =
            new(
                1.0f,
                0.72f,
                0.18f,
                1.0f);

    private static readonly Vector4
        CenterColor =
            new(
                0.18f,
                0.90f,
                0.72f,
                1.0f);

    public NativeBuildingFootprintPreviewGeometry Build(
        NativeSceneSnapshot scene,
        IReadOnlyList<MapStudioProjectedBuildingFootprint> buildings)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(buildings);

        var vertices =
            new List<NativeMapVertex>();

        var rendered =
            0;

        var skipped =
            0;

        foreach (var building in buildings)
        {
            if (
                building.Points.Count <
                    3)
            {
                skipped++;
                continue;
            }

            var worldPoints =
                new List<Vector3>(
                    building.Points.Count);

            var valid =
                true;

            foreach (var point in building.Points)
            {
                if (
                    !NativeTerrainSampler
                        .TryGetHeightAtWorldPoint(
                            scene,
                            point.X,
                            point.Z,
                            out var height))
                {
                    valid =
                        false;
                    break;
                }

                worldPoints.Add(
                    new Vector3(
                        (float)point.X,
                        (float)height +
                            0.65f,
                        (float)point.Z));
            }

            if (!valid)
            {
                skipped++;
                continue;
            }

            for (
                var index = 0;
                index <
                    worldPoints.Count;
                index++)
            {
                AddLine(
                    vertices,
                    worldPoints[index],
                    worldPoints[
                        (
                            index +
                            1
                        ) %
                        worldPoints.Count],
                    FootprintColor);
            }

            if (
                NativeTerrainSampler
                    .TryGetHeightAtWorldPoint(
                        scene,
                        building.Center.X,
                        building.Center.Z,
                        out var centerHeight))
            {
                var center =
                    new Vector3(
                        (float)building.Center.X,
                        (float)centerHeight +
                            0.7f,
                        (float)building.Center.Z);

                const float radius =
                    0.8f;

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
                    CenterColor);

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
                    CenterColor);
            }

            rendered++;
        }

        return new NativeBuildingFootprintPreviewGeometry(
            vertices.ToArray(),
            rendered,
            skipped);
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
