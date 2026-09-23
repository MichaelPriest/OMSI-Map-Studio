using System.Numerics;
using MapStudio.Core.ProtonBus;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class ProtonBusExportSceneTests
{
    [Fact]
    public void PlannerKeepsSmallMeshInSingleChunk()
    {
        var mesh =
            CreateQuad();

        var chunks =
            ProtonBus3dsMeshChunkPlanner
                .Plan(
                    mesh);

        var chunk =
            Assert.Single(
                chunks);

        Assert.Equal(
            "road_0001",
            chunk.Name);

        Assert.Equal(
            4,
            chunk.Vertices.Count);

        Assert.Equal(
            2,
            chunk.Triangles.Count);
    }

    [Fact]
    public void PlannerSplitsWhenFaceLimitIsReached()
    {
        var mesh =
            CreateQuad();

        var chunks =
            ProtonBus3dsMeshChunkPlanner
                .Plan(
                    mesh,
                    maxVertices: 65535,
                    maxFaces: 1);

        Assert.Equal(
            2,
            chunks.Count);

        Assert.All(
            chunks,
            chunk =>
                Assert.Single(
                    chunk.Triangles));
    }

    [Fact]
    public void PlannerRemapsVertexIndicesInsideEachChunk()
    {
        var mesh =
            CreateQuad();

        var chunks =
            ProtonBus3dsMeshChunkPlanner
                .Plan(
                    mesh,
                    maxVertices: 3,
                    maxFaces: 10);

        Assert.Equal(
            2,
            chunks.Count);

        Assert.All(
            chunks,
            chunk =>
            {
                Assert.Equal(
                    3,
                    chunk.Vertices.Count);

                var triangle =
                    Assert.Single(
                        chunk.Triangles);

                Assert.InRange(
                    triangle.A,
                    0,
                    2);

                Assert.InRange(
                    triangle.B,
                    0,
                    2);

                Assert.InRange(
                    triangle.C,
                    0,
                    2);
            });
    }

    [Fact]
    public void PlannerRejectsOutOfRangeTriangleIndices()
    {
        var mesh =
            new ProtonBusExportMesh(
                "broken",
                [
                    new(
                        Vector3.Zero,
                        Vector2.Zero)
                ],
                [
                    new(
                        0,
                        1,
                        2)
                ],
                []);

        Assert.Throws<
            ArgumentException>(
                () =>
                    ProtonBus3dsMeshChunkPlanner
                        .Plan(
                            mesh));
    }

    private static ProtonBusExportMesh
        CreateQuad() =>
        new(
            "road",
            [
                new(
                    new(
                        0,
                        0,
                        0),
                    new(
                        0,
                        0)),
                new(
                    new(
                        1,
                        0,
                        0),
                    new(
                        1,
                        0)),
                new(
                    new(
                        1,
                        0,
                        1),
                    new(
                        1,
                        1)),
                new(
                    new(
                        0,
                        0,
                        1),
                    new(
                        0,
                        1))
            ],
            [
                new(
                    0,
                    1,
                    2,
                    "asphalt"),
                new(
                    0,
                    2,
                    3,
                    "asphalt")
            ],
            [
                new(
                    "asphalt",
                    "asphalt.png")
            ]);
}
