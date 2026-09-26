using System.Numerics;
using MapStudio.Renderer.Scene;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeSplineXExporterTests
{
    [Fact]
    public void WritesDirectXTextMeshRelativeToChosenOrigin()
    {
        var geometry =
            new NativeSplineTriangleGeometry(
                [
                    new NativeMapVertex(
                        new Vector3(
                            10,
                            2,
                            20),
                        Vector4.One,
                        new Vector2(
                            0,
                            0)),
                    new NativeMapVertex(
                        new Vector3(
                            11,
                            2,
                            20),
                        Vector4.One,
                        new Vector2(
                            1,
                            0)),
                    new NativeMapVertex(
                        new Vector3(
                            10,
                            2,
                            21),
                        Vector4.One,
                        new Vector2(
                            0,
                            1))
                ],
                [],
                new Dictionary<
                    MapStudio.Renderer.Picking.PickingId,
                    NativeTriangleRange>(),
                [],
                1,
                1);

        var result =
            new NativeSplineXExporter()
                .Build(
                    geometry,
                    new Vector3(
                        10,
                        2,
                        20),
                    1);

        Assert.StartsWith(
            "xof 0303txt 0032",
            result.Content,
            StringComparison.Ordinal);

        Assert.Contains(
            "Mesh MapStudioSplineExport",
            result.Content,
            StringComparison.Ordinal);

        Assert.Contains(
            "0;0;0;,",
            result.Content,
            StringComparison.Ordinal);

        Assert.Contains(
            "1;0;0;,",
            result.Content,
            StringComparison.Ordinal);

        Assert.Contains(
            "MeshTextureCoords",
            result.Content,
            StringComparison.Ordinal);

        Assert.Equal(
            3,
            result.VertexCount);

        Assert.Equal(
            1,
            result.TriangleCount);

        Assert.Equal(
            1,
            result.SplineCount);
    }

    [Fact]
    public void RefusesEmptyGeometry()
    {
        var geometry =
            new NativeSplineTriangleGeometry(
                [],
                [],
                new Dictionary<
                    MapStudio.Renderer.Picking.PickingId,
                    NativeTriangleRange>(),
                [],
                0,
                0);

        Assert.Throws<
            InvalidDataException>(
                () =>
                    new NativeSplineXExporter()
                        .Build(
                            geometry,
                            Vector3.Zero,
                            0));
    }
}
