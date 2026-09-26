using System.Buffers.Binary;
using MapStudio.Core.Workspace;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioStarterSkyGeneratorTests
{
    [Fact]
    public async Task EnsureCreatesDayAndNightSkiesWithoutOverwriting()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-Sky-" +
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            var generator =
                new MapStudioStarterSkyGenerator();

            Assert.True(
                await generator
                    .EnsureAsync(
                        root));

            var day =
                Path.Combine(
                    root,
                    "Texture",
                    "himmel01.bmp");

            var night =
                Path.Combine(
                    root,
                    "Texture",
                    "himmel05.bmp");

            Assert.True(
                File.Exists(
                    day));

            Assert.True(
                File.Exists(
                    night));

            var dayBytes =
                await File
                    .ReadAllBytesAsync(
                        day);

            var nightBytes =
                await File
                    .ReadAllBytesAsync(
                        night);

            Assert.Equal(
                (byte)'B',
                dayBytes[0]);

            Assert.Equal(
                (byte)'M',
                dayBytes[1]);

            Assert.Equal(
                512,
                BinaryPrimitives
                    .ReadInt32LittleEndian(
                        dayBytes.AsSpan(
                            18,
                            4)));

            Assert.Equal(
                256,
                BinaryPrimitives
                    .ReadInt32LittleEndian(
                        dayBytes.AsSpan(
                            22,
                            4)));

            Assert.NotEqual(
                dayBytes,
                nightBytes);

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
