using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Models;
using MapStudio.Core.ProtonBus;
using Xunit;

namespace MapStudio.ProtonBus.Tests;

public sealed class ProtonBusOmsiAssetResolverTests
{
    [Fact]
    public async Task ResolverLoadsSplineSceneryCollisionAndTextures()
    {
        var root =
            CreateRoot();

        try
        {
            var splinePath =
                Path.Combine(
                    root,
                    "Splines",
                    "Test",
                    "road.sli");

            WriteSpline(
                splinePath,
                "road.dds");

            var splineTexture =
                Path.Combine(
                    root,
                    "Splines",
                    "Test",
                    "Texture",
                    "road.dds");

            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    splineTexture)!);

            File.WriteAllBytes(
                splineTexture,
                [68, 68, 83, 32]);

            var scoPath =
                Path.Combine(
                    root,
                    "Sceneryobjects",
                    "Test",
                    "test.sco");

            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    scoPath)!);

            await File.WriteAllTextAsync(
                scoPath,
                string.Join(
                    Environment.NewLine,
                    [
                        "[friendlyname]",
                        "Test",
                        "[mesh]",
                        "triangle.o3d",
                        "[collision_mesh]",
                        "collision.o3d"
                    ]));

            var modelDirectory =
                Path.Combine(
                    root,
                    "Sceneryobjects",
                    "Test",
                    "model");

            Directory.CreateDirectory(
                modelDirectory);

            var writer =
                new OmsiO3dGeometryWriter();

            await writer.WriteAsync(
                Path.Combine(
                    modelDirectory,
                    "triangle.o3d"),
                CreateGeometry(
                    "building.png"));

            await writer.WriteAsync(
                Path.Combine(
                    modelDirectory,
                    "collision.o3d"),
                CreateGeometry(
                    null));

            var sceneryTexture =
                Path.Combine(
                    root,
                    "Sceneryobjects",
                    "Test",
                    "Texture",
                    "building.png");

            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    sceneryTexture)!);

            File.WriteAllBytes(
                sceneryTexture,
                [137, 80, 78, 71]);

            var content =
                new OmsiTileContent(
                    new(
                        true,
                        ObjectCount: 1,
                        SplineCount: 1,
                        SplineAttachmentCount: 0),
                    [
                        CreateObject()
                    ],
                    [
                        CreateSpline(
                            1,
                            @"Splines\Test\road.sli")
                    ]);

            var result =
                await new ProtonBusOmsiAssetResolver()
                    .ResolveAsync(
                        root,
                        content);

            Assert.False(
                result.HasBlockingIssues);

            Assert.True(
                result.SplineDefinitions
                    .ContainsKey(
                        @"Splines\Test\road.sli"));

            var asset =
                Assert.Single(
                    result.SceneryAssets)
                    .Value;

            Assert.Equal(
                2,
                asset.Meshes.Count);

            var collision =
                Assert.Single(
                    asset.Meshes,
                    mesh =>
                        mesh.GenerateCollider);

            Assert.True(
                collision.Invisible);

            Assert.Contains(
                result.Textures,
                texture =>
                    texture.TargetFileName ==
                        "road.png" &&
                    texture.RequiresConversion);

            Assert.Contains(
                result.Textures,
                texture =>
                    texture.TargetFileName ==
                        "building.png" &&
                    !texture.RequiresConversion);

            Assert.DoesNotContain(
                result.Issues,
                issue =>
                    issue.Code ==
                    "textureTargetCollision");
        }
        finally
        {
            DeleteRoot(
                root);
        }
    }

    [Fact]
    public async Task ResolverReportsTextureTargetCollision()
    {
        var root =
            CreateRoot();

        try
        {
            foreach (
                var pack
                in new[]
                {
                    "A",
                    "B"
                })
            {
                var splinePath =
                    Path.Combine(
                        root,
                        "Splines",
                        pack,
                        "road.sli");

                WriteSpline(
                    splinePath,
                    "road.dds");

                var texturePath =
                    Path.Combine(
                        root,
                        "Splines",
                        pack,
                        "Texture",
                        "road.dds");

                Directory.CreateDirectory(
                    Path.GetDirectoryName(
                        texturePath)!);

                File.WriteAllBytes(
                    texturePath,
                    pack == "A"
                        ? [1, 2, 3]
                        : [4, 5, 6]);
            }

            var content =
                new OmsiTileContent(
                    new(
                        true,
                        ObjectCount: 0,
                        SplineCount: 2,
                        SplineAttachmentCount: 0),
                    [],
                    [
                        CreateSpline(
                            1,
                            @"Splines\A\road.sli"),
                        CreateSpline(
                            2,
                            @"Splines\B\road.sli")
                    ]);

            var result =
                await new ProtonBusOmsiAssetResolver()
                    .ResolveAsync(
                        root,
                        content);

            Assert.True(
                result.HasBlockingIssues);

            Assert.Contains(
                result.Issues,
                issue =>
                    issue.Code ==
                    "textureTargetCollision");
        }
        finally
        {
            DeleteRoot(
                root);
        }
    }

    [Fact]
    public async Task ResolverReportsMissingSplineAndScenery()
    {
        var root =
            CreateRoot();

        try
        {
            var content =
                new OmsiTileContent(
                    new(
                        true,
                        ObjectCount: 1,
                        SplineCount: 1,
                        SplineAttachmentCount: 0),
                    [
                        CreateObject()
                    ],
                    [
                        CreateSpline(
                            1,
                            @"Splines\Missing\road.sli")
                    ]);

            var result =
                await new ProtonBusOmsiAssetResolver()
                    .ResolveAsync(
                        root,
                        content);

            Assert.True(
                result.HasBlockingIssues);

            Assert.Contains(
                result.Issues,
                issue =>
                    issue.Code ==
                    "splineMissing");

            Assert.Contains(
                result.Issues,
                issue =>
                    issue.Code ==
                    "scoMissing");
        }
        finally
        {
            DeleteRoot(
                root);
        }
    }

    private static void WriteSpline(
        string path,
        string textureName)
    {
        Directory.CreateDirectory(
            Path.GetDirectoryName(
                path)!);

        File.WriteAllText(
            path,
            string.Join(
                Environment.NewLine,
                [
                    "[texture]",
                    textureName,
                    "[profile]",
                    "0",
                    "[profilepnt]",
                    "-3",
                    "0",
                    "0",
                    "1",
                    "[profilepnt]",
                    "3",
                    "0",
                    "1",
                    "1"
                ]));
    }

    private static OmsiPlacedSpline
        CreateSpline(
            int id,
            string path) =>
        new(
            HeaderValue:
                "spline",
            SplinePath:
                path,
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

    private static OmsiPlacedObject
        CreateObject() =>
        new(
            HeaderValue:
                "object",
            SceneryObjectPath:
                @"Sceneryobjects\Test\test.sco",
            ObjectId: 1,
            X: 0,
            Y: 0,
            Z: 0,
            Rotation: 0,
            Pitch: 0,
            Bank: 0,
            ExtraValues: []);

    private static OmsiO3dGeometry
        CreateGeometry(
            string? textureName) =>
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
                        textureName)
            ]);

    private static string CreateRoot()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudioProtonBusResolverTests",
                Guid.NewGuid()
                    .ToString("N"));

        Directory.CreateDirectory(
            root);

        return root;
    }

    private static void DeleteRoot(
        string root)
    {
        if (
            Directory.Exists(
                root))
        {
            Directory.Delete(
                root,
                recursive: true);
        }
    }
}
