using MapStudio.Renderer.Picking;
using MapStudio.Renderer.Scene;
using MapStudio.Renderer.Viewport;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeReferenceOverlayGeometryBuilderTests
{
    [Fact]
    public void BuildCreatesTerrainFollowingGrid()
    {
        var scene =
            new NativeSceneSnapshot(
                Array.Empty<
                    NativeSceneTile>(),
                Array.Empty<
                    NativeObjectEntity>(),
                Array.Empty<
                    NativeSplineEntity>(),
                Array.Empty<
                    NativeTerrainEntity>());

        var overlay =
            new NativeReferenceOverlayDefinition(
                "reference.png",
                640,
                640,
                1,
                320,
                320,
                0.5f,
                "Google Maps");

        var geometry =
            new NativeReferenceOverlayGeometryBuilder()
                .Build(
                    scene,
                    overlay);

        Assert.Equal(
            24 *
            24 *
            6,
            geometry.Vertices.Length);

        Assert.Equal(
            24 *
            24 *
            2,
            geometry.TriangleCount);

        Assert.All(
            geometry.Vertices,
            vertex =>
            {
                Assert.InRange(
                    vertex.Position.Y,
                    0.079f,
                    0.081f);

                Assert.InRange(
                    vertex.Color.W,
                    0.499f,
                    0.501f);
            });
    }
}
