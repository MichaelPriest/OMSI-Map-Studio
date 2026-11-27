using MapStudio.Renderer.Scene;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeSkySphereGeometryBuilderTests
{
    [Fact]
    public void BuildCreatesTexturedDoubleSidedSphere()
    {
        var geometry =
            new NativeSkySphereGeometryBuilder()
                .Build(
                    longitudeSegments: 8,
                    latitudeSegments: 4);

        Assert.Equal(
            128,
            geometry.TriangleCount);

        Assert.Equal(
            384,
            geometry.Vertices.Length);

        Assert.All(
            geometry.Vertices,
            vertex =>
            {
                Assert.InRange(
                    vertex.TexCoord.X,
                    0.0f,
                    1.0f);

                Assert.InRange(
                    vertex.TexCoord.Y,
                    0.0f,
                    1.0f);

                Assert.InRange(
                    vertex.Position.Length(),
                    0.999f,
                    1.001f);
            });
    }
}
