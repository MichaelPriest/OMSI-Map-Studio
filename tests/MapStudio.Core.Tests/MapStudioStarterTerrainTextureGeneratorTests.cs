using System.Buffers.Binary;
using MapStudio.Core.Workspace;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioStarterTerrainTextureGeneratorTests
{
    [Fact]
    public async Task EnsureCreatesOriginalBaseAndDetailTextures()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-TerrainTexture-" +
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            var generator =
                new MapStudioStarterTerrainTextureGenerator();

            Assert.True(
                await generator
                    .EnsureAsync(
                        root));

            var basePath =
                Path.Combine(
                    root,
                    "Texture",
                    "mapstudio_grass.bmp");

            var detailPath =
                Path.Combine(
                    root,
                    "Texture",
                    "mapstudio_grass_detail.bmp");

            var baseBytes =
                await File
                    .ReadAllBytesAsync(
                        basePath);

            var detailBytes =
                await File
                    .ReadAllBytesAsync(
                        detailPath);

            Assert.Equal(
                (byte)'B',
                baseBytes[0]);

            Assert.Equal(
                (byte)'M',
                baseBytes[1]);

            Assert.Equal(
                256,
                BinaryPrimitives
                    .ReadInt32LittleEndian(
                        baseBytes.AsSpan(
                            18,
                            4)));

            Assert.Equal(
                128,
                BinaryPrimitives
                    .ReadInt32LittleEndian(
                        detailBytes.AsSpan(
                            18,
                            4)));

            Assert.False(
                baseBytes.SequenceEqual(
                    detailBytes));

            Assert.False(
                await generator
                    .EnsureAsync(
                        root));
        }
        finally
        {
            if (Directory.Exists(
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
