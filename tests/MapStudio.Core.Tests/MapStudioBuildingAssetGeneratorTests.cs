using MapStudio.Core.AI;
using MapStudio.Core.Omsi.Buildings;
using MapStudio.Core.Omsi.Models;
using MapStudio.Core.Omsi.Scenery;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioBuildingAssetGeneratorTests
{
    [Fact]
    public void GeometryBuilderCreatesEditableGableBuilding()
    {
        var generator =
            new MapStudioBuildingAssetGenerator();

        var geometry =
            generator.BuildGeometry(
                new MapStudioBuildingSpec(
                    "House",
                    12,
                    9,
                    6,
                    2,
                    MapStudioBuildingRoofType
                        .Gable,
                    2.5));

        Assert.True(
            geometry.IsLoaded);

        Assert.Equal(
            4,
            geometry.Materials.Count);

        Assert.True(
            geometry.Positions.Length >
            0);

        Assert.True(
            geometry.Indices.Length >=
            36);

        using var stream =
            new MemoryStream();

        new OmsiO3dGeometryWriter()
            .Write(
                stream,
                geometry);

        Assert.True(
            stream.Length >
            0);
    }

    [Fact]
    public void GeometryBuilderAddsFacadeOpeningPanels()
    {
        var generator =
            new MapStudioBuildingAssetGenerator();

        var plain =
            generator.BuildGeometry(
                new MapStudioBuildingSpec(
                    "Plain",
                    12,
                    9,
                    6,
                    2,
                    MapStudioBuildingRoofType
                        .Flat));

        var detailed =
            generator.BuildGeometry(
                new MapStudioBuildingSpec(
                    "Detailed",
                    12,
                    9,
                    6,
                    2,
                    MapStudioBuildingRoofType
                        .Flat,
                    WindowsPerFloor:
                        3,
                    DoorCount:
                        1,
                    WindowWidthMeters:
                        1.2,
                    WindowHeightMeters:
                        1.1));

        Assert.True(
            detailed.Indices.Length >
            plain.Indices.Length);

        Assert.Contains(
            (ushort)2,
            detailed.TriangleMaterialIndices);

        Assert.Contains(
            (ushort)3,
            detailed.TriangleMaterialIndices);
    }

    [Theory]
    [InlineData(
        MapStudioBuildingRoofType.Hip)]
    [InlineData(
        MapStudioBuildingRoofType.Shed)]
    public void GeometryBuilderCreatesAdditionalSlopedRoofTypes(
        MapStudioBuildingRoofType roofType)
    {
        var geometry =
            new MapStudioBuildingAssetGenerator()
                .BuildGeometry(
                    new MapStudioBuildingSpec(
                        "Roof test",
                        12,
                        9,
                        6,
                        2,
                        roofType,
                        2));

        Assert.True(
            geometry.IsLoaded);

        Assert.True(
            geometry.Indices.Length >=
            36);

        Assert.Contains(
            (ushort)1,
            geometry.TriangleMaterialIndices);
    }

    [Fact]
    public async Task GeneratorWritesScoO3dAndBacksUpExistingAsset()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-Building-" +
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            Directory.CreateDirectory(
                root);

            var generator =
                new MapStudioBuildingAssetGenerator();

            var first =
                await generator
                    .GenerateAsync(
                        root,
                        new MapStudioBuildingSpec(
                            "Casa Teste",
                            10,
                            8,
                            6,
                            2,
                            MapStudioBuildingRoofType
                                .Flat));

            Assert.True(
                File.Exists(
                    first
                        .SceneryObjectPath));

            Assert.True(
                File.Exists(
                    first.MeshPath));

            var metadata =
                await new OmsiSceneryObjectReader()
                    .ReadMetadataAsync(
                        first
                            .SceneryObjectPath);

            Assert.True(
                metadata.Exists);

            Assert.Equal(
                "Casa Teste",
                metadata.FriendlyName);

            Assert.Contains(
                "building.o3d",
                metadata.MeshPaths);

            var mesh =
                new OmsiO3dGeometryReader()
                    .Read(
                        first.MeshPath);

            Assert.True(
                mesh.IsLoaded);

            Assert.NotNull(
                first.FacadeTexturePath);

            Assert.True(
                File.Exists(
                    first.FacadeTexturePath!));

            Assert.All(
                mesh.Materials,
                material =>
                    Assert.False(
                        string.IsNullOrWhiteSpace(
                            material.TextureName)));

            var second =
                await generator
                    .GenerateAsync(
                        root,
                        new MapStudioBuildingSpec(
                            "Casa Teste",
                            11,
                            9,
                            7,
                            2,
                            MapStudioBuildingRoofType
                                .Gable,
                            2));

            Assert.NotNull(
                second.BackupDirectory);

            Assert.True(
                Directory.Exists(
                    second.BackupDirectory));
        }
        finally
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
}
