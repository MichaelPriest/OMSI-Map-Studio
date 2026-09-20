using MapStudio.Core.Omsi.Maps;
using MapStudio.Renderer.Scene;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeTerrainTests
{
    [Fact]
    public void SamplerUsesBilinearTerrainInterpolation()
    {
        var tile =
            new NativeSceneTile(
                new OmsiTileReference(0, 0, "tile_0_0.map"),
                new OmsiTileContent(
                    new OmsiTileSummary(true, 0, 0, 0),
                    Array.Empty<OmsiPlacedObject>(),
                    Array.Empty<OmsiPlacedSpline>(),
                    new OmsiTerrainGrid(
                        1,
                        [0, 10, 20, 30])));

        var height =
            NativeTerrainSampler.GetHeightAtLocalPoint(
                tile,
                150,
                150);

        Assert.InRange(height, 14.999, 15.001);
    }

    [Fact]
    public void TerrainBuilderCreatesTwoTrianglesPerCell()
    {
        var tile =
            new NativeSceneTile(
                new OmsiTileReference(0, 0, "tile_0_0.map"),
                new OmsiTileContent(
                    new OmsiTileSummary(true, 0, 0, 0),
                    Array.Empty<OmsiPlacedObject>(),
                    Array.Empty<OmsiPlacedSpline>(),
                    new OmsiTerrainGrid(
                        1,
                        [0, 10, 20, 30])));

        var scene =
            new NativeSceneSnapshot(
                [tile],
                Array.Empty<NativeObjectEntity>(),
                Array.Empty<NativeSplineEntity>(),
                [
                    new NativeTerrainEntity(
                        tile.Reference,
                        tile.Content.Terrain!)
                ]);

        var geometry =
            new NativeTerrainTriangleGeometryBuilder().Build(scene);

        Assert.Equal(2, geometry.TriangleCount);
        Assert.Equal(6, geometry.Vertices.Length);
    }
}
