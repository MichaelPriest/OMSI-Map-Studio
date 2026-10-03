using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Scene;
using MapStudio.Core.Omsi.Structures;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioInfrastructureAssetGeneratorTests
{
    [Fact]
    public void GuardRailBuildsPhysicalMesh()
    {
        var feature =
            new MapStudioProjectedInfrastructureFeature(
                "guard-1",
                MapStudioOsmInfrastructureKind.GuardRail,
                [
                    new MapStudioRoadPoint(0, 0),
                    new MapStudioRoadPoint(12, 0)
                ],
                new MapStudioRoadPoint(6, 0),
                null,
                null,
                null,
                false);

        var geometry =
            new MapStudioInfrastructureAssetGenerator()
                .BuildGeometry(feature);

        Assert.True(geometry.IsLoaded);
        Assert.NotEmpty(geometry.Positions);
        Assert.NotEmpty(geometry.Indices);
        Assert.All(
            geometry.TriangleMaterialIndices,
            material =>
                Assert.Equal(
                    (ushort)1,
                    material));
    }

    [Fact]
    public void ParkingBuildsTriangulatedSurface()
    {
        var feature =
            new MapStudioProjectedInfrastructureFeature(
                "parking-1",
                MapStudioOsmInfrastructureKind.Parking,
                [
                    new MapStudioRoadPoint(0, 0),
                    new MapStudioRoadPoint(10, 0),
                    new MapStudioRoadPoint(10, 8),
                    new MapStudioRoadPoint(0, 8)
                ],
                new MapStudioRoadPoint(5, 4),
                "Parking",
                "asphalt",
                null,
                true);

        var geometry =
            new MapStudioInfrastructureAssetGenerator()
                .BuildGeometry(feature);

        Assert.Equal(
            6,
            geometry.Indices.Length);

        Assert.Equal(
            2,
            geometry.TriangleMaterialIndices.Length);

        Assert.All(
            geometry.TriangleMaterialIndices,
            material =>
                Assert.Equal(
                    (ushort)2,
                    material));
    }

    [Fact]
    public void SidewalkWidthControlsRibbonMesh()
    {
        var feature =
            new MapStudioProjectedInfrastructureFeature(
                "sidewalk-1",
                MapStudioOsmInfrastructureKind.Sidewalk,
                [
                    new MapStudioRoadPoint(0, 0),
                    new MapStudioRoadPoint(8, 0)
                ],
                new MapStudioRoadPoint(4, 0),
                null,
                "concrete",
                2.4,
                false);

        var geometry =
            new MapStudioInfrastructureAssetGenerator()
                .BuildGeometry(feature);

        var zValues =
            geometry.Positions
                .Where(
                    (_, index) =>
                        index % 3 == 2)
                .ToArray();

        Assert.Contains(
            zValues,
            value =>
                Math.Abs(
                    value -
                    1.2f) <
                0.01f);

        Assert.Contains(
            zValues,
            value =>
                Math.Abs(
                    value +
                    1.2f) <
                0.01f);
    }

    [Fact]
    public async Task GenerateAsyncCreatesOmsiAssetPackage()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-infrastructure-" +
                Guid.NewGuid().ToString("N"));

        try
        {
            var feature =
                new MapStudioProjectedInfrastructureFeature(
                    "wall-asset",
                    MapStudioOsmInfrastructureKind.Wall,
                    [
                        new MapStudioRoadPoint(100, 200),
                        new MapStudioRoadPoint(112, 200)
                    ],
                    new MapStudioRoadPoint(106, 200),
                    "Wall asset",
                    null,
                    null,
                    false);

            var result =
                await new MapStudioInfrastructureAssetGenerator()
                    .GenerateAsync(
                        root,
                        feature);

            Assert.True(
                File.Exists(
                    result.MeshPath));

            Assert.True(
                File.Exists(
                    result.SceneryObjectPath));

            Assert.True(
                File.Exists(
                    Path.Combine(
                        result.ObjectDirectory,
                        "mapstudio-osm-infrastructure.txt")));

            Assert.True(
                File.Exists(
                    Path.Combine(
                        result.ObjectDirectory,
                        "Texture",
                        "ms_infra_concrete.bmp")));

            Assert.Equal(
                feature.Center,
                result.WorldCenter);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }
}
