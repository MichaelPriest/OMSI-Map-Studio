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
            CreateScene(
                reference,
                terrain);

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
                    3.999f,
                    4.001f);

                Assert.InRange(
                    vertex.Color.W,
                    0.499f,
                    0.501f);
            });
    }

    [Fact]
    public void BuildUsesTerrainSurfaceWithoutPhysicalLift()
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
                    1,
                    2,
                    3,
                    4
                ]);

        var geometry =
            new NativeReferenceOverlayGeometryBuilder()
                .Build(
                    CreateScene(
                        reference,
                        terrain),
                    new NativeReferenceOverlayDefinition(
                        "reference.png",
                        300,
                        300,
                        1,
                        150,
                        150,
                        0.55f,
                        "CARTO / OpenStreetMap"));

        Assert.Equal(
            6,
            geometry.Vertices.Length);

        Assert.Equal(
            [
                1.0f,
                4.0f,
                2.0f,
                1.0f,
                3.0f,
                4.0f
            ],
            geometry.Vertices
                .Select(
                    vertex =>
                        vertex.Position.Y)
                .ToArray());
    }

    [Fact]
    public void BuildKeepsRasterNorthAtNegativeWorldZ()
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
                    0,
                    0,
                    0,
                    0
                ]);

        var geometry =
            new NativeReferenceOverlayGeometryBuilder()
                .Build(
                    CreateScene(
                        reference,
                        terrain),
                    new NativeReferenceOverlayDefinition(
                        "reference.png",
                        300,
                        300,
                        1,
                        150,
                        150,
                        0.55f,
                        "CARTO / OpenStreetMap"),
                    segments:
                        1);

        Assert.NotEmpty(
            geometry.Vertices);

        var north =
            geometry.Vertices
                .OrderBy(
                    vertex =>
                        vertex.Position.Z)
                .First();

        var south =
            geometry.Vertices
                .OrderByDescending(
                    vertex =>
                        vertex.Position.Z)
                .First();

        Assert.Equal(
            0.0f,
            north.TexCoord.Y,
            4);

        Assert.Equal(
            1.0f,
            south.TexCoord.Y,
            4);

        Assert.True(
            north.Position.Z <
            south.Position.Z);
    }

    [Fact]
    public void BuildKeepsBoundaryUvOutsideForTransparentBorderSampling()
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
                    0,
                    0,
                    0,
                    0
                ]);

        var geometry =
            new NativeReferenceOverlayGeometryBuilder()
                .Build(
                    CreateScene(
                        reference,
                        terrain),
                    new NativeReferenceOverlayDefinition(
                        "reference.png",
                        256,
                        256,
                        1,
                        128,
                        128,
                        0.55f,
                        "CARTO / OpenStreetMap"));

        Assert.Contains(
            geometry.Vertices,
            vertex =>
                vertex.TexCoord.X >
                    1.0f ||
                vertex.TexCoord.Y >
                    1.0f);
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

    private static NativeSceneSnapshot
        CreateScene(
            OmsiTileReference reference,
            OmsiTerrainGrid terrain) =>
        new(
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
}
