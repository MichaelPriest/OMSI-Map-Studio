using MapStudio.Renderer.Viewport;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeTerrainBrushVisualMathTests
{
    [Fact]
    public void StrongerBrushProducesStrongerVisualOpacity()
    {
        var light = NativeTerrainBrushVisualMath.ResolveIntensityOpacity(0.25);
        var medium = NativeTerrainBrushVisualMath.ResolveIntensityOpacity(5);
        var strong = NativeTerrainBrushVisualMath.ResolveIntensityOpacity(100);

        Assert.InRange(light, 0.08, 0.38);
        Assert.True(medium > light);
        Assert.True(strong > medium);
        Assert.InRange(strong, 0.379, 0.381);
    }

    [Fact]
    public void InvalidIntensityFallsBackToSafeMinimum()
    {
        var invalid = NativeTerrainBrushVisualMath.ResolveIntensityOpacity(double.NaN);
        var minimum = NativeTerrainBrushVisualMath.ResolveIntensityOpacity(0.05);

        Assert.Equal(minimum, invalid, 6);
    }
}
