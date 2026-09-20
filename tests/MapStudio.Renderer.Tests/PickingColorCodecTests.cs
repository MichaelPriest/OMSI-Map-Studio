using MapStudio.Renderer.Picking;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class PickingColorCodecTests
{
    [Fact]
    public void EncodedIdRoundTripsThroughRgbaBytes()
    {
        var input =
            new PickingId(
                PickingKind.Object,
                0x0055AA);

        var encoded =
            PickingColorCodec.Encode(
                input);

        var r =
            (byte)(
                encoded &
                0xFF);

        var g =
            (byte)(
                (encoded >> 8) &
                0xFF);

        var b =
            (byte)(
                (encoded >> 16) &
                0xFF);

        var a =
            (byte)(
                (encoded >> 24) &
                0xFF);

        var reconstructed =
            (uint)(
                r |
                (g << 8) |
                (b << 16) |
                (a << 24));

        Assert.Equal(
            input,
            PickingColorCodec.Decode(
                reconstructed));
    }
}
