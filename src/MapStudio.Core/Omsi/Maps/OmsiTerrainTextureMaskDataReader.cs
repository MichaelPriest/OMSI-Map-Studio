using System.Buffers.Binary;

namespace MapStudio.Core.Omsi.Maps;

public static class OmsiTerrainTextureMaskDataReader
{
    private const int DataOffset = 128;
    private const uint DdsHeaderSize = 124;
    private const uint PixelFormatSize = 32;
    private const uint DdpfAlpha = 0x00000002;
    private const int MaximumDimension = 4096;

    public static OmsiTerrainTextureMaskData Read(
        string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            path);

        using var stream =
            new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

        if (stream.Length < DataOffset)
        {
            throw new InvalidDataException(
                "terrainMaskTruncatedHeader");
        }

        var header =
            new byte[DataOffset];

        stream.ReadExactly(
            header);

        var span =
            header.AsSpan();

        if (
            span[0] != (byte)'D' ||
            span[1] != (byte)'D' ||
            span[2] != (byte)'S' ||
            span[3] != (byte)' ')
        {
            throw new InvalidDataException(
                "terrainMaskInvalidSignature");
        }

        var headerSize =
            BinaryPrimitives
                .ReadUInt32LittleEndian(
                    span.Slice(
                        4,
                        4));

        var height =
            BinaryPrimitives
                .ReadInt32LittleEndian(
                    span.Slice(
                        12,
                        4));

        var width =
            BinaryPrimitives
                .ReadInt32LittleEndian(
                    span.Slice(
                        16,
                        4));

        var pixelFormatSize =
            BinaryPrimitives
                .ReadUInt32LittleEndian(
                    span.Slice(
                        76,
                        4));

        var pixelFormatFlags =
            BinaryPrimitives
                .ReadUInt32LittleEndian(
                    span.Slice(
                        80,
                        4));

        var fourCc =
            BinaryPrimitives
                .ReadUInt32LittleEndian(
                    span.Slice(
                        84,
                        4));

        var bitCount =
            BinaryPrimitives
                .ReadUInt32LittleEndian(
                    span.Slice(
                        88,
                        4));

        var alphaMask =
            BinaryPrimitives
                .ReadUInt32LittleEndian(
                    span.Slice(
                        104,
                        4));

        if (
            headerSize !=
                DdsHeaderSize ||
            pixelFormatSize !=
                PixelFormatSize ||
            width <= 0 ||
            height <= 0 ||
            width > MaximumDimension ||
            height > MaximumDimension ||
            (pixelFormatFlags &
                DdpfAlpha) == 0 ||
            fourCc != 0 ||
            bitCount != 8 ||
            alphaMask != 0xFF)
        {
            throw new InvalidDataException(
                "terrainMaskUnsupportedFormat");
        }

        var pixelCount =
            checked(
                width *
                height);

        if (
            stream.Length <
            DataOffset +
                (long)pixelCount)
        {
            throw new InvalidDataException(
                "terrainMaskTruncatedPixels");
        }

        var alpha =
            new byte[pixelCount];

        stream.ReadExactly(
            alpha);

        return new OmsiTerrainTextureMaskData(
            width,
            height,
            alpha);
    }
}
