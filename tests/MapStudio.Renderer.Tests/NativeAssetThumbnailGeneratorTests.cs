using System.Numerics;
using MapStudio.Renderer.Scene;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeAssetThumbnailGeneratorTests
{
    [Fact]
    public void RendererProducesBmpFromRealPreviewTriangles()
    {
        var color =
            new Vector4(
                0.7f,
                0.3f,
                0.2f,
                1);

        var geometry =
            new NativeAssetPreviewGeometry(
                [
                    new NativeMapVertex(
                        new Vector3(
                            -1,
                            0,
                            0),
                        color),
                    new NativeMapVertex(
                        new Vector3(
                            1,
                            0,
                            0),
                        color),
                    new NativeMapVertex(
                        new Vector3(
                            0,
                            2,
                            0),
                        color)
                ],
                new Vector3(
                    -1,
                    0,
                    0),
                new Vector3(
                    1,
                    2,
                    0),
                1,
                null);

        var bytes =
            new NativeAssetThumbnailGenerator()
                .RenderBmp(
                    geometry,
                    96,
                    64);

        Assert.True(
            bytes.Length >
            54);

        Assert.Equal(
            (byte)'B',
            bytes[0]);

        Assert.Equal(
            (byte)'M',
            bytes[1]);

        Assert.Contains(
            bytes
                .Skip(54),
            value =>
                value >
                40);
    }

    [Fact]
    public void InvalidPreviewDoesNotCreateThumbnail()
    {
        var bytes =
            new NativeAssetThumbnailGenerator()
                .RenderBmp(
                    NativeAssetPreviewGeometry
                        .Error(
                            "noGeometry"));

        Assert.Empty(
            bytes);
    }
}
