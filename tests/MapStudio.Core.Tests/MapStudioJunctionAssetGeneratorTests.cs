using MapStudio.Core.Omsi.Junctions;
using MapStudio.Core.Omsi.Models;
using MapStudio.Core.Omsi.Scenery;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioJunctionAssetGeneratorTests
{
    [Fact]
    public void GeometryBuilderCreatesRenderableJunctionSurface()
    {
        var generator =
            new MapStudioJunctionAssetGenerator();

        var geometry =
            generator.BuildGeometry(
                new MapStudioJunctionSpec(
                    "Cross",
                    [
                        new(
                            0,
                            7),
                        new(
                            90,
                            7),
                        new(
                            180,
                            7),
                        new(
                            270,
                            7)
                    ]));

        Assert.True(
            geometry.IsLoaded);

        Assert.Equal(
            3,
            geometry.Materials.Count);

        Assert.Contains(
            geometry.Materials,
            material =>
                string.Equals(
                    material.TextureName,
                    "ms_junction_asphalt.bmp",
                    StringComparison.OrdinalIgnoreCase));

        Assert.Contains(
            geometry.Materials,
            material =>
                string.Equals(
                    material.TextureName,
                    "ms_junction_marking.bmp",
                    StringComparison.OrdinalIgnoreCase));

        Assert.Contains(
            geometry.Materials,
            material =>
                string.Equals(
                    material.TextureName,
                    "ms_junction_sidewalk.bmp",
                    StringComparison.OrdinalIgnoreCase));

        Assert.InRange(
            geometry
                .TriangleMaterialIndices
                .Length,
            100,
            180);

        Assert.Equal(
            geometry
                .TriangleMaterialIndices
                .Length *
            3,
            geometry.Indices.Length);

        var maximumRadius =
            Enumerable
                .Range(
                    0,
                    geometry.Positions.Length /
                        3)
                .Select(
                    index =>
                    {
                        var x =
                            geometry.Positions[
                                index *
                                3];

                        var z =
                            geometry.Positions[
                                index *
                                3 +
                                2];

                        return Math.Sqrt(
                            x *
                                x +
                            z *
                                z);
                    })
                .Max();

        Assert.InRange(
            maximumRadius,
            4.0,
            6.5);
    }


    [Fact]
    public void GeometryBuilderCutsBackOutsideCrossCornersToRoadWidth()
    {
        var geometry =
            new MapStudioJunctionAssetGenerator()
                .BuildGeometry(
                    new MapStudioJunctionSpec(
                        "Cross fitted",
                        [
                            new(
                                0,
                                7),
                            new(
                                90,
                                7),
                            new(
                                180,
                                7),
                            new(
                                270,
                                7)
                        ]));

        Assert.Equal(
            0f,
            geometry.Positions[0]);

        Assert.Equal(
            0f,
            geometry.Positions[2]);

        var diagonal =
            Enumerable
                .Range(
                    1,
                    geometry.Positions.Length /
                        3 -
                    1)
                .Select(
                    index =>
                    {
                        var x =
                            geometry.Positions[
                                index *
                                3];

                        var z =
                            geometry.Positions[
                                index *
                                3 +
                                2];

                        return
                            (
                                X: x,
                                Z: z,
                                Difference:
                                    Math.Abs(
                                        x -
                                        z)
                            );
                    })
                .Where(
                    point =>
                        point.X >
                            0 &&
                        point.Z >
                            0)
                .OrderBy(
                    point =>
                        point.Difference)
                .First();

        Assert.InRange(
            diagonal.X,
            3.45f,
            3.55f);

        Assert.InRange(
            diagonal.Z,
            3.45f,
            3.55f);
    }




    [Fact]
    public void GeometryBuilderAddsSidewalkMouthSurfacesWhenRoadIsWiderThanCarriageway()
    {
        var geometry =
            new MapStudioJunctionAssetGenerator()
                .BuildGeometry(
                    new MapStudioJunctionSpec(
                        "Sidewalk cross",
                        [
                            new(
                                0,
                                11,
                                2,
                                false,
                                3.5),
                            new(
                                90,
                                11,
                                2,
                                false,
                                3.5),
                            new(
                                180,
                                11,
                                2,
                                false,
                                3.5),
                            new(
                                270,
                                11,
                                2,
                                false,
                                3.5)
                        ]));

        var sidewalkTriangles =
            geometry
                .TriangleMaterialIndices
                .Count(
                    material =>
                        material ==
                        2);

        Assert.Equal(
            16,
            sidewalkTriangles);
    }

    [Fact]
    public void GeometryBuilderDoesNotAddSidewalkOverlayWhenTotalWidthEqualsCarriageway()
    {
        var geometry =
            new MapStudioJunctionAssetGenerator()
                .BuildGeometry(
                    new MapStudioJunctionSpec(
                        "Road only",
                        [
                            new(
                                0,
                                7,
                                2,
                                false,
                                3.5),
                            new(
                                120,
                                7,
                                2,
                                false,
                                3.5),
                            new(
                                240,
                                7,
                                2,
                                false,
                                3.5)
                        ]));

        Assert.DoesNotContain(
            geometry
                .TriangleMaterialIndices,
            material =>
                material ==
                2);
    }


    [Fact]
    public void GeometryBuilderAddsLaneMouthMarkingStubs()
    {
        var geometry =
            new MapStudioJunctionAssetGenerator()
                .BuildGeometry(
                    new MapStudioJunctionSpec(
                        "Marked cross",
                        [
                            new(
                                0,
                                7,
                                2,
                                false,
                                3.5),
                            new(
                                90,
                                14,
                                4,
                                false,
                                3.5),
                            new(
                                180,
                                7,
                                2,
                                false,
                                3.5),
                            new(
                                270,
                                14,
                                4,
                                false,
                                3.5)
                        ]));

        var markingTriangles =
            geometry
                .TriangleMaterialIndices
                .Count(
                    material =>
                        material ==
                        1);

        // Two 2-lane mouths contribute one divider each and
        // two 4-lane mouths contribute three dividers each.
        // Every divider is a two-triangle quad.
        Assert.Equal(
            16,
            markingTriangles);

        Assert.All(
            Enumerable.Range(
                0,
                geometry
                    .Positions
                    .Length /
                3)
                .Where(
                    index =>
                        geometry
                            .Positions[
                                index *
                                3 +
                                1] >
                            0.102f),
            index =>
                Assert.InRange(
                    geometry.Positions[
                        index *
                        3 +
                        1],
                    0.103f,
                    0.107f));
    }

    [Fact]
    public async Task InternalPathsRespectInboundAndOutboundOneWayArms()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-Junction-Direction-" +
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            Directory.CreateDirectory(
                root);

            var result =
                await new MapStudioJunctionAssetGenerator()
                    .GenerateAsync(
                        root,
                        new MapStudioJunctionSpec(
                            "Directed T",
                            [
                                new(
                                    0,
                                    7,
                                    2,
                                    true,
                                    3.5,
                                    2,
                                    0),
                                new(
                                    120,
                                    7,
                                    2,
                                    true,
                                    3.5,
                                    0,
                                    2),
                                new(
                                    240,
                                    7,
                                    2,
                                    false,
                                    3.5,
                                    1,
                                    1)
                            ]));

            Assert.Equal(
                18,
                result.InternalPathCount);
        }
        finally
        {
            if (
                Directory.Exists(
                    root))
            {
                Directory.Delete(
                    root,
                    recursive:
                        true);
            }
        }
    }



    [Fact]
    public async Task MultipleDirectionalLanesProduceSeparateLaneCenteredMovements()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-Junction-Lanes-" +
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            Directory.CreateDirectory(
                root);

            var result =
                await new MapStudioJunctionAssetGenerator()
                    .GenerateAsync(
                        root,
                        new MapStudioJunctionSpec(
                            "Two lane turn",
                            [
                                new(
                                    0,
                                    7,
                                    2,
                                    true,
                                    3.5,
                                    2,
                                    0),
                                new(
                                    90,
                                    7,
                                    2,
                                    true,
                                    3.5,
                                    0,
                                    2),
                                new(
                                    180,
                                    7,
                                    2,
                                    true,
                                    3.5,
                                    0,
                                    0)
                            ]));

            Assert.Equal(
                8,
                result.InternalPathCount);

            var metadata =
                await new OmsiSceneryObjectReader()
                    .ReadMetadataAsync(
                        result
                            .SceneryObjectPath);

            Assert.Equal(
                8,
                metadata.Paths.Count);

            var firstLaneStart =
                metadata.Paths[0];

            var secondLaneStart =
                metadata.Paths[4];

            Assert.InRange(
                Math.Abs(
                    firstLaneStart.X -
                    secondLaneStart.X),
                3.49,
                3.51);

            Assert.InRange(
                Math.Abs(
                    firstLaneStart.Z -
                    secondLaneStart.Z),
                0,
                0.00001);

            Assert.NotEqual(
                firstLaneStart.Rotation,
                secondLaneStart.Rotation);
        }
        finally
        {
            if (
                Directory.Exists(
                    root))
            {
                Directory.Delete(
                    root,
                    recursive:
                        true);
            }
        }
    }

    [Fact]
    public async Task TurningMovementUsesConnectedBezierPathSegments()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-Junction-Curve-" +
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            Directory.CreateDirectory(
                root);

            var result =
                await new MapStudioJunctionAssetGenerator()
                    .GenerateAsync(
                        root,
                        new MapStudioJunctionSpec(
                            "Single turn",
                            [
                                new(
                                    0,
                                    7,
                                    2,
                                    true,
                                    3.5,
                                    1,
                                    0),
                                new(
                                    90,
                                    7,
                                    2,
                                    true,
                                    3.5,
                                    0,
                                    1),
                                new(
                                    180,
                                    7,
                                    2,
                                    true,
                                    3.5,
                                    0,
                                    0)
                            ]));

            Assert.Equal(
                4,
                result.InternalPathCount);

            var metadata =
                await new OmsiSceneryObjectReader()
                    .ReadMetadataAsync(
                        result
                            .SceneryObjectPath);

            Assert.Equal(
                4,
                metadata.Paths.Count);

            Assert.All(
                metadata.Paths,
                path =>
                    Assert.Equal(
                        0,
                        path.Radius,
                        6));

            for (
                var index = 0;
                index <
                    metadata.Paths.Count -
                    1;
                index++)
            {
                var current =
                    metadata.Paths[
                        index];

                var next =
                    metadata.Paths[
                        index +
                        1];

                var radians =
                    current.Rotation *
                    Math.PI /
                    180.0;

                var endX =
                    current.X +
                    Math.Sin(
                        radians) *
                    current.Length;

                var endZ =
                    current.Z +
                    Math.Cos(
                        radians) *
                    current.Length;

                Assert.InRange(
                    Math.Abs(
                        endX -
                        next.X),
                    0,
                    0.00001);

                Assert.InRange(
                    Math.Abs(
                        endZ -
                        next.Z),
                    0,
                    0.00001);
            }

            Assert.InRange(
                metadata.Paths[0]
                    .Rotation,
                160,
                180);

            Assert.InRange(
                metadata.Paths[^1]
                    .Rotation,
                90,
                110);
        }
        finally
        {
            if (
                Directory.Exists(
                    root))
            {
                Directory.Delete(
                    root,
                    recursive:
                        true);
            }
        }
    }

    [Fact]
    public async Task GeneratorWritesScoO3dAndInternalTrafficPaths()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-Junction-" +
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            Directory.CreateDirectory(
                root);

            var result =
                await new MapStudioJunctionAssetGenerator()
                    .GenerateAsync(
                        root,
                        new MapStudioJunctionSpec(
                            "Cross 4x",
                            [
                                new(
                                    0,
                                    7),
                                new(
                                    90,
                                    7),
                                new(
                                    180,
                                    7),
                                new(
                                    270,
                                    7)
                            ]));

            Assert.True(
                File.Exists(
                    result
                        .SceneryObjectPath));

            Assert.True(
                File.Exists(
                    result.MeshPath));

            Assert.Equal(
                36,
                result.InternalPathCount);

            var metadata =
                await new OmsiSceneryObjectReader()
                    .ReadMetadataAsync(
                        result
                            .SceneryObjectPath);

            Assert.True(
                metadata.Exists);

            Assert.Equal(
                "Cross 4x",
                metadata.FriendlyName);

            Assert.Equal(
                "junction.o3d",
                Assert.Single(
                    metadata.MeshPaths));

            Assert.True(
                metadata.Paths.Count ==
                36);

            Assert.All(
                metadata.Paths,
                path =>
                {
                    Assert.Equal(
                        0,
                        path.Type);

                    Assert.True(
                        path.Length >
                        0);

                    Assert.InRange(
                        path.Width,
                        2.5,
                        4.5);
                });

            var mesh =
                new OmsiO3dGeometryReader()
                    .Read(
                        result.MeshPath);

            Assert.True(
                mesh.IsLoaded);

            Assert.True(
                mesh.Indices.Length >
                0);

            Assert.Equal(
                3,
                mesh.Materials.Count);

            Assert.Contains(
                mesh.Materials,
                material =>
                    string.Equals(
                        material.TextureName,
                        "ms_junction_asphalt.bmp",
                        StringComparison.OrdinalIgnoreCase));

            Assert.Contains(
                mesh.Materials,
                material =>
                    string.Equals(
                        material.TextureName,
                        "ms_junction_marking.bmp",
                        StringComparison.OrdinalIgnoreCase));

            Assert.Contains(
                mesh.Materials,
                material =>
                    string.Equals(
                        material.TextureName,
                        "ms_junction_sidewalk.bmp",
                        StringComparison.OrdinalIgnoreCase));

            Assert.True(
                File.Exists(
                    Path.Combine(
                        result.ObjectDirectory,
                        "Texture",
                        "ms_junction_asphalt.bmp")));

            Assert.True(
                File.Exists(
                    Path.Combine(
                        result.ObjectDirectory,
                        "Texture",
                        "ms_junction_marking.bmp")));

            Assert.True(
                File.Exists(
                    Path.Combine(
                        result.ObjectDirectory,
                        "Texture",
                        "ms_junction_sidewalk.bmp")));
        }
        finally
        {
            if (
                Directory.Exists(
                    root))
            {
                Directory.Delete(
                    root,
                    recursive:
                        true);
            }
        }
    }
}
