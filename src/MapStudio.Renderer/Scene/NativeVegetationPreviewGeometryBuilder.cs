using System.Numerics;
using MapStudio.Core.Generation.Vegetation;

namespace MapStudio.Renderer.Scene;

public sealed record NativeVegetationPreviewGeometry(
    NativeMapVertex[] Vertices,
    int RenderedPointCount,
    int SkippedPointCount);

public sealed class NativeVegetationPreviewGeometryBuilder
{
    private static readonly Vector4
        MarkerColor =
            new(
                0.30f,
                0.95f,
                0.42f,
                1.0f);

    public NativeVegetationPreviewGeometry Build(
        NativeSceneSnapshot scene,
        IReadOnlyList<
            MapStudioProjectedVegetationPoint>
            points)
    {
        ArgumentNullException.ThrowIfNull(
            scene);

        ArgumentNullException.ThrowIfNull(
            points);

        var vertices =
            new List<
                NativeMapVertex>(
                    points.Count *
                    6);

        var rendered =
            0;

        var skipped =
            0;

        foreach (
            var point in points)
        {
            if (
                !NativeTerrainSampler
                    .TryGetHeightAtWorldPoint(
                        scene,
                        point.Position.X,
                        point.Position.Z,
                        out var height))
            {
                skipped++;
                continue;
            }

            var center =
                new Vector3(
                    (float)
                        point.Position.X,
                    (float)
                        height +
                    0.25f,
                    (float)
                        point.Position.Z);

            const float radius =
                0.9f;

            const float stemHeight =
                2.2f;

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
                        0));

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
                        radius));

            AddLine(
                vertices,
                center,
                center +
                    new Vector3(
                        0,
                        stemHeight,
                        0));

            rendered++;
        }

        return new NativeVegetationPreviewGeometry(
            vertices.ToArray(),
            rendered,
            skipped);
    }

    private static void AddLine(
        ICollection<NativeMapVertex> output,
        Vector3 from,
        Vector3 to)
    {
        output.Add(
            new NativeMapVertex(
                from,
                MarkerColor));

        output.Add(
            new NativeMapVertex(
                to,
                MarkerColor));
    }
}
