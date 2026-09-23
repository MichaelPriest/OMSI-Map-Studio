using System.Numerics;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Models;
using MapStudio.Core.Omsi.Scenery;
using MapStudio.Core.ProtonBus;
using Xunit;

namespace MapStudio.ProtonBus.Tests;

public sealed class
    ProtonBusOmsiSceneryGeometryConverterTests
{
    [Fact]
    public void ConverterAppliesTileAndObjectTransform()
    {
        var mesh =
            ProtonBusOmsiSceneryGeometryConverter
                .Build(
                    new(
                        1,
                        2,
                        "tile.map"),
                    CreateObject(
                        x: 10,
                        y: 20,
                        z: 3,
                        rotation: 0),
                    CreateGeometry());

        AssertVectorClose(
            new(
                310,
                3,
                620),
            mesh.Vertices[0]
                .Position);

        AssertVectorClose(
            new(
                311,
                3,
                620),
            mesh.Vertices[1]
                .Position);

        AssertVectorClose(
            new(
                310,
                3,
                621),
            mesh.Vertices[2]
                .Position);
    }

    [Fact]
    public void ConverterAppliesLocalMeshTransform()
    {
        var mesh =
            ProtonBusOmsiSceneryGeometryConverter
                .Build(
                    new(
                        0,
                        0,
                        "tile.map"),
                    CreateObject(
                        0,
                        0,
                        0,
                        0),
                    CreateGeometry(),
                    new(
                        PositionX: 5,
                        PositionY: 2,
                        PositionZ: 7,
                        RotationX: 0,
                        RotationY: 0,
                        RotationZ: 0,
                        ScaleX: 2,
                        ScaleY: 2,
                        ScaleZ: 2));

        AssertVectorClose(
            new(
                5,
                2,
                7),
            mesh.Vertices[0]
                .Position);

        AssertVectorClose(
            new(
                7,
                2,
                7),
            mesh.Vertices[1]
                .Position);
    }

    [Fact]
    public void ConverterNormalizesOmsiTriangleWinding()
    {
        var mesh =
            ProtonBusOmsiSceneryGeometryConverter
                .Build(
                    new(
                        0,
                        0,
                        "tile.map"),
                    CreateObject(
                        0,
                        0,
                        0,
                        0),
                    CreateGeometry());

        var triangle =
            Assert.Single(
                mesh.Triangles);

        Assert.Equal(
            0,
            triangle.A);

        Assert.Equal(
            2,
            triangle.B);

        Assert.Equal(
            1,
            triangle.C);
    }

    [Fact]
    public void ConverterPlansPngMaterialAndFlags()
    {
        var geometry =
            CreateGeometry() with
            {
                Materials =
                [
                    new(
                        DiffuseR: 1,
                        DiffuseG: 1,
                        DiffuseB: 1,
                        DiffuseA: 0.5f,
                        SpecularR: 0,
                        SpecularG: 0,
                        SpecularB: 0,
                        EmissionR: 0.2f,
                        EmissionG: 0,
                        EmissionB: 0,
                        SpecularPower: 0,
                        TextureName:
                            "building wall.dds")
                ]
            };

        var mesh =
            ProtonBusOmsiSceneryGeometryConverter
                .Build(
                    new(
                        0,
                        0,
                        "tile.map"),
                    CreateObject(
                        0,
                        0,
                        0,
                        0),
                    geometry);

        var material =
            Assert.Single(
                mesh.Materials);

        Assert.Equal(
            "building_wall.png",
            material.TextureFileName);

        Assert.True(
            material.Transparent);

        Assert.True(
            material.Emissive);

        Assert.Equal(
            0.5f,
            material.Opacity,
            precision: 4);

        Assert.Equal(
            Vector3.One,
            material.DiffuseColor);

        Assert.Equal(
            material.Name,
            Assert.Single(
                    mesh.Triangles)
                .MaterialName);
    }

    [Fact]
    public void ConverterFlipsTextureVByDefault()
    {
        var mesh =
            ProtonBusOmsiSceneryGeometryConverter
                .Build(
                    new(
                        0,
                        0,
                        "tile.map"),
                    CreateObject(
                        0,
                        0,
                        0,
                        0),
                    CreateGeometry());

        Assert.Equal(
            new Vector2(
                0.25f,
                0.25f),
            mesh.Vertices[0]
                .TextureCoordinate);
    }

    [Fact]
    public void ConverterCanTagColliderMesh()
    {
        var mesh =
            ProtonBusOmsiSceneryGeometryConverter
                .Build(
                    new(
                        0,
                        0,
                        "tile.map"),
                    CreateObject(
                        0,
                        0,
                        0,
                        0),
                    CreateGeometry(),
                    options:
                        new(
                            GenerateCollider:
                                true));

        Assert.Contains(
            ProtonBusMeshNameTags
                .Collider,
            mesh.Name,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ConverterRejectsBrokenIndexBuffer()
    {
        var geometry =
            CreateGeometry() with
            {
                Indices =
                [
                    0,
                    1
                ]
            };

        Assert.Throws<
            ArgumentException>(
                () =>
                    ProtonBusOmsiSceneryGeometryConverter
                        .Build(
                            new(
                                0,
                                0,
                                "tile.map"),
                            CreateObject(
                                0,
                                0,
                                0,
                                0),
                            geometry));
    }

    private static OmsiPlacedObject
        CreateObject(
            double x,
            double y,
            double z,
            double rotation) =>
        new(
            HeaderValue:
                "object",
            SceneryObjectPath:
                @"Sceneryobjects\test.sco",
            ObjectId: 7,
            X: x,
            Y: y,
            Z: z,
            Rotation: rotation,
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
                0.25f,
                0.75f,
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
                        "building.dds")
            ]);

    private static void AssertVectorClose(
        Vector3 expected,
        Vector3 actual)
    {
        Assert.Equal(
            expected.X,
            actual.X,
            precision: 4);

        Assert.Equal(
            expected.Y,
            actual.Y,
            precision: 4);

        Assert.Equal(
            expected.Z,
            actual.Z,
            precision: 4);
    }
}
