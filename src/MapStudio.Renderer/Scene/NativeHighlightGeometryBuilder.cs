using System.Numerics;

namespace MapStudio.Renderer.Scene;

public static class NativeHighlightGeometryBuilder
{
    public static NativeMapVertex[] Build(
        NativeMapVertex[] sourceVertices,
        NativeTriangleRange range,
        Vector3 cameraPosition,
        Vector4 color,
        float cameraBias)
    {
        ArgumentNullException.ThrowIfNull(
            sourceVertices);

        if (
            range.VertexCount <= 0 ||
            range.StartVertex < 0 ||
            range.StartVertex +
                range.VertexCount >
            sourceVertices.Length)
        {
            return Array.Empty<
                NativeMapVertex>();
        }

        var output =
            new NativeMapVertex[
                range.VertexCount];

        var bias =
            Math.Max(
                0,
                cameraBias);

        for (
            var index = 0;
            index < output.Length;
            index++)
        {
            var source =
                sourceVertices[
                    range.StartVertex +
                    index];

            var position =
                source.Position;

            var towardCamera =
                cameraPosition -
                position;

            if (
                bias > 0 &&
                towardCamera.LengthSquared() >
                    0.000001f)
            {
                position +=
                    Vector3.Normalize(
                        towardCamera) *
                    bias;
            }

            output[index] =
                new NativeMapVertex(
                    position,
                    color);
        }

        return output;
    }
}
