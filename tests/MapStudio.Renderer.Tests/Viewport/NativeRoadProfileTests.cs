using MapStudio.Core.Omsi.Maps;
using MapStudio.Renderer.Picking;
using MapStudio.Renderer.Scene;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeRoadProfileTests
{
    private static NativeSplineEntity Spline(double length) => new(
        new PickingId(PickingKind.Spline, 42), new OmsiTileReference(0, 0, "tile.map"),
        new OmsiPlacedSpline("0", "road.sli", 42, -1, -1, 0, 0, 2, 0,
            length, 0, 0, 10, false, []), 0, 2, 0);

    [Fact]
    public void SamplesVariableGradientAndDoesNotInventTerrain()
    {
        var spline = Spline(100);
        var scene = new NativeSceneSnapshot([], [], [spline], []);
        var profile = NativeRoadProfileSampler.Build(scene, spline)!;
        Assert.Equal(51, profile.Points.Count);
        Assert.Equal(0, profile.Points[0].Distance);
        Assert.Equal(100, profile.Points[^1].Distance);
        Assert.Equal(5, profile.Rise, 3);
        Assert.Equal(10, profile.MaxAbsoluteGradient, 3);
        Assert.Equal(5, profile.Points[25].Gradient, 3);
        Assert.All(profile.Points, p => Assert.Null(p.TerrainHeight));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsInvalidLength(double length)
    {
        var spline = Spline(length);
        Assert.Null(NativeRoadProfileSampler.Build(new NativeSceneSnapshot([], [], [spline], []), spline));
    }
}
