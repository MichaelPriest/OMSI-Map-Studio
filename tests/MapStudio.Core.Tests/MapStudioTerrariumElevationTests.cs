using MapStudio.Core.Generation.Terrain;
using Xunit;
namespace MapStudio.Core.Tests;
public sealed class MapStudioTerrariumElevationTests
{
    [Theory]
    [InlineData(128, 0, 0, 0)]
    [InlineData(130, 188, 128, 700.5)]
    [InlineData(127, 255, 0, -1)]
    public void DecodesTerrariumRgb(int r, int g, int b, double expected) =>
        Assert.Equal(expected, MapStudioTerrariumElevationSurfaceClient.DecodeHeight((byte)r, (byte)g, (byte)b));
}
