using System.Numerics;
using MapStudio.Renderer.Viewport;

namespace MapStudio.Renderer.Scene;

public sealed class
    NativeSplineEndpointEditGeometryBuilder
{
    private static readonly Vector4 StartColor =
        new(
            0.18f,
            0.92f,
            0.38f,
            1.0f);

    private static readonly Vector4 EndColor =
        new(
            1.0f,
            0.48f,
            0.08f,
            1.0f);

    public NativeAssetPreviewGeometry Build(
        NativeSplineAsset asset,
        NativeSplinePlacementShape shape,
        float markerSize =
            1.5f)
    {
        ArgumentNullException.ThrowIfNull(
            asset);

        ArgumentNullException.ThrowIfNull(
            shape);

        var road =
            new NativeSplinePlacementGeometryBuilder()
                .Build(
                    asset,
                    shape);

        if (!road.IsRenderable)
        {
            return road;
        }

        markerSize =
            Math.Clamp(
                markerSize,
                0.5f,
                8.0f);

        var vertices =
            new List<NativeMapVertex>(
                road.Vertices.Length +
                72);

        vertices.AddRange(
            road.Vertices);

        AppendMarker(
            shape.Start,
            markerSize,
            StartColor,
            vertices);

        AppendMarker(
            shape.End,
            markerSize,
            EndColor,
            vertices);

        var minimum =
            road.Minimum;

        var maximum =
            road.Maximum;

        foreach (
            var vertex in
                vertices.Skip(
                    road.Vertices.Length))
        {
            minimum =
                Vector3.Min(
                    minimum,
                    vertex.Position);

            maximum =
                Vector3.Max(
                    maximum,
                    vertex.Position);
        }

        return new NativeAssetPreviewGeometry(
            vertices.ToArray(),
            minimum,
            maximum,
            road.SourceMeshCount +
                2,
            null);
    }

    private static void AppendMarker(
        Vector3 center,
        float size,
        Vector4 color,
        List<NativeMapVertex> output)
    {
        var half =
            size *
            0.5f;

        var bottom =
            center +
            Vector3.UnitY *
            0.12f;

        var top =
            bottom +
            Vector3.UnitY *
            size;

        var p000 =
            new Vector3(
                bottom.X -
                    half,
                bottom.Y,
                bottom.Z -
                    half);

        var p100 =
            new Vector3(
                bottom.X +
                    half,
                bottom.Y,
                bottom.Z -
                    half);

        var p110 =
            new Vector3(
                bottom.X +
                    half,
                bottom.Y,
                bottom.Z +
                    half);

        var p010 =
            new Vector3(
                bottom.X -
                    half,
                bottom.Y,
                bottom.Z +
                    half);

        var p001 =
            new Vector3(
                top.X -
                    half,
                top.Y,
                top.Z -
                    half);

        var p101 =
            new Vector3(
                top.X +
                    half,
                top.Y,
                top.Z -
                    half);

        var p111 =
            new Vector3(
                top.X +
                    half,
                top.Y,
                top.Z +
                    half);

        var p011 =
            new Vector3(
                top.X -
                    half,
                top.Y,
                top.Z +
                    half);

        AppendQuad(
            p000,
            p100,
            p110,
            p010,
            color,
            output);

        AppendQuad(
            p001,
            p011,
            p111,
            p101,
            color,
            output);

        AppendQuad(
            p000,
            p001,
            p101,
            p100,
            color,
            output);

        AppendQuad(
            p100,
            p101,
            p111,
            p110,
            color,
            output);

        AppendQuad(
            p110,
            p111,
            p011,
            p010,
            color,
            output);

        AppendQuad(
            p010,
            p011,
            p001,
            p000,
            color,
            output);
    }

    private static void AppendQuad(
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Vector3 d,
        Vector4 color,
        List<NativeMapVertex> output)
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
}
