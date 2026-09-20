using System.Numerics;
using MapStudio.Renderer.Scene;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeHighlightGeometryBuilderTests
{
    [Fact]
    public void BuildRecolorsAndBiasesGeometryTowardCamera()
    {
        var source =
            new[]
            {
                new NativeMapVertex(
                    new Vector3(
                        0,
                        0,
                        0),
                    Vector4.One),
                new NativeMapVertex(
                    new Vector3(
                        1,
                        0,
                        0),
                    Vector4.One),
                new NativeMapVertex(
                    new Vector3(
                        0,
                        0,
                        1),
                    Vector4.One)
            };

        var color =
            new Vector4(
                0,
                0.45f,
                1,
                1);

        var result =
            NativeHighlightGeometryBuilder
                .Build(
                    source,
                    new NativeTriangleRange(
                        0,
                        3),
                    new Vector3(
                        0,
                        10,
                        0),
                    color,
                    0.05f);

        Assert.Equal(
            3,
            result.Length);

        Assert.All(
            result,
            vertex =>
            {
                Assert.Equal(
                    color,
                    vertex.Color);

                Assert.True(
                    vertex.Position.Y >
                    0);
            });
    }

    [Fact]
    public void BuildRejectsRangeOutsideSource()
    {
        var result =
            NativeHighlightGeometryBuilder
                .Build(
                    Array.Empty<
                        NativeMapVertex>(),
                    new NativeTriangleRange(
                        2,
                        3),
                    Vector3.One,
                    Vector4.One,
                    0.05f);

        Assert.Empty(
            result);
    }
}
