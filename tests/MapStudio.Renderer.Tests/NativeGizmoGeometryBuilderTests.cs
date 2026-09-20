using System.Numerics;
using MapStudio.Renderer.Picking;
using MapStudio.Renderer.Scene;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeGizmoGeometryBuilderTests
{
    [Fact]
    public void MoveGizmoBuildsThreePickableAxes()
    {
        var geometry =
            new NativeGizmoGeometryBuilder()
                .Build(
                    new Vector3(
                        10,
                        20,
                        30),
                    NativeGizmoMode.Move,
                    12,
                    fullRotation: true);

        Assert.NotEmpty(
            geometry.Vertices);

        Assert.NotEmpty(
            geometry.PickingVertices);

        var ids =
            geometry.PickingVertices
                .Select(
                    vertex =>
                        Decode(
                            vertex.Color))
                .Where(
                    id =>
                        id.Kind ==
                        PickingKind.Gizmo)
                .Distinct()
                .ToArray();

        Assert.Contains(
            NativeGizmoIds.ToPickingId(
                NativeGizmoHandle.MoveX),
            ids);

        Assert.Contains(
            NativeGizmoIds.ToPickingId(
                NativeGizmoHandle.MoveY),
            ids);

        Assert.Contains(
            NativeGizmoIds.ToPickingId(
                NativeGizmoHandle.MoveZ),
            ids);
    }

    [Fact]
    public void SplineRotateGizmoKeepsOnlyYawRing()
    {
        var geometry =
            new NativeGizmoGeometryBuilder()
                .Build(
                    Vector3.Zero,
                    NativeGizmoMode.Rotate,
                    10,
                    fullRotation: false);

        var ids =
            geometry.PickingVertices
                .Select(
                    vertex =>
                        Decode(
                            vertex.Color))
                .Distinct()
                .ToArray();

        Assert.Single(
            ids);

        Assert.Equal(
            NativeGizmoIds.ToPickingId(
                NativeGizmoHandle.RotateY),
            ids[0]);
    }

    private static PickingId Decode(
        Vector4 color)
    {
        byte Channel(
            float value) =>
            (byte)Math.Clamp(
                MathF.Round(
                    value *
                    255.0f),
                0,
                255);

        var encoded =
            (uint)(
                Channel(color.X) |
                Channel(color.Y) << 8 |
                Channel(color.Z) << 16 |
                Channel(color.W) << 24);

        return
            PickingColorCodec
                .Decode(
                    encoded);
    }
}
