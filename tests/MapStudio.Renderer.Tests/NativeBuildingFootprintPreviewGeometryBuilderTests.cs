using MapStudio.Core.AI;
using MapStudio.Core.Generation.Buildings;
using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Renderer.Scene;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeBuildingFootprintPreviewGeometryBuilderTests
{
    [Fact]
    public void BuilderDrawsClosedFootprintOnTerrain()
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

        var building =
            new MapStudioProjectedBuildingFootprint(
                "building",
                [
                    new(20, 20),
                    new(40, 20),
                    new(40, 35),
                    new(20, 35)
                ],
                new(30, 27.5),
                "yes",
                null,
                2,
                6,
                MapStudioBuildingRoofType.Flat,
                0,
                null,
                null);

        var geometry =
            new NativeBuildingFootprintPreviewGeometryBuilder()
                .Build(
                    scene,
                    [building]);

        Assert.Equal(
            1,
            geometry.RenderedBuildingCount);

        Assert.Equal(
            0,
            geometry.SkippedBuildingCount);

        Assert.Equal(
            12,
            geometry.Vertices.Length);

        Assert.All(
            geometry.Vertices,
            vertex =>
                Assert.InRange(
                    vertex.Position.Y,
                    4.64f,
                    4.71f));
    }

    [Fact]
    public void BuilderSkipsFootprintOutsideLoadedTerrain()
    {
        var scene =
            new NativeSceneSnapshot(
                [],
                [],
                [],
                []);

        var building =
            new MapStudioProjectedBuildingFootprint(
                "building",
                [
                    new(20, 20),
                    new(40, 20),
                    new(40, 35)
                ],
                new(30, 25),
                "yes",
                null,
                1,
                3,
                MapStudioBuildingRoofType.Flat,
                0,
                null,
                null);

        var geometry =
            new NativeBuildingFootprintPreviewGeometryBuilder()
                .Build(
                    scene,
                    [building]);

        Assert.Equal(
            0,
            geometry.RenderedBuildingCount);

        Assert.Equal(
            1,
            geometry.SkippedBuildingCount);

        Assert.Empty(
            geometry.Vertices);
    }
}
