using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Renderer.Scene;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeProceduralRoadPreviewGeometryBuilderTests
{
    [Fact]
    public void BuilderFollowsTerrainAndMarksJunction()
    {
        var reference =
            new OmsiTileReference(
                0,
                0,
                "tile_0_0.map");

        var terrain =
            new OmsiTerrainGrid(
                1,
                [
                    5,
                    5,
                    5,
                    5
                ]);

        var content =
            new OmsiTileContent(
                new OmsiTileSummary(
                    true,
                    0,
                    0,
                    0),
                [],
                [],
                terrain);

        var tile =
            new NativeSceneTile(
                reference,
                content);

        var scene =
            new NativeSceneSnapshot(
                [tile],
                [],
                [],
                [
                    new NativeTerrainEntity(
                        reference,
                        terrain)
                ]);

        var graph =
            new MapStudioRoadGraphBuilder()
                .Build(
                    [
                        new MapStudioRoadTrace(
                            "a",
                            [
                                new(20, 50),
                                new(80, 50)
                            ],
                            "road"),
                        new MapStudioRoadTrace(
                            "b",
                            [
                                new(50, 20),
                                new(50, 80)
                            ],
                            "road")
                    ]);

        var geometry =
            new NativeProceduralRoadPreviewGeometryBuilder()
                .Build(
                    scene,
                    graph);

        Assert.Equal(
            4,
            geometry.RenderedSegmentCount);

        Assert.Equal(
            1,
            geometry.RenderedJunctionCount);

        Assert.True(
            geometry.Vertices.Length >
            8);

        Assert.All(
            geometry.Vertices,
            vertex =>
                Assert.InRange(
                    vertex.Position.Y,
                    5.54f,
                    5.56f));
    }
}
