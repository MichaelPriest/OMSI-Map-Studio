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

        Assert.Single(
            geometry.Materials);

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
                3,
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
                12,
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
                12);

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
                "ms_junction_asphalt.bmp",
                Assert.Single(
                    mesh.Materials)
                    .TextureName);

            Assert.True(
                File.Exists(
                    Path.Combine(
                        result.ObjectDirectory,
                        "Texture",
                        "ms_junction_asphalt.bmp")));
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
