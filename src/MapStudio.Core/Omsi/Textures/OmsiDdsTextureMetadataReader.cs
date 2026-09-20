using System.Buffers.Binary;
using System.Text;

namespace MapStudio.Core.Omsi.Textures;

public static class OmsiDdsTextureMetadataReader
{
    private const uint DdsMagic = 0x20534444;
    private const uint AlphaPixelsFlag = 0x00000001;
    private const uint AlphaFlag = 0x00000002;
    private const uint FourCcFlag = 0x00000004;
    private const uint RgbFlag = 0x00000040;
    private const uint LuminanceFlag = 0x00020000;

    public static OmsiDdsTextureMetadata? TryRead(
        ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 128)
        {
            return null;
        }

        if (
            BinaryPrimitives
                .ReadUInt32LittleEndian(
                    bytes[..4]) !=
            DdsMagic)
        {
            return null;
        }

        var headerSize =
            BinaryPrimitives
                .ReadUInt32LittleEndian(
                    bytes.Slice(4, 4));

        var pixelFormatSize =
            BinaryPrimitives
                .ReadUInt32LittleEndian(
                    bytes.Slice(76, 4));

        if (
            headerSize != 124 ||
            pixelFormatSize != 32)
        {
            return null;
        }

        var height =
            BinaryPrimitives
                .ReadUInt32LittleEndian(
                    bytes.Slice(12, 4));

        var width =
            BinaryPrimitives
                .ReadUInt32LittleEndian(
                    bytes.Slice(16, 4));

        if (
            width == 0 ||
            height == 0 ||
            width > int.MaxValue ||
            height > int.MaxValue)
        {
            return null;
        }

        var pixelFlags =
            BinaryPrimitives
                .ReadUInt32LittleEndian(
                    bytes.Slice(80, 4));

        var fourCcBytes =
            bytes.Slice(84, 4);

        var fourCc =
            Encoding.ASCII.GetString(
                fourCcBytes);

        var rgbBits =
            BinaryPrimitives
                .ReadUInt32LittleEndian(
                    bytes.Slice(88, 4));

        var alphaOnly =
            (pixelFlags & AlphaFlag) != 0 &&
            (pixelFlags &
                (RgbFlag |
                 LuminanceFlag |
                 FourCcFlag)) == 0 &&
            rgbBits == 8;

        var format =
            alphaOnly
                ? "A8"
                : (pixelFlags &
                    FourCcFlag) != 0
                    ? fourCc.TrimEnd(
                        '\0',
                        ' ')
                    : (pixelFlags &
                        LuminanceFlag) != 0
                        ? $"L{rgbBits}"
                        : (pixelFlags &
                            RgbFlag) != 0
                            ? (pixelFlags &
                                AlphaPixelsFlag) != 0
                                ? $"RGBA{rgbBits}"
                                : $"RGB{rgbBits}"
                            : $"DDS-{rgbBits}";

        if (
            string.IsNullOrWhiteSpace(
                format))
        {
            format = "DDS";
        }

        return new OmsiDdsTextureMetadata(
            checked((int)width),
            checked((int)height),
            format,
            alphaOnly);
    }
}
