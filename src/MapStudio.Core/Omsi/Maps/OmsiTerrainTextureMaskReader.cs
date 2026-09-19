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
        string path) =>
        ReadCore(
            layerIndex,
            path,
            includePixelStatistics: true);

    public OmsiTerrainTextureMask ReadHeader(
        int layerIndex,
        string path) =>
        ReadCore(
            layerIndex,
            path,
            includePixelStatistics: false);

    private static OmsiTerrainTextureMask
        ReadCore(
            int layerIndex,
            string path,
            bool includePixelStatistics)
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
            if (fileSize < DataOffset)
            {
                return Error(
                    layerIndex,
                    fileName,
                    fileSize,
                    "truncatedHeader");
            }

            using var stream =
                new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 4096,
                    options:
                        FileOptions
                            .SequentialScan);

            var header =
                new byte[DataOffset];

            stream.ReadExactly(header);

            if (
                header[0] != (byte)'D' ||
                header[1] != (byte)'D' ||
                header[2] != (byte)'S' ||
                header[3] != (byte)' ')
            {
                return Error(
                    layerIndex,
                    fileName,
                    fileSize,
                    "invalidSignature");
            }

            var span =
                header.AsSpan();

            var headerSize =
                BinaryPrimitives
                    .ReadUInt32LittleEndian(
                        span.Slice(4, 4));

            var height =
                BinaryPrimitives
                    .ReadInt32LittleEndian(
                        span.Slice(12, 4));

            var width =
                BinaryPrimitives
                    .ReadInt32LittleEndian(
                        span.Slice(16, 4));

            var pixelFormatSize =
                BinaryPrimitives
                    .ReadUInt32LittleEndian(
                        span.Slice(76, 4));

            var pixelFormatFlags =
                BinaryPrimitives
                    .ReadUInt32LittleEndian(
                        span.Slice(80, 4));

            var fourCc =
                BinaryPrimitives
                    .ReadUInt32LittleEndian(
                        span.Slice(84, 4));

            var bitCount =
                BinaryPrimitives
                    .ReadUInt32LittleEndian(
                        span.Slice(88, 4));

            var redMask =
                BinaryPrimitives
                    .ReadUInt32LittleEndian(
                        span.Slice(92, 4));

            var greenMask =
                BinaryPrimitives
                    .ReadUInt32LittleEndian(
                        span.Slice(96, 4));

            var blueMask =
                BinaryPrimitives
                    .ReadUInt32LittleEndian(
                        span.Slice(100, 4));

            var alphaMask =
                BinaryPrimitives
                    .ReadUInt32LittleEndian(
                        span.Slice(104, 4));

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
                    (long)DataOffset +
                    pixelCount);

            if (fileSize < requiredLength)
            {
                return Error(
                    layerIndex,
                    fileName,
                    fileSize,
                    "truncatedPixels");
            }

            if (!includePixelStatistics)
            {
                return new OmsiTerrainTextureMask(
                    LayerIndex: layerIndex,
                    FileName: fileName,
                    FileSize: fileSize,
                    IsValid: true,
                    Width: width,
                    Height: height,
                    HasPixelStatistics: false,
                    Coverage: 0,
                    MinimumAlpha: 0,
                    MaximumAlpha: 0,
                    ErrorCode: null);
            }

            var pixels =
                new byte[pixelCount];

            stream.ReadExactly(pixels);

            byte minimum = byte.MaxValue;
            byte maximum = byte.MinValue;
            var nonZero = 0;

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
                HasPixelStatistics: true,
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
        catch (EndOfStreamException)
        {
            return Error(
                layerIndex,
                fileName,
                fileSize,
                "truncatedPixels");
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
            HasPixelStatistics: false,
            Coverage: 0,
            MinimumAlpha: 0,
            MaximumAlpha: 0,
            ErrorCode: code);
}
