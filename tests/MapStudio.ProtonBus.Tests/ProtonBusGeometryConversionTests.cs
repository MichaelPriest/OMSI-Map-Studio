using System.Numerics;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Splines;
using MapStudio.Core.ProtonBus;
using Xunit;

namespace MapStudio.ProtonBus.Tests;

public sealed class ProtonBusGeometryConversionTests
{
    [Fact]
    public void CoordinateConversionSwapsRuntimeYAndZ()
    {
        var converted =
            ProtonBusCoordinateSpace
                .ToProton3ds(
                    new Vector3(
                        10,
                        20,
                        30));

        Assert.Equal(
            new Vector3(
                10,
                30,
                20),
            converted);
    }

    [Fact]
    public void CoordinateConversionFlipsTriangleWinding()
    {
        var triangle =
            new ProtonBusExportTriangle(
                1,
                2,
                3,
                "road");

        var converted =
            ProtonBusCoordinateSpace
                .ToProton3ds(
                    triangle);

        Assert.Equal(
            1,
            converted.A);

        Assert.Equal(
            3,
            converted.B);

        Assert.Equal(
            2,
            converted.C);

        Assert.Equal(
            "road",
            converted.MaterialName);
    }

    [Fact]
    public void TexturePlannerCreatesPortablePngName()
    {
        Assert.Equal(
            "asfalto_principal.png",
            ProtonBusTextureNamePlanner
                .ToPortablePngName(
                    @"texture\asfalto principal.dds",
                    "fallback"));

        Assert.Equal(
            "S_o_Paulo.png",
            ProtonBusTextureNamePlanner
                .ToPortablePngName(
                    "São Paulo.bmp",
                    "fallback"));
    }

    [Fact]
    public void StraightSplineUsesTileWorldOriginAndProfileWidth()
    {
        var tile =
            new OmsiTileReference(
                2,
                -1,
                "tile_2_-1.map");

        var spline =
            CreateSpline(
                x: 20,
                y: 30,
                z: 5,
                rotation: 0,
                length: 10,
                radius: 0,
                gradientStart: 0,
                gradientEnd: 0);

        var definition =
            CreateRoadDefinition();

        var mesh =
            ProtonBusOmsiSplineTessellator
                .Build(
                    tile,
                    spline,
                    definition,
                    new(
                        MaximumSegmentLength:
                            5));

        Assert.Equal(
            8,
            mesh.Vertices.Count);

        Assert.Equal(
            4,
            mesh.Triangles.Count);

        Assert.Contains(
            ProtonBusMeshNameTags
                .Collider,
            mesh.Name,
            StringComparison.Ordinal);

        AssertVectorClose(
            new(
                616.5f,
                5,
                -270),
            mesh.Vertices[0]
                .Position);

        AssertVectorClose(
            new(
                623.5f,
                5,
                -270),
            mesh.Vertices[3]
                .Position);

        AssertVectorClose(
            new(
                616.5f,
                5,
                -265),
            mesh.Vertices[1]
                .Position);
    }

    [Fact]
    public void SplineInterpolatesGradientRise()
    {
        var tile =
            new OmsiTileReference(
                0,
                0,
                "tile.map");

        var spline =
            CreateSpline(
                x: 0,
                y: 0,
                z: 2,
                rotation: 0,
                length: 10,
                radius: 0,
                gradientStart: 10,
                gradientEnd: 10);

        var mesh =
            ProtonBusOmsiSplineTessellator
                .Build(
                    tile,
                    spline,
                    CreateRoadDefinition(),
                    new(
                        MaximumSegmentLength:
                            10));

        Assert.Equal(
            2,
            mesh.Vertices[0]
                .Position.Y,
            precision: 4);

        Assert.Equal(
            3,
            mesh.Vertices[1]
                .Position.Y,
            precision: 4);
    }

    [Fact]
    public void CurvedSplineFollowsOmsiRadiusGeometry()
    {
        var tile =
            new OmsiTileReference(
                0,
                0,
                "tile.map");

        var length =
            Math.PI *
            10 /
            2;

        var spline =
            CreateSpline(
                x: 0,
                y: 0,
                z: 0,
                rotation: 0,
                length: length,
                radius: 10,
                gradientStart: 0,
                gradientEnd: 0);

        var definition =
            new OmsiSplineDefinition(
                true,
                ["road.png"],
                [
                    new(
                        0,
                        "road.png",
                        0,
                        new(
                            0,
                            0,
                            0,
                            1),
                        new(
                            0,
                            0,
                            1,
                            1))
                ]);

        var mesh =
            ProtonBusOmsiSplineTessellator
                .Build(
                    tile,
                    spline,
                    definition,
                    new(
                        MaximumSegmentLength:
                            length));

        AssertVectorClose(
            new(
                10,
                0,
                10),
            mesh.Vertices[1]
                .Position);
    }

    [Fact]
    public void TerrainTessellatorBuildsTwoTrianglesPerCell()
    {
        var tile =
            new OmsiTileReference(
                2,
                -1,
                "tile_2_-1.map");

        var terrain =
            new OmsiTerrainGrid(
                1,
                [
                    1,
                    2,
                    3,
                    4
                ]);

        var mesh =
            ProtonBusOmsiTerrainTessellator
                .Build(
                    tile,
                    terrain,
                    new(
                        TextureFileName:
                            "ground.dds"));

        Assert.Equal(
            4,
            mesh.Vertices.Count);

        Assert.Equal(
            2,
            mesh.Triangles.Count);

        AssertVectorClose(
            new(
                600,
                1,
                -300),
            mesh.Vertices[0]
                .Position);

        AssertVectorClose(
            new(
                900,
                4,
                0),
            mesh.Vertices[3]
                .Position);

        Assert.Equal(
            "ground.png",
            Assert.Single(
                    mesh.Materials)
                .TextureFileName);

        Assert.Equal(
            new Vector2(
                1,
                1),
            mesh.Vertices[3]
                .TextureCoordinate);
    }

    [Fact]
    public void TerrainTessellatorRejectsInvalidHeightCount()
    {
        var terrain =
            new OmsiTerrainGrid(
                2,
                [
                    0,
                    0,
                    0
                ]);

        Assert.Throws<
            ArgumentException>(
                () =>
                    ProtonBusOmsiTerrainTessellator
                        .Build(
                            new(
                                0,
                                0,
                                "tile.map"),
                            terrain));
    }

    private static OmsiPlacedSpline
        CreateSpline(
            double x,
            double y,
            double z,
            double rotation,
            double length,
            double radius,
            double gradientStart,
            double gradientEnd) =>
        new(
            HeaderValue: "spline",
            SplinePath:
                @"Splines\road.sli",
            SplineId: 42,
            PreviousSplineId: -1,
            NextSplineId: -1,
            X: x,
            Z: z,
            Y: y,
            Rotation: rotation,
            Length: length,
            Radius: radius,
            GradientStart:
                gradientStart,
            GradientEnd:
                gradientEnd,
            IsHeightSpline: false,
            ExtraValues: []);

    private static OmsiSplineDefinition
        CreateRoadDefinition() =>
        new(
            true,
            ["road.dds"],
            [
                new(
                    0,
                    "road.dds",
                    0,
                    new(
                        -3.5,
                        0,
                        0,
                        1),
                    new(
                        3.5,
                        0,
                        1,
                        1))
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
