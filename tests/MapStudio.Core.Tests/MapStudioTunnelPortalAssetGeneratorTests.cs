using Xunit;
using MapStudio.Core.Omsi.Structures;

namespace MapStudio.Core.Tests;

public sealed class MapStudioTunnelPortalAssetGeneratorTests
{
    [Fact]
    public void NormalizeClampsPortalDimensions()
    {
        var spec = new MapStudioTunnelPortalSpec(
            " portal ",
            100,
            2,
            0.01,
            20).Normalize();

        Assert.Equal("portal", spec.Name);
        Assert.Equal(30.0, spec.RoadWidthMeters);
        Assert.Equal(3.5, spec.ClearHeightMeters);
        Assert.Equal(0.2, spec.FrameThicknessMeters);
        Assert.Equal(4.0, spec.DepthMeters);
    }

    [Fact]
    public void BuildGeometryCreatesRealPortalFrame()
    {
        var geometry =
            new MapStudioTunnelPortalAssetGenerator()
                .BuildGeometry(
                    new MapStudioTunnelPortalSpec(
                        "MS_TunnelPortal_W080",
                        8.0));

        Assert.NotEmpty(geometry.Positions);
        Assert.NotEmpty(geometry.Indices);
        Assert.NotEmpty(geometry.Materials);
        Assert.Contains(
            geometry.Materials,
            material => string.Equals(
                material.TextureName,
                "ms_tunnel_concrete.bmp",
                StringComparison.OrdinalIgnoreCase));

        var xValues = geometry.Positions
            .Where((_, index) => index % 3 == 0)
            .ToArray();

        var yValues = geometry.Positions
            .Where((_, index) => index % 3 == 1)
            .ToArray();

        Assert.True(xValues.Max() - xValues.Min() > 9.0f);
        Assert.True(yValues.Max() > 4.5f);
    }
}
