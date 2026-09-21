using MapStudio.Core.AI;
using MapStudio.Core.Generation.Buildings;
using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Omsi.Buildings;
using MapStudio.Core.Omsi.Models;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioFootprintBuildingAssetGeneratorTests
{
    [Fact]
    public void GeometryPreservesConcaveFootprint()
    {
        var building =
            new MapStudioProjectedBuildingFootprint(
                "building-l",
                [
                    new(0, 0),
                    new(12, 0),
                    new(12, 4),
                    new(5, 4),
                    new(5, 10),
                    new(0, 10)
                ],
                new(5.0, 4.0),
                "commercial",
                "L Building",
                3,
                9,
                MapStudioBuildingRoofType.Flat,
                0,
                null,
                null);

        var geometry =
            new MapStudioFootprintBuildingAssetGenerator()
                .BuildGeometry(
                    building);

        Assert.True(
            geometry.IsLoaded);

        Assert.True(
            geometry.Positions.Length >
            0);

        Assert.True(
            geometry.Indices.Length >
            30);

        Assert.Equal(
            2,
            geometry.Materials.Count);

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

    [Theory]
    [InlineData(MapStudioBuildingRoofType.Gable)]
    [InlineData(MapStudioBuildingRoofType.Hip)]
    [InlineData(MapStudioBuildingRoofType.Shed)]
    public void QuadrilateralRoofShapeRaisesGeometryAboveWall(
        MapStudioBuildingRoofType roofType)
    {
        var building =
            new MapStudioProjectedBuildingFootprint(
                "roof-test",
                [
                    new(0, 0),
                    new(12, 0),
                    new(12, 8),
                    new(0, 8)
                ],
                new(6, 4),
                "house",
                null,
                2,
                6,
                roofType,
                2,
                null,
                null);

        var geometry =
            new MapStudioFootprintBuildingAssetGenerator()
                .BuildGeometry(
                    building);

        var maximumY =
            geometry.Positions
                .Where(
                    (_, index) =>
                        index %
                            3 ==
                        1)
                .Max();

        Assert.InRange(
            maximumY,
            7.99f,
            8.01f);
    }

    [Fact]
    public async Task GeneratorWritesFootprintScoAndO3d()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-OsmBuilding-" +
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            var building =
                new MapStudioProjectedBuildingFootprint(
                    "osm-building-10",
                    [
                        new(100, 100),
                        new(110, 100),
                        new(110, 108),
                        new(100, 108)
                    ],
                    new(105, 104),
                    "house",
                    "Casa Real",
                    2,
                    6,
                    MapStudioBuildingRoofType.Gable,
                    2,
                    "Rua Teste",
                    "1");

            var result =
                await new MapStudioFootprintBuildingAssetGenerator()
                    .GenerateAsync(
                        root,
                        building);

            Assert.True(
                File.Exists(
                    result.SceneryObjectPath));

            Assert.True(
                File.Exists(
                    result.MeshPath));

            Assert.Equal(
                105,
                result.WorldCenter.X);

            Assert.Equal(
                104,
                result.WorldCenter.Z);

            var geometry =
                new OmsiO3dGeometryReader()
                    .Read(
                        result.MeshPath);

            Assert.True(
                geometry.IsLoaded);
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

    [Fact]
    public void SelfIntersectingFootprintDoesNotProduceInvalidRoof()
    {
        var building =
            new MapStudioProjectedBuildingFootprint(
                "bowtie",
                [
                    new(0, 0),
                    new(10, 10),
                    new(0, 10),
                    new(10, 0)
                ],
                new(5, 5),
                "yes",
                null,
                1,
                3,
                MapStudioBuildingRoofType.Flat,
                0,
                null,
                null);

        Assert.Throws<
            InvalidDataException>(
                () =>
                    new MapStudioFootprintBuildingAssetGenerator()
                        .BuildGeometry(
                            building));
    }
}
