using MapStudio.Core.Omsi.Models;
using MapStudio.Core.Omsi.Scenery;
using MapStudio.Core.Omsi.Structures;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioBridgePierAssetGeneratorTests
{
    [Fact]
    public void GeometryMatchesRequestedPierHeightAndCapWidth()
    {
        var geometry =
            new MapStudioBridgePierAssetGenerator()
                .BuildGeometry(
                    new MapStudioBridgePierSpec(
                        "Pier",
                        6.25,
                        1.8,
                        1.4,
                        9.0));

        Assert.True(
            geometry.IsLoaded);
        Assert.Equal(
            1,
            geometry.Materials.Count);
        Assert.Equal(
            "ms_bridge_concrete.bmp",
            geometry.Materials[0]
                .TextureName);

        var vertices =
            Enumerable.Range(
                0,
                geometry.Positions.Length / 3)
                .Select(
                    index =>
                        (
                            X:
                                geometry.Positions[
                                    index * 3],
                            Y:
                                geometry.Positions[
                                    index * 3 + 1]
                        ))
                .ToArray();

        Assert.InRange(
            vertices.Max(
                vertex =>
                    vertex.Y),
            6.249f,
            6.251f);

        Assert.InRange(
            vertices.Max(
                vertex =>
                    vertex.X) -
            vertices.Min(
                vertex =>
                    vertex.X),
            8.999f,
            9.001f);
    }

    [Fact]
    public async Task GeneratorWritesReusableScoO3dTextureAndManifest()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-BridgePier-" +
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            Directory.CreateDirectory(
                root);

            var generator =
                new MapStudioBridgePierAssetGenerator();
            var spec =
                new MapStudioBridgePierSpec(
                    "MS_BridgePier_H0500_W090",
                    5.0,
                    1.8,
                    1.4,
                    9.0);

            var first =
                await generator
                    .GenerateAsync(
                        root,
                        spec);
            var second =
                await generator
                    .GenerateAsync(
                        root,
                        spec);

            Assert.True(
                File.Exists(
                    first.SceneryObjectPath));
            Assert.True(
                File.Exists(
                    first.MeshPath));
            Assert.True(
                File.Exists(
                    first.TexturePath));
            Assert.Null(
                second.BackupDirectory);

            var metadata =
                await new OmsiSceneryObjectReader()
                    .ReadMetadataAsync(
                        first.SceneryObjectPath);

            Assert.True(
                metadata.Exists);
            Assert.Equal(
                "MS_BridgePier_H0500_W090",
                metadata.FriendlyName);
            Assert.Equal(
                "pier.o3d",
                Assert.Single(
                    metadata.MeshPaths));

            var geometry =
                new OmsiO3dGeometryReader()
                    .Read(
                        first.MeshPath);

            Assert.True(
                geometry.IsLoaded);
            Assert.True(
                geometry.Indices.Length > 0);
            Assert.Contains(
                geometry.Materials,
                material =>
                    string.Equals(
                        material.TextureName,
                        "ms_bridge_concrete.bmp",
                        StringComparison.OrdinalIgnoreCase));

            var manifest =
                await File.ReadAllTextAsync(
                    Path.Combine(
                        first.ObjectDirectory,
                        "mapstudio-bridge-pier.txt"));

            Assert.Contains(
                "Version=1.0.0",
                manifest,
                StringComparison.Ordinal);
            Assert.Contains(
                "Height=5.00",
                manifest,
                StringComparison.Ordinal);
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
