using MapStudio.Core.Omsi.Models;
using MapStudio.Core.Omsi.Scenery;
using MapStudio.Core.Omsi.Splines;
using MapStudio.Renderer.Scene;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class
    NativeAssetPreviewGeometryBuilderTests
{
    [Fact]
    public void SceneryPreviewUsesRealMeshCoordinates()
    {
        var geometry =
            new OmsiO3dGeometry(
                true,
                null,
                [
                    -2, -1, 0,
                    3, -1, 0,
                    0, 2, 4
                ],
                Array.Empty<float>(),
                Array.Empty<float>(),
                [0u, 1u, 2u],
                Array.Empty<ushort>(),
                Array.Empty<
                    OmsiO3dMaterial>());

        var asset =
            new NativeSceneryAsset(
                @"Sceneryobjects\Test\asset.sco",
                @"C:\OMSI\Sceneryobjects\Test\asset.sco",
                [
                    new NativeSceneryMeshAsset(
                        "model.o3d",
                        @"C:\OMSI\Sceneryobjects\Test\model\model.o3d",
                        OmsiSceneryMeshTransform
                            .Identity,
                        null,
                        geometry,
                        Array.Empty<string?>())
                ],
                null,
                false,
                null);

        var preview =
            new NativeAssetPreviewGeometryBuilder()
                .BuildScenery(
                    asset);

        Assert.True(
            preview.IsRenderable);

        Assert.Equal(
            1,
            preview.TriangleCount);

        Assert.Equal(
            -2,
            preview.Minimum.X);

        Assert.Equal(
            4,
            preview.Maximum.Y);
    }


    [Fact]
    public void TreePreviewBuildsCrossedBillboards()
    {
        var asset =
            new NativeSceneryAsset(
                @"Sceneryobjects\MapStudio_Vegetation\Starter_Tree\starter_tree.sco",
                @"C:\Workspace\Sceneryobjects\MapStudio_Vegetation\Starter_Tree\starter_tree.sco",
                [],
                new OmsiSceneryTreeDefinition(
                    "Texture\\starter_tree.tga",
                    8,
                    12,
                    0.45,
                    0.60),
                false,
                null,
                @"C:\Workspace\Sceneryobjects\MapStudio_Vegetation\Starter_Tree\Texture\starter_tree.tga");

        var preview =
            new NativeAssetPreviewGeometryBuilder()
                .BuildScenery(
                    asset);

        Assert.True(
            preview.IsRenderable);

        Assert.True(
            preview.TriangleCount >=
            8);

        Assert.Equal(
            1,
            preview.SourceMeshCount);

        Assert.True(
            preview.Maximum.Y >
            9);
    }

    [Fact]
    public void ModelPreviewUsesStandaloneO3dGeometry()
    {
        var geometry =
            new OmsiO3dGeometry(
                true,
                null,
                [
                    -3, -2, 0,
                     4, -2, 0,
                     0, 3, 5
                ],
                Array.Empty<float>(),
                Array.Empty<float>(),
                [0u, 1u, 2u],
                Array.Empty<ushort>(),
                Array.Empty<
                    OmsiO3dMaterial>());

        var preview =
            new NativeAssetPreviewGeometryBuilder()
                .BuildModel(
                    geometry);

        Assert.True(
            preview.IsRenderable);

        Assert.Equal(
            1,
            preview.SourceMeshCount);

        Assert.Equal(
            1,
            preview.TriangleCount);

        Assert.Equal(
            -3,
            preview.Minimum.X);

        Assert.Equal(
            5,
            preview.Maximum.Y);
    }

    [Fact]
    public void SplinePreviewExtrudesRealProfile()
    {
        var definition =
            new OmsiSplineDefinition(
                true,
                ["road.bmp"],
                [
                    new OmsiSplineSurface(
                        0,
                        "road.bmp",
                        0,
                        new OmsiSplineProfilePoint(
                            -4,
                            0,
                            0,
                            1),
                        new OmsiSplineProfilePoint(
                            4,
                            0.2,
                            1,
                            1))
                ]);

        var asset =
            new NativeSplineAsset(
                @"Splines\Test\road.sli",
                @"C:\OMSI\Splines\Test\road.sli",
                definition,
                [@"C:\OMSI\Splines\Test\Texture\road.dds"],
                null);

        var preview =
            new NativeAssetPreviewGeometryBuilder()
                .BuildSpline(
                    asset,
                    20);

        Assert.True(
            preview.IsRenderable);

        Assert.True(
            preview.TriangleCount >
            2);

        Assert.Equal(
            -4,
            preview.Minimum.X);

        Assert.Equal(
            20,
            preview.Maximum.Z);
    }
}
