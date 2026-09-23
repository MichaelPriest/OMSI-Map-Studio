using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Models;
using MapStudio.Core.Omsi.Scenery;
using MapStudio.Core.Omsi.Splines;
using MapStudio.Core.ProtonBus;
using Xunit;

namespace MapStudio.ProtonBus.Tests;

public sealed class
    ProtonBusOmsiTileExportBuilderTests
{
    [Fact]
    public void BuilderCombinesTerrainSplineAndScenery()
    {
        var tile =
            new OmsiTileReference(
                1,
                2,
                "tile.map");

        var spline =
            CreateSpline(
                10);

        var placedObject =
            CreateObject(
                20);

        var content =
            new OmsiTileContent(
                new(
                    true,
                    ObjectCount: 1,
                    SplineCount: 1,
                    SplineAttachmentCount: 0,
                    TerrainMarkerPresent:
                        true,
                    TerrainFileExists:
                        true),
                [
                    placedObject
                ],
                [
                    spline
                ],
                Terrain:
                    new(
                        1,
                        [
                            0,
                            0,
                            0,
                            0
                        ]));

        var result =
            ProtonBusOmsiTileExportBuilder
                .Build(
                    tile,
                    content,
                    new Dictionary<
                        string,
                        OmsiSplineDefinition>(
                            StringComparer
                                .OrdinalIgnoreCase)
                    {
                        [
                            "Splines/road.sli"
                        ] =
                            CreateSplineDefinition()
                    },
                    new Dictionary<
                        string,
                        ProtonBusResolvedSceneryAsset>(
                            StringComparer
                                .OrdinalIgnoreCase)
                    {
                        [
                            "Sceneryobjects/test.sco"
                        ] =
                            new(
                                [
                                    new(
                                        CreateGeometry(),
                                        OmsiSceneryMeshTransform
                                            .Identity,
                                        MeshOrdinal:
                                            0)
                                ])
                    });

        Assert.Equal(
            1,
            result.TerrainMeshCount);

        Assert.Equal(
            1,
            result.SplineMeshCount);

        Assert.Equal(
            1,
            result.SceneryMeshCount);

        Assert.Equal(
            3,
            result.Scene.Meshes.Count);

        Assert.False(
            result.HasMissingAssets);

        Assert.Empty(
            result.MissingSplineDefinitions);

        Assert.Empty(
            result.MissingSceneryAssets);
    }

    [Fact]
    public void BuilderReportsMissingAssetsOnce()
    {
        var tile =
            new OmsiTileReference(
                0,
                0,
                "tile.map");

        var content =
            new OmsiTileContent(
                new(
                    true,
                    ObjectCount: 2,
                    SplineCount: 2,
                    SplineAttachmentCount: 0),
                [
                    CreateObject(
                        1),
                    CreateObject(
                        2)
                ],
                [
                    CreateSpline(
                        1),
                    CreateSpline(
                        2)
                ]);

        var result =
            ProtonBusOmsiTileExportBuilder
                .Build(
                    tile,
                    content,
                    new Dictionary<
                        string,
                        OmsiSplineDefinition>(),
                    new Dictionary<
                        string,
                        ProtonBusResolvedSceneryAsset>());

        Assert.True(
            result.HasMissingAssets);

        Assert.Equal(
            [
                @"Splines\road.sli"
            ],
            result.MissingSplineDefinitions);

        Assert.Equal(
            [
                @"Sceneryobjects\test.sco"
            ],
            result.MissingSceneryAssets);

        Assert.Empty(
            result.Scene.Meshes);
    }


    [Fact]
    public void BuilderAppliesTerrainHeightOnlyToRelativeObjects()
    {
        var tile =
            new OmsiTileReference(
                0,
                0,
                "tile.map");

        var placedObject =
            new OmsiPlacedObject(
                HeaderValue:
                    "object",
                SceneryObjectPath:
                    @"Sceneryobjects\test.sco",
                ObjectId: 9,
                X: 150,
                Y: 150,
                Z: 2,
                Rotation: 0,
                Pitch: 0,
                Bank: 0,
                ExtraValues: []);

        var content =
            new OmsiTileContent(
                new(
                    true,
                    ObjectCount: 1,
                    SplineCount: 0,
                    SplineAttachmentCount: 0,
                    TerrainMarkerPresent:
                        true,
                    TerrainFileExists:
                        true),
                [
                    placedObject
                ],
                [],
                Terrain:
                    new(
                        1,
                        [
                            10,
                            10,
                            10,
                            10
                        ]));

        ProtonBusResolvedSceneryAsset CreateAsset(
            bool absolute) =>
            new(
                [
                    new(
                        CreateGeometry(),
                        OmsiSceneryMeshTransform
                            .Identity,
                        MeshOrdinal:
                            0)
                ],
                UsesAbsoluteHeight:
                    absolute);

        ProtonBusOmsiTileExportResult Build(
            bool absolute) =>
            ProtonBusOmsiTileExportBuilder
                .Build(
                    tile,
                    content,
                    new Dictionary<
                        string,
                        OmsiSplineDefinition>(),
                    new Dictionary<
                        string,
                        ProtonBusResolvedSceneryAsset>
                    {
                        [
                            @"Sceneryobjects\test.sco"
                        ] =
                            CreateAsset(
                                absolute)
                    });

        var relativeObject =
            Assert.Single(
                Build(
                    absolute:
                        false)
                    .Scene
                    .Meshes,
                mesh =>
                    mesh.Name.StartsWith(
                        "object_",
                        StringComparison.Ordinal));

        var absoluteObject =
            Assert.Single(
                Build(
                    absolute:
                        true)
                    .Scene
                    .Meshes,
                mesh =>
                    mesh.Name.StartsWith(
                        "object_",
                        StringComparison.Ordinal));

        Assert.Equal(
            12,
            relativeObject
                .Vertices[0]
                .Position.Y,
            precision: 4);

        Assert.Equal(
            2,
            absoluteObject
                .Vertices[0]
                .Position.Y,
            precision: 4);
    }

    [Fact]
    public void BuilderPropagatesColliderFlagFromResolvedMesh()
    {
        var tile =
            new OmsiTileReference(
                0,
                0,
                "tile.map");

        var content =
            new OmsiTileContent(
                new(
                    true,
                    ObjectCount: 1,
                    SplineCount: 0,
                    SplineAttachmentCount: 0),
                [
                    CreateObject(
                        5)
                ],
                []);

        var result =
            ProtonBusOmsiTileExportBuilder
                .Build(
                    tile,
                    content,
                    new Dictionary<
                        string,
                        OmsiSplineDefinition>(),
                    new Dictionary<
                        string,
                        ProtonBusResolvedSceneryAsset>
                    {
                        [
                            @"Sceneryobjects\test.sco"
                        ] =
                            new(
                                [
                                    new(
                                        CreateGeometry(),
                                        OmsiSceneryMeshTransform
                                            .Identity,
                                        MeshOrdinal:
                                            0,
                                        GenerateCollider:
                                            true)
                                ])
                    });

        var mesh =
            Assert.Single(
                result.Scene.Meshes);

        Assert.Contains(
            ProtonBusMeshNameTags
                .Collider,
            mesh.Name,
            StringComparison.Ordinal);
    }

    private static OmsiPlacedSpline
        CreateSpline(
            int id) =>
        new(
            HeaderValue:
                "spline",
            SplinePath:
                @"Splines\road.sli",
            SplineId:
                id,
            PreviousSplineId:
                -1,
            NextSplineId:
                -1,
            X: 0,
            Z: 0,
            Y: 0,
            Rotation: 0,
            Length: 10,
            Radius: 0,
            GradientStart: 0,
            GradientEnd: 0,
            IsHeightSpline:
                false,
            ExtraValues: []);

    private static OmsiSplineDefinition
        CreateSplineDefinition() =>
        new(
            true,
            [
                "road.dds"
            ],
            [
                new(
                    0,
                    "road.dds",
                    0,
                    new(
                        -3,
                        0,
                        0,
                        1),
                    new(
                        3,
                        0,
                        1,
                        1))
            ]);

    private static OmsiPlacedObject
        CreateObject(
            int id) =>
        new(
            HeaderValue:
                "object",
            SceneryObjectPath:
                @"Sceneryobjects\test.sco",
            ObjectId:
                id,
            X: 0,
            Y: 0,
            Z: 0,
            Rotation: 0,
            Pitch: 0,
            Bank: 0,
            ExtraValues: []);

    private static OmsiO3dGeometry
        CreateGeometry() =>
        new(
            IsLoaded: true,
            ErrorCode: null,
            Positions:
            [
                0,
                0,
                0,
                1,
                0,
                0,
                0,
                0,
                1
            ],
            Normals:
            [
                0,
                1,
                0,
                0,
                1,
                0,
                0,
                1,
                0
            ],
            Uvs:
            [
                0,
                0,
                1,
                0,
                0,
                1
            ],
            Indices:
            [
                0,
                1,
                2
            ],
            TriangleMaterialIndices:
            [
                0
            ],
            Materials:
            [
                new(
                    DiffuseR: 1,
                    DiffuseG: 1,
                    DiffuseB: 1,
                    DiffuseA: 1,
                    SpecularR: 0,
                    SpecularG: 0,
                    SpecularB: 0,
                    EmissionR: 0,
                    EmissionG: 0,
                    EmissionB: 0,
                    SpecularPower: 0,
                    TextureName:
                        "object.dds")
            ]);
}
