using System.Numerics;
using MapStudio.Renderer.Viewport;

namespace MapStudio.Renderer.Scene;

public sealed class NativeReferenceOverlayGeometryBuilder
{
    public NativeReferenceOverlayGeometry
        Build(
            NativeSceneSnapshot scene,
            NativeReferenceOverlayDefinition overlay,
            int segments = 24)
    {
        ArgumentNullException.ThrowIfNull(
            scene);

        ArgumentNullException.ThrowIfNull(
            overlay);

        if (
            string.IsNullOrWhiteSpace(
                overlay.ImagePath) ||
            overlay.Width <= 0 ||
            overlay.Height <= 0 ||
            !double.IsFinite(
                overlay.MetersPerPixel) ||
            overlay.MetersPerPixel <= 0 ||
            !double.IsFinite(
                overlay.AnchorWorldX) ||
            !double.IsFinite(
                overlay.AnchorWorldZ))
        {
            throw new InvalidDataException(
                "invalidReferenceOverlay");
        }

        var safeSegments =
            Math.Clamp(
                segments,
                1,
                64);

        var sampleCount =
            safeSegments +
            1;

        var widthMeters =
            overlay.Width *
            overlay.MetersPerPixel;

        var heightMeters =
            overlay.Height *
            overlay.MetersPerPixel;

        var opacity =
            Math.Clamp(
                overlay.Opacity,
                0.05f,
                1.0f);

        var points =
            new Vector3[
                sampleCount *
                sampleCount];

        var uvs =
            new Vector2[
                points.Length];

        for (
            var row = 0;
            row < sampleCount;
            row++)
        {
            var v =
                (double)row /
                safeSegments;

            for (
                var column = 0;
                column < sampleCount;
                column++)
            {
                var u =
                    (double)column /
                    safeSegments;

                var worldX =
                    overlay.AnchorWorldX +
                    (
                        u -
                        0.5
                    ) *
                    widthMeters;

                var worldZ =
                    overlay.AnchorWorldZ +
                    (
                        v -
                        0.5
                    ) *
                    heightMeters;

                var height =
                    NativeTerrainSampler
                        .GetHeightAtWorldPoint(
                            scene,
                            worldX,
                            worldZ) +
                    0.08;

                var index =
                    row *
                    sampleCount +
                    column;

                points[index] =
                    new Vector3(
                        (float)worldX,
                        (float)height,
                        (float)worldZ);

                uvs[index] =
                    new Vector2(
                        (float)u,
                        (float)(
                            1.0 -
                            v));
            }
        }

        var color =
            new Vector4(
                1,
                1,
                1,
                opacity);

        var vertices =
            new List<
                NativeMapVertex>(
                    safeSegments *
                    safeSegments *
                    6);

        for (
            var row = 0;
            row < safeSegments;
            row++)
        {
            for (
                var column = 0;
                column < safeSegments;
                column++)
            {
                var topLeft =
                    row *
                    sampleCount +
                    column;

                var topRight =
                    topLeft +
                    1;

                var bottomLeft =
                    topLeft +
                    sampleCount;

                var bottomRight =
                    bottomLeft +
                    1;

                AppendTriangle(
                    vertices,
                    points,
                    uvs,
                    color,
                    topLeft,
                    bottomRight,
                    topRight);

                AppendTriangle(
                    vertices,
                    points,
                    uvs,
                    color,
                    topLeft,
                    bottomLeft,
                    bottomRight);
            }
        }

        return new NativeReferenceOverlayGeometry(
            vertices.ToArray(),
            overlay.ImagePath);
    }

    private static void AppendTriangle(
        List<NativeMapVertex> output,
        IReadOnlyList<Vector3> points,
        IReadOnlyList<Vector2> uvs,
        Vector4 color,
        int a,
        int b,
        int c)
    {
        output.Add(
            new NativeMapVertex(
                points[a],
                color,
                uvs[a]));

        output.Add(
            new NativeMapVertex(
                points[b],
                color,
                uvs[b]));

        output.Add(
            new NativeMapVertex(
                points[c],
                color,
                uvs[c]));
    }
}
