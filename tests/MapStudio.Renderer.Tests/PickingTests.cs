using MapStudio.Renderer.Picking;

namespace MapStudio.Renderer.Tests;

public sealed class PickingTests
{
    [Theory]
    [InlineData(PickingKind.Object, 1)]
    [InlineData(PickingKind.Object, 324)]
    [InlineData(PickingKind.Spline, 8)]
    [InlineData(PickingKind.Gizmo, 65535)]
    public void EncodeDecodeRoundTrips(
        PickingKind kind,
        int value)
    {
        var input =
            new PickingId(
                kind,
                value);

        Assert.Equal(
            input,
            PickingColorCodec.Decode(
                PickingColorCodec.Encode(
                    input)));
    }

    [Fact]
    public void RegistryResolvesAndReusesReleasedId()
    {
        var registry =
            new PickingRegistry<object>();

        var firstItem =
            new object();

        var first =
            registry.Register(
                PickingKind.Object,
                firstItem);

        Assert.True(
            registry.TryResolve(
                first,
                out var resolved));

        Assert.Same(
            firstItem,
            resolved);

        Assert.True(
            registry.Unregister(first));

        var second =
            registry.Register(
                PickingKind.Spline,
                new object());

        Assert.Equal(
            first.Value,
            second.Value);
    }
}
