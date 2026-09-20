using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Models;
using MapStudio.Core.Omsi.Scenery;
using MapStudio.Renderer.Picking;
using MapStudio.Renderer.Scene;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeObjectTriangleGeometryBuilderTests
{
    [Fact]
    public void BuildKeepsRealO3dTriangleInWorldSpace()
    {
        var tile =
            new OmsiTileReference(
                0,
                0,
                "tile_0_0.map");

        var placed =
            new OmsiPlacedObject(
                "object",
                @"Sceneryobjects\Test\house.sco",
                10,
                X: 150,
                Y: 150,
                Z: 2,
                Rotation: 0,
                Pitch: 0,
                Bank: 0,
                ExtraValues:
                    Array.Empty<string>());

        var content =
            new OmsiTileContent(
                new OmsiTileSummary(
                    true,
                    1,
                    0,
                    0),
                [placed],
                Array.Empty<
                    OmsiPlacedSpline>());

        var scene =
            new NativeSceneBuilder()
                .Build(
                    [
                        new NativeSceneTile(
                            tile,
                            content)
                    ],
                    new PickingRegistry<
                        object>());

        var geometry =
            new OmsiO3dGeometry(
                true,
                null,
                [
                    -2, 0, -2,
                    2, 0, -2,
                    0, 0, 2
                ],
                [
                    0, 1, 0,
                    0, 1, 0,
                    0, 1, 0
                ],
                [
                    0, 0,
                    1, 0,
                    0.5f, 1
                ],
                [0u, 1u, 2u],
                [0],
                [
                    new OmsiO3dMaterial(
                        0.8f,
                        0.4f,
                        0.2f,
                        1,
                        0,
                        0,
                        0,
                        0,
                        0,
                        0,
                        0,
                        null)
                ]);

        var asset =
            new NativeSceneryAsset(
                placed
                    .SceneryObjectPath,
                @"C:\OMSI\Sceneryobjects\Test\house.sco",
                [
                    new NativeSceneryMeshAsset(
                        "house.o3d",
                        OmsiSceneryMeshTransform
                            .Identity,
                        null,
                        geometry)
                ],
                null,
                false,
                null);

        var result =
            new NativeObjectTriangleGeometryBuilder()
                .Build(
                    scene,
                    new Dictionary<
                        string,
                        NativeSceneryAsset>(
                            StringComparer
                                .OrdinalIgnoreCase)
                    {
                        [
                            placed
                                .SceneryObjectPath
                        ] = asset
                    });

        Assert.Equal(
            1,
            result.TriangleCount);

        Assert.Equal(
            1,
            result.LoadedObjectCount);

        Assert.Single(
            result.Ranges);

        var range =
            Assert.Single(
                result.Ranges.Values);

        Assert.Equal(
            0,
            range.StartVertex);

        Assert.Equal(
            3,
            range.VertexCount);

        Assert.All(
            result.Vertices,
            vertex =>
            {
                Assert.InRange(
                    vertex.Position.X,
                    148.0f,
                    152.0f);

                Assert.InRange(
                    vertex.Position.Y,
                    1.999f,
                    2.001f);

                Assert.InRange(
                    vertex.Position.Z,
                    148.0f,
                    152.0f);
            });
    }
}
