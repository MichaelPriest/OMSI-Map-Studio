using System.Numerics;
using MapStudio.Renderer.Scene;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeOmsiModelSpaceTests
{
    [Fact]
    public void PositionConvertsOmsiZUpToRendererYUp()
    {
        var converted =
            NativeOmsiModelSpace
                .ToRendererPosition(
                    new Vector3(
                        2,
                        7,
                        11));

        Assert.Equal(
            new Vector3(
                2,
                11,
                7),
            converted);
    }

    [Fact]
    public void UvRestoresDirect3DSourceVOrientation()
    {
        var converted =
            NativeOmsiModelSpace
                .ToRendererUv(
                    new Vector2(
                        0.25f,
                        0.80f));

        Assert.Equal(
            new Vector2(
                0.25f,
                0.20f),
            converted);
    }

    [Fact]
    public void NormalConvertsOmsiUpToRendererUp()
    {
        var converted =
            NativeOmsiModelSpace
                .ToRendererNormal(
                    Vector3.UnitZ);

        Assert.Equal(
            Vector3.UnitY,
            converted);
    }

    [Fact]
    public void ConvertedTranslationUsesRendererAxes()
    {
        var source =
            Matrix4x4.CreateTranslation(
                3,
                5,
                9);

        var converted =
            NativeOmsiModelSpace
                .ToRendererTransform(
                    source);

        var position =
            Vector3.Transform(
                Vector3.Zero,
                converted);

        Assert.Equal(
            new Vector3(
                3,
                9,
                5),
            position);
    }

    [Fact]
    public void WindingIsReversedAfterHandednessChange()
    {
        Assert.Equal(
            0,
            NativeOmsiModelSpace
                .SourceCornerForRendererCorner(
                    0));

        Assert.Equal(
            2,
            NativeOmsiModelSpace
                .SourceCornerForRendererCorner(
                    1));

        Assert.Equal(
            1,
            NativeOmsiModelSpace
                .SourceCornerForRendererCorner(
                    2));
    }
}
