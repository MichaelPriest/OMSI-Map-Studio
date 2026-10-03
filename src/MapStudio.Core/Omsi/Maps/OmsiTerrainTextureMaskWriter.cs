using System.Buffers.Binary;

namespace MapStudio.Core.Omsi.Maps;

public static class OmsiTerrainTextureMaskWriter
{
    private const int DataOffset = 128;

    public static byte[] Write(
        OmsiTerrainTextureMaskData mask)
    {
        ArgumentNullException.ThrowIfNull(
            mask);

        if (
            mask.Width <= 0 ||
            mask.Height <= 0 ||
            mask.Width > 4096 ||
            mask.Height > 4096 ||
            mask.AlphaPixels.Length !=
                checked(
                    mask.Width *
                    mask.Height))
        {
            throw new InvalidDataException(
                "terrainMaskInvalidData");
        }

        var bytes =
            new byte[
                checked(
                    DataOffset +
                    mask.AlphaPixels
                        .Length)];

        var span =
            bytes.AsSpan();

        span[0] = (byte)'D';
        span[1] = (byte)'D';
        span[2] = (byte)'S';
        span[3] = (byte)' ';

        BinaryPrimitives
            .WriteUInt32LittleEndian(
                span.Slice(4, 4),
                124);

        BinaryPrimitives
            .WriteUInt32LittleEndian(
                span.Slice(8, 4),
                0x0000100F);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                span.Slice(12, 4),
                mask.Height);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                span.Slice(16, 4),
                mask.Width);

        BinaryPrimitives
            .WriteUInt32LittleEndian(
                span.Slice(20, 4),
                checked(
                    (uint)mask.Width));

        BinaryPrimitives
            .WriteUInt32LittleEndian(
                span.Slice(76, 4),
                32);

        BinaryPrimitives
            .WriteUInt32LittleEndian(
                span.Slice(80, 4),
                0x00000002);

        BinaryPrimitives
            .WriteUInt32LittleEndian(
                span.Slice(88, 4),
                8);

        BinaryPrimitives
            .WriteUInt32LittleEndian(
                span.Slice(104, 4),
                0xFF);

        BinaryPrimitives
            .WriteUInt32LittleEndian(
                span.Slice(108, 4),
                0x00001000);

        mask.AlphaPixels.CopyTo(
            span.Slice(
                DataOffset));

        return bytes;
    }
}
