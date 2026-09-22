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
                    // Core preserves O3D geometry but stores V in its
                    // editor-normalized convention (1 - source V).
                    0, 1,
                    1, 1,
                    0.5f, 0
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
                        @"C:\OMSI\Sceneryobjects\Test\model\house.o3d",
                        OmsiSceneryMeshTransform
                            .Identity,
                        null,
                        geometry,
                        [@"C:\OMSI\Sceneryobjects\Test\Texture\house.dds"],
                        [@"C:\OMSI\Sceneryobjects\Test\Texture\house_night.dds"],
                        [@"C:\OMSI\Sceneryobjects\Test\Texture\house_light.dds"],
                        [1],
                        [true],
                        [true],
                        [@"C:\OMSI\Sceneryobjects\Test\Texture\house_trans.dds"],
                        [@"C:\OMSI\Sceneryobjects\Test\Texture\house_bump.dds"],
                        [0.05],
                        [@"C:\OMSI\Sceneryobjects\Test\Texture\house_env.dds"],
                        [0.4])
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
            result.MaterialBatches);

        var materialBatch =
            result.MaterialBatches[0];

        Assert.Equal(
            @"C:\OMSI\Sceneryobjects\Test\Texture\house.dds",
            materialBatch.TexturePath);

        Assert.Equal(
            @"C:\OMSI\Sceneryobjects\Test\Texture\house_night.dds",
            materialBatch.NightTexturePath);

        Assert.Equal(
            @"C:\OMSI\Sceneryobjects\Test\Texture\house_light.dds",
            materialBatch.LightTexturePath);

        Assert.Equal(
            1,
            materialBatch.AlphaMode);

        Assert.True(
            materialBatch.NoZWrite);

        Assert.True(
            materialBatch.NoZCheck);

        Assert.Equal(
            @"C:\OMSI\Sceneryobjects\Test\Texture\house_trans.dds",
            materialBatch.TransMapTexturePath);

        Assert.Equal(
            @"C:\OMSI\Sceneryobjects\Test\Texture\house_bump.dds",
            materialBatch.BumpTexturePath);

        Assert.Equal(
            0.05,
            materialBatch.BumpStrength);

        Assert.Equal(
            @"C:\OMSI\Sceneryobjects\Test\Texture\house_env.dds",
            materialBatch.EnvironmentTexturePath);

        Assert.Equal(
            0.4,
            materialBatch.EnvironmentStrength);

        Assert.Equal(
            new System.Numerics.Vector2(
                0,
                0),
            result.Vertices[0].TexCoord);

        Assert.Equal(
            new System.Numerics.Vector2(
                1,
                0),
            result.Vertices[1].TexCoord);

        Assert.All(
            result.Vertices,
            vertex =>
            {
                Assert.Equal(
                    new System.Numerics.Vector3(
                        0,
                        1,
                        0),
                    vertex.Normal);

                Assert.InRange(
                    vertex.Tangent.X,
                    0.999f,
                    1.001f);

                Assert.InRange(
                    Math.Abs(
                        vertex.Tangent.W),
                    0.999f,
                    1.001f);
            });

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
    [Fact]
    public void BuildLiftsOnSurfaceObjectVisuallyWithoutChangingPlacement()
    {
        var tile =
            new OmsiTileReference(
                0,
                0,
                "tile_0_0.map");

        var placed =
            new OmsiPlacedObject(
                "object",
                @"Sceneryobjects\Test\crossing.sco",
                20,
                X: 10,
                Y: 10,
                Z: 0,
                Rotation: 0,
                Pitch: 0,
                Bank: 0,
                ExtraValues:
                    Array.Empty<string>());

        var scene =
            new NativeSceneBuilder()
                .Build(
                    [
                        new NativeSceneTile(
                            tile,
                            new OmsiTileContent(
                                new OmsiTileSummary(
                                    true,
                                    1,
                                    0,
                                    0),
                                [placed],
                                Array.Empty<OmsiPlacedSpline>()))
                    ],
                    new PickingRegistry<object>());

        var geometry =
            new OmsiO3dGeometry(
                true,
                null,
                [
                    0, 0, 0,
                    1, 0, 0,
                    0, 0, 1
                ],
                [
                    0, 1, 0,
                    0, 1, 0,
                    0, 1, 0
                ],
                [],
                [0u, 1u, 2u],
                [0],
                [
                    new OmsiO3dMaterial(
                        1, 1, 1, 1,
                        0, 0, 0, 0,
                        0, 0, 0, null)
                ]);

        var asset =
            new NativeSceneryAsset(
                placed.SceneryObjectPath,
                @"C:\OMSI\Sceneryobjects\Test\crossing.sco",
                [
                    new NativeSceneryMeshAsset(
                        "crossing.o3d",
                        @"C:\OMSI\Sceneryobjects\Test\model\crossing.o3d",
                        OmsiSceneryMeshTransform.Identity,
                        null,
                        geometry,
                        [null])
                ],
                null,
                false,
                null,
                null,
                "on_surface");

        var result =
            new NativeObjectTriangleGeometryBuilder()
                .Build(
                    scene,
                    new Dictionary<string, NativeSceneryAsset>(
                        StringComparer.OrdinalIgnoreCase)
                    {
                        [placed.SceneryObjectPath] =
                            asset
                    });

        Assert.All(
            result.Vertices,
            vertex =>
                Assert.InRange(
                    vertex.Position.Y,
                    0.014f,
                    0.016f));

        Assert.Equal(
            0,
            placed.Z);
    }

    [Fact]
    public void BuildCreatesNativeTreeCrossBillboard()
    {
        var tile =
            new OmsiTileReference(
                0,
                0,
                "tile_0_0.map");

        var placed =
            new OmsiPlacedObject(
                "object",
                @"Sceneryobjects\Trees_MC\tree_medium_09.sco",
                5509,
                X: 150,
                Y: 150,
                Z: 2,
                Rotation: 15,
                Pitch: 0,
                Bank: 0,
                ExtraValues:
                [
                    "4",
                    "Tree_Medium_09.tga",
                    "12.996",
                    "1.438"
                ]);

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

        var asset =
            new NativeSceneryAsset(
                placed
                    .SceneryObjectPath,
                @"C:\OMSI\Sceneryobjects\Trees_MC\tree_medium_09.sco",
                Array.Empty<
                    NativeSceneryMeshAsset>(),
                new OmsiSceneryTreeDefinition(
                    "Tree_Medium_09.tga",
                    12,
                    18,
                    1.1,
                    1.5),
                false,
                null,
                @"C:\OMSI\Sceneryobjects\Trees_MC\Texture\Tree_Medium_09.tga");

        Assert.True(
            asset.IsLoaded);

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
            4,
            result.TriangleCount);

        Assert.Equal(
            1,
            result.LoadedObjectCount);

        var batch =
            Assert.Single(
                result.MaterialBatches);

        Assert.Equal(
            12,
            batch.VertexCount);

        Assert.Equal(
            1,
            batch.AlphaMode);

        Assert.True(
            batch.DoubleSided);

        Assert.Equal(
            asset.TreeTexturePath,
            batch.TexturePath);

        var range =
            Assert.Single(
                result.Ranges.Values);

        Assert.Equal(
            12,
            range.VertexCount);

        Assert.Contains(
            result.Vertices,
            vertex =>
                Math.Abs(
                    vertex.Position.Y -
                    (2.0f + 12.996f)) <
                0.001f);
    }

}
