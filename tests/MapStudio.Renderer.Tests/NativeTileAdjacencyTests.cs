using System.Linq;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Renderer.Picking;
using MapStudio.Renderer.Scene;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeTileAdjacencyTests
{
    [Fact]
    public void ThreeByThreeTerrainMeshesShareExactWorldEdges()
    {
        var tiles =
            (
                from tileY in Enumerable.Range(-1, 3)
                from tileX in Enumerable.Range(-1, 3)
                select new NativeSceneTile(
                    new OmsiTileReference(
                        tileX,
                        tileY,
                        $"tile_{tileX}_{tileY}.map"),
                    new OmsiTileContent(
                        new OmsiTileSummary(
                            true,
                            0,
                            0,
                            0),
                        Array.Empty<OmsiPlacedObject>(),
                        Array.Empty<OmsiPlacedSpline>(),
                        new OmsiTerrainGrid(
                            1,
                            new float[]
                            {
                                0,
                                0,
                                0,
                                0
                            })))
            )
            .ToArray();

        var scene =
            new NativeSceneBuilder()
                .Build(
                    tiles,
                    new PickingRegistry<object>());

        var geometry =
            new NativeTerrainTriangleGeometryBuilder()
                .Build(scene);

        Assert.Equal(
            tiles.Length,
            geometry.MaterialBatches.Count);

        var extents =
            tiles
                .Select(
                    (tile, index) =>
                    {
                        var batch =
                            geometry.MaterialBatches[index];

                        var vertices =
                            geometry.Vertices
                                .Skip(batch.StartVertex)
                                .Take(batch.VertexCount)
                                .ToArray();

                        return new
                        {
                            tile.Reference.X,
                            tile.Reference.Y,
                            MinX = vertices.Min(vertex => vertex.Position.X),
                            MaxX = vertices.Max(vertex => vertex.Position.X),
                            MinZ = vertices.Min(vertex => vertex.Position.Z),
                            MaxZ = vertices.Max(vertex => vertex.Position.Z)
                        };
                    })
                .ToDictionary(
                    item => (item.X, item.Y));

        foreach (var item in extents.Values)
        {
            var expected =
                OmsiTileGrid.GetBounds(
                    item.X,
                    item.Y);

            Assert.Equal((float)expected.MinX, item.MinX);
            Assert.Equal((float)expected.MaxX, item.MaxX);
            Assert.Equal((float)expected.MinZ, item.MinZ);
            Assert.Equal((float)expected.MaxZ, item.MaxZ);
        }

        for (var y = -1; y <= 1; y++)
        {
            for (var x = -1; x < 1; x++)
            {
                var current = extents[(x, y)];
                var right = extents[(x + 1, y)];

                Assert.Equal(current.MaxX, right.MinX);
                Assert.Equal(current.MinZ, right.MinZ);
                Assert.Equal(current.MaxZ, right.MaxZ);
            }
        }

        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y < 1; y++)
            {
                var current = extents[(x, y)];
                var next = extents[(x, y + 1)];

                Assert.Equal(current.MaxZ, next.MinZ);
                Assert.Equal(current.MinX, next.MinX);
                Assert.Equal(current.MaxX, next.MaxX);
            }
        }
    }
}
