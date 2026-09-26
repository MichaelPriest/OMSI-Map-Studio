using System.Numerics;
using MapStudio.Renderer.Scene;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeOmsiModelSpaceTests
{
    [Fact]
    public void UvRestoresDirect3DSourceVOrientation()
    {
        var converted =
            NativeOmsiModelSpace
                .ToRendererUv(
                    new Vector2(
                        0.25f,
                        0.80f));

        Assert.InRange(
            converted.X,
            0.249999f,
            0.250001f);

        Assert.InRange(
            converted.Y,
            0.199999f,
            0.200001f);
    }
}
