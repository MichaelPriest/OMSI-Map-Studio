using MapStudio.Core.Omsi.Maps;
using MapStudio.Renderer.Scene;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeWaterTriangleGeometryBuilderTests
{
    [Fact]
    public void BuilderMapsFourWaterSamplesToTileCorners()
    {
        var tile =
            new NativeSceneTile(
                new OmsiTileReference(
                    2,
                    -1,
                    "tile_2_-1.map"),
                new OmsiTileContent(
                    new OmsiTileSummary(
                        true,
                        0,
                        0,
                        0,
                        WaterMarkerPresent:
                            true,
                        WaterFileExists:
                            true,
                        WaterFileSize:
                            20),
                    [],
                    [],
                    Water:
                        new OmsiWaterGrid(
                            [
                                1.0f,
                                2.0f,
                                3.0f,
                                4.0f
                            ])));

        var scene =
            new NativeSceneSnapshot(
                [tile],
                [],
                [],
                []);

        var geometry =
            new NativeWaterTriangleGeometryBuilder()
                .Build(
                    scene);

        Assert.Equal(
            1,
            geometry.RenderedTileCount);

        Assert.Equal(
            2,
            geometry.TriangleCount);

        Assert.Equal(
            6,
            geometry.Vertices.Length);

        Assert.Contains(
            geometry.Vertices,
            vertex =>
                vertex.Position.X ==
                    600.0f &&
                vertex.Position.Z ==
                    -300.0f &&
                vertex.Position.Y ==
                    1.0f);

        Assert.Contains(
            geometry.Vertices,
            vertex =>
                vertex.Position.X ==
                    900.0f &&
                vertex.Position.Z ==
                    -300.0f &&
                vertex.Position.Y ==
                    2.0f);

        Assert.Contains(
            geometry.Vertices,
            vertex =>
                vertex.Position.X ==
                    600.0f &&
                vertex.Position.Z ==
                    0.0f &&
                vertex.Position.Y ==
                    3.0f);

        Assert.Contains(
            geometry.Vertices,
            vertex =>
                vertex.Position.X ==
                    900.0f &&
                vertex.Position.Z ==
                    0.0f &&
                vertex.Position.Y ==
                    4.0f);
    }

    [Fact]
    public void BuilderIgnoresTilesWithoutWater()
    {
        var tile =
            new NativeSceneTile(
                new OmsiTileReference(
                    0,
                    0,
                    "tile_0_0.map"),
                new OmsiTileContent(
                    new OmsiTileSummary(
                        true,
                        0,
                        0,
                        0),
                    [],
                    []));

        var scene =
            new NativeSceneSnapshot(
                [tile],
                [],
                [],
                []);

        var geometry =
            new NativeWaterTriangleGeometryBuilder()
                .Build(
                    scene);

        Assert.Equal(
            0,
            geometry.RenderedTileCount);

        Assert.Empty(
            geometry.Vertices);
    }
}
