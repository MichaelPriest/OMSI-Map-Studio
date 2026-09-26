using System.Numerics;

namespace MapStudio.Renderer.Scene;

public sealed record NativeSkyGeometry(
    NativeMapVertex[] Vertices)
{
    public int TriangleCount =>
        Vertices.Length / 3;
}

public sealed class NativeSkySphereGeometryBuilder
{
    public NativeSkyGeometry Build(
        int longitudeSegments = 32,
        int latitudeSegments = 16)
    {
        longitudeSegments =
            Math.Clamp(
                longitudeSegments,
                8,
                128);

        latitudeSegments =
            Math.Clamp(
                latitudeSegments,
                4,
                64);

        var vertices =
            new List<NativeMapVertex>(
                longitudeSegments *
                latitudeSegments *
                12);

        for (
            var latitude = 0;
            latitude < latitudeSegments;
            latitude++)
        {
            var v0 =
                latitude /
                (float)latitudeSegments;

            var v1 =
                (latitude + 1) /
                (float)latitudeSegments;

            var theta0 =
                v0 *
                MathF.PI;

            var theta1 =
                v1 *
                MathF.PI;

            for (
                var longitude = 0;
                longitude < longitudeSegments;
                longitude++)
            {
                var u0 =
                    longitude /
                    (float)longitudeSegments;

                var u1 =
                    (longitude + 1) /
                    (float)longitudeSegments;

                var phi0 =
                    u0 *
                    MathF.Tau;

                var phi1 =
                    u1 *
                    MathF.Tau;

                var topLeft =
                    CreatePoint(
                        theta0,
                        phi0);

                var topRight =
                    CreatePoint(
                        theta0,
                        phi1);

                var bottomLeft =
                    CreatePoint(
                        theta1,
                        phi0);

                var bottomRight =
                    CreatePoint(
                        theta1,
                        phi1);

                var uvTopLeft =
                    new Vector2(
                        1.0f - u0,
                        v0);

                var uvTopRight =
                    new Vector2(
                        1.0f - u1,
                        v0);

                var uvBottomLeft =
                    new Vector2(
                        1.0f - u0,
                        v1);

                var uvBottomRight =
                    new Vector2(
                        1.0f - u1,
                        v1);

                AppendDoubleSidedTriangle(
                    topLeft,
                    bottomRight,
                    topRight,
                    uvTopLeft,
                    uvBottomRight,
                    uvTopRight,
                    vertices);

                AppendDoubleSidedTriangle(
                    topLeft,
                    bottomLeft,
                    bottomRight,
                    uvTopLeft,
                    uvBottomLeft,
                    uvBottomRight,
                    vertices);
            }
        }

        return new NativeSkyGeometry(
            vertices.ToArray());
    }

    private static Vector3 CreatePoint(
        float theta,
        float phi)
    {
        var sinTheta =
            MathF.Sin(theta);

        return new Vector3(
            sinTheta *
                MathF.Sin(phi),
            MathF.Cos(theta),
            sinTheta *
                MathF.Cos(phi));
    }

    private static void AppendDoubleSidedTriangle(
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Vector2 uvA,
        Vector2 uvB,
        Vector2 uvC,
        List<NativeMapVertex> output)
    {
        var color =
            Vector4.One;

        output.Add(
            new NativeMapVertex(
                a,
                color,
                uvA));

        output.Add(
            new NativeMapVertex(
                b,
                color,
                uvB));

        output.Add(
            new NativeMapVertex(
                c,
                color,
                uvC));

        output.Add(
            new NativeMapVertex(
                c,
                color,
                uvC));

        output.Add(
            new NativeMapVertex(
                b,
                color,
                uvB));

        output.Add(
            new NativeMapVertex(
                a,
                color,
                uvA));
    }
}
