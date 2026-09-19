using System.Buffers.Binary;

namespace MapStudio.Core.Omsi.Maps;

public sealed class OmsiTerrainTextureMaskReader
{
    private const uint DdsHeaderSize = 124;
    private const uint PixelFormatSize = 32;
    private const uint DdpfAlpha = 0x00000002;
    private const int DataOffset = 128;
    private const int MaximumDimension = 4096;

    public OmsiTerrainTextureMask Read(
        int layerIndex,
        string path)
    {
        ArgumentOutOfRangeException
            .ThrowIfLessThanOrEqual(
                layerIndex,
                0);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            path);

        var fileName =
            Path.GetFileName(path);

        if (!File.Exists(path))
        {
            return Error(
                layerIndex,
                fileName,
                0,
                "missingFile");
        }

        var fileSize =
            new FileInfo(path).Length;

        try
        {
            var bytes =
                File.ReadAllBytes(path);

            if (bytes.Length < DataOffset)
            {
                return Error(
                    layerIndex,
                    fileName,
                    fileSize,
                    "truncatedHeader");
            }

            if (
                bytes[0] != (byte)'D' ||
                bytes[1] != (byte)'D' ||
                bytes[2] != (byte)'S' ||
                bytes[3] != (byte)' ')
            {
                return Error(
                    layerIndex,
                    fileName,
                    fileSize,
                    "invalidSignature");
            }

            var headerSize =
                BinaryPrimitives
                    .ReadUInt32LittleEndian(
                        bytes.AsSpan(4, 4));

            var height =
                BinaryPrimitives
                    .ReadInt32LittleEndian(
                        bytes.AsSpan(12, 4));

            var width =
                BinaryPrimitives
                    .ReadInt32LittleEndian(
                        bytes.AsSpan(16, 4));

            var pixelFormatSize =
                BinaryPrimitives
                    .ReadUInt32LittleEndian(
                        bytes.AsSpan(76, 4));

            var pixelFormatFlags =
                BinaryPrimitives
                    .ReadUInt32LittleEndian(
                        bytes.AsSpan(80, 4));

            var fourCc =
                BinaryPrimitives
                    .ReadUInt32LittleEndian(
                        bytes.AsSpan(84, 4));

            var bitCount =
                BinaryPrimitives
                    .ReadUInt32LittleEndian(
                        bytes.AsSpan(88, 4));

            var redMask =
                BinaryPrimitives
                    .ReadUInt32LittleEndian(
                        bytes.AsSpan(92, 4));

            var greenMask =
                BinaryPrimitives
                    .ReadUInt32LittleEndian(
                        bytes.AsSpan(96, 4));

            var blueMask =
                BinaryPrimitives
                    .ReadUInt32LittleEndian(
                        bytes.AsSpan(100, 4));

            var alphaMask =
                BinaryPrimitives
                    .ReadUInt32LittleEndian(
                        bytes.AsSpan(104, 4));

            if (
                headerSize != DdsHeaderSize ||
                pixelFormatSize !=
                    PixelFormatSize)
            {
                return Error(
                    layerIndex,
                    fileName,
                    fileSize,
                    "invalidHeader");
            }

            if (
                width <= 0 ||
                height <= 0 ||
                width > MaximumDimension ||
                height > MaximumDimension)
            {
                return Error(
                    layerIndex,
                    fileName,
                    fileSize,
                    "invalidDimensions");
            }

            if (
                (pixelFormatFlags &
                    DdpfAlpha) == 0 ||
                fourCc != 0 ||
                bitCount != 8 ||
                redMask != 0 ||
                greenMask != 0 ||
                blueMask != 0 ||
                alphaMask != 0xFF)
            {
                return Error(
                    layerIndex,
                    fileName,
                    fileSize,
                    "unsupportedPixelFormat");
            }

            var pixelCount =
                checked(width * height);

            var requiredLength =
                checked(
                    DataOffset +
                    pixelCount);

            if (bytes.Length < requiredLength)
            {
                return Error(
                    layerIndex,
                    fileName,
                    fileSize,
                    "truncatedPixels");
            }

            byte minimum = byte.MaxValue;
            byte maximum = byte.MinValue;
            var nonZero = 0;

            var pixels =
                bytes.AsSpan(
                    DataOffset,
                    pixelCount);

            foreach (var alpha in pixels)
            {
                minimum =
                    Math.Min(
                        minimum,
                        alpha);

                maximum =
                    Math.Max(
                        maximum,
                        alpha);

                if (alpha != 0)
                {
                    nonZero++;
                }
            }

            return new OmsiTerrainTextureMask(
                LayerIndex: layerIndex,
                FileName: fileName,
                FileSize: fileSize,
                IsValid: true,
                Width: width,
                Height: height,
                Coverage:
                    nonZero /
                    (double)pixelCount,
                MinimumAlpha: minimum,
                MaximumAlpha: maximum,
                ErrorCode: null);
        }
        catch (OverflowException)
        {
            return Error(
                layerIndex,
                fileName,
                fileSize,
                "maskTooLarge");
        }
        catch (IOException)
        {
            return Error(
                layerIndex,
                fileName,
                fileSize,
                "ioError");
        }
    }

    private static OmsiTerrainTextureMask Error(
        int layerIndex,
        string fileName,
        long fileSize,
        string code) =>
        new(
            LayerIndex: layerIndex,
            FileName: fileName,
            FileSize: fileSize,
            IsValid: false,
            Width: 0,
            Height: 0,
            Coverage: 0,
            MinimumAlpha: 0,
            MaximumAlpha: 0,
            ErrorCode: code);
}
