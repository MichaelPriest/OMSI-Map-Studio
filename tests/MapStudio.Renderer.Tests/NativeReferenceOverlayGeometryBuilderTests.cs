using MapStudio.Core.Omsi.Maps;
using MapStudio.Renderer.Scene;
using MapStudio.Renderer.Viewport;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeReferenceOverlayGeometryBuilderTests
{
    [Fact]
    public void BuildClipsOverlayToLoadedTerrain()
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
                    4,
                    4,
                    4,
                    4
                ]);

        var scene =
            new NativeSceneSnapshot(
                [
                    new NativeSceneTile(
                        reference,
                        new OmsiTileContent(
                            new OmsiTileSummary(
                                true,
                                0,
                                0,
                                0),
                            [],
                            [],
                            terrain))
                ],
                [],
                [],
                [
                    new NativeTerrainEntity(
                        reference,
                        terrain)
                ]);

        var overlay =
            new NativeReferenceOverlayDefinition(
                "reference.png",
                640,
                640,
                1,
                320,
                320,
                0.5f,
                "CARTO / OpenStreetMap");

        var geometry =
            new NativeReferenceOverlayGeometryBuilder()
                .Build(
                    scene,
                    overlay);

        Assert.NotEmpty(
            geometry.Vertices);

        Assert.All(
            geometry.Vertices,
            vertex =>
            {
                Assert.InRange(
                    vertex.Position.X,
                    0,
                    300);

                Assert.InRange(
                    vertex.Position.Z,
                    0,
                    300);

                Assert.InRange(
                    vertex.Position.Y,
                    4.079f,
                    4.081f);

                Assert.InRange(
                    vertex.Color.W,
                    0.499f,
                    0.501f);

                Assert.InRange(
                    vertex.TexCoord.X,
                    0,
                    1);

                Assert.InRange(
                    vertex.TexCoord.Y,
                    0,
                    1);
            });
    }

    [Fact]
    public void BuildSkipsReferenceOutsideLoadedTerrain()
    {
        var scene =
            new NativeSceneSnapshot(
                [],
                [],
                [],
                []);

        var overlay =
            new NativeReferenceOverlayDefinition(
                "reference.png",
                256,
                256,
                1,
                128,
                128,
                0.55f,
                "CARTO / OpenStreetMap");

        var geometry =
            new NativeReferenceOverlayGeometryBuilder()
                .Build(
                    scene,
                    overlay);

        Assert.Empty(
            geometry.Vertices);

        Assert.Equal(
            0,
            geometry.TriangleCount);
    }
}
