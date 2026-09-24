using System.Buffers.Binary;
using System.IO.Compression;

namespace MapStudio.Renderer.Graphics;

public sealed record NativeDecodedPng(
    int Width,
    int Height,
    byte[] Rgba);

public static class NativePngDecoder
{
    private static ReadOnlySpan<byte> Signature =>
    [
        137, 80, 78, 71, 13, 10, 26, 10
    ];

    public static NativeDecodedPng? TryDecodeFile(
        string path)
    {
        if (
            string.IsNullOrWhiteSpace(path) ||
            !File.Exists(path))
        {
            return null;
        }

        try
        {
            var bytes =
                File.ReadAllBytes(path);

            return TryDecode(bytes);
        }
        catch (
            Exception exception)
            when (
                exception is
                    IOException or
                    UnauthorizedAccessException or
                    InvalidDataException or
                    ArgumentException or
                    OverflowException)
        {
            return null;
        }
    }

    public static NativeDecodedPng? TryDecode(
        ReadOnlySpan<byte> bytes)
    {
        try
        {
            if (
                bytes.Length < 33 ||
                !bytes[..8]
                    .SequenceEqual(Signature))
            {
                return null;
            }

            var offset = 8;
            var width = 0;
            var height = 0;
            byte bitDepth = 0;
            byte colorType = 0;
            byte compressionMethod = 0;
            byte filterMethod = 0;
            byte interlaceMethod = 0;
            byte[]? palette = null;
            byte[]? transparency = null;

            using var idat =
                new MemoryStream();

            while (
                offset + 12 <=
                bytes.Length)
            {
                var length =
                    checked(
                        (int)BinaryPrimitives
                            .ReadUInt32BigEndian(
                                bytes.Slice(
                                    offset,
                                    4)));

                offset += 4;

                if (
                    length < 0 ||
                    offset + 8 + length >
                        bytes.Length)
                {
                    return null;
                }

                var type =
                    bytes.Slice(
                        offset,
                        4);

                offset += 4;

                var data =
                    bytes.Slice(
                        offset,
                        length);

                offset += length;

                // Skip CRC. The image bytes are already protected by
                // HTTPS/download validation and WIC remains a fallback.
                offset += 4;

                if (IsChunk(type, "IHDR"))
                {
                    if (
                        length != 13 ||
                        width != 0 ||
                        height != 0)
                    {
                        return null;
                    }

                    width =
                        checked(
                            (int)BinaryPrimitives
                                .ReadUInt32BigEndian(
                                    data[..4]));

                    height =
                        checked(
                            (int)BinaryPrimitives
                                .ReadUInt32BigEndian(
                                    data.Slice(
                                        4,
                                        4)));

                    bitDepth = data[8];
                    colorType = data[9];
                    compressionMethod = data[10];
                    filterMethod = data[11];
                    interlaceMethod = data[12];
                }
                else if (IsChunk(type, "PLTE"))
                {
                    palette =
                        data.ToArray();
                }
                else if (IsChunk(type, "tRNS"))
                {
                    transparency =
                        data.ToArray();
                }
                else if (IsChunk(type, "IDAT"))
                {
                    idat.Write(data);
                }
                else if (IsChunk(type, "IEND"))
                {
                    break;
                }
            }

            if (
                width <= 0 ||
                height <= 0 ||
                width > 16_384 ||
                height > 16_384 ||
                bitDepth != 8 ||
                compressionMethod != 0 ||
                filterMethod != 0 ||
                interlaceMethod != 0 ||
                idat.Length == 0)
            {
                return null;
            }

            var bytesPerPixel =
                colorType switch
                {
                    0 => 1,
                    2 => 3,
                    3 => 1,
                    4 => 2,
                    6 => 4,
                    _ => 0
                };

            if (bytesPerPixel == 0)
            {
                return null;
            }

            if (
                colorType == 3 &&
                (
                    palette is null ||
                    palette.Length == 0 ||
                    palette.Length % 3 != 0
                ))
            {
                return null;
            }

            var rowBytes =
                checked(
                    width *
                    bytesPerPixel);

            var expectedInflatedBytes =
                checked(
                    height *
                    (
                        rowBytes +
                        1
                    ));

            var filtered =
                new byte[
                    expectedInflatedBytes];

            idat.Position = 0;

            using (
                var zlib =
                    new ZLibStream(
                        idat,
                        CompressionMode
                            .Decompress,
                        leaveOpen: true))
            {
                var total = 0;

                while (
                    total <
                    filtered.Length)
                {
                    var read =
                        zlib.Read(
                            filtered,
                            total,
                            filtered.Length -
                                total);

                    if (read == 0)
                    {
                        break;
                    }

                    total += read;
                }

                if (total != filtered.Length)
                {
                    return null;
                }
            }

            var scanlines =
                new byte[
                    checked(
                        height *
                        rowBytes)];

            var previous =
                new byte[
                    rowBytes];

            var current =
                new byte[
                    rowBytes];

            var sourceOffset = 0;

            for (
                var row = 0;
                row < height;
                row++)
            {
                var filter =
                    filtered[
                        sourceOffset++];

                if (filter > 4)
                {
                    return null;
                }

                for (
                    var column = 0;
                    column < rowBytes;
                    column++)
                {
                    var raw =
                        filtered[
                            sourceOffset++];

                    var left =
                        column >=
                            bytesPerPixel
                            ? current[
                                column -
                                bytesPerPixel]
                            : (byte)0;

                    var up =
                        previous[
                            column];

                    var upperLeft =
                        column >=
                            bytesPerPixel
                            ? previous[
                                column -
                                bytesPerPixel]
                            : (byte)0;

                    current[column] =
                        filter switch
                        {
                            0 => raw,
                            1 => Add(
                                raw,
                                left),
                            2 => Add(
                                raw,
                                up),
                            3 => Add(
                                raw,
                                (byte)(
                                    (
                                        left +
                                        up
                                    ) /
                                    2)),
                            4 => Add(
                                raw,
                                Paeth(
                                    left,
                                    up,
                                    upperLeft)),
                            _ => raw
                        };
                }

                current.CopyTo(
                    scanlines,
                    row *
                        rowBytes);

                (
                    previous,
                    current
                ) =
                (
                    current,
                    previous
                );

                Array.Clear(
                    current);
            }

            var rgba =
                new byte[
                    checked(
                        width *
                        height *
                        4)];

            for (
                var pixel = 0;
                pixel <
                    width *
                    height;
                pixel++)
            {
                var source =
                    pixel *
                    bytesPerPixel;

                var target =
                    pixel *
                    4;

                switch (colorType)
                {
                    case 0:
                    {
                        var gray =
                            scanlines[source];

                        rgba[target] =
                            gray;
                        rgba[target + 1] =
                            gray;
                        rgba[target + 2] =
                            gray;
                        rgba[target + 3] =
                            255;
                        break;
                    }

                    case 2:
                        rgba[target] =
                            scanlines[source];
                        rgba[target + 1] =
                            scanlines[
                                source + 1];
                        rgba[target + 2] =
                            scanlines[
                                source + 2];
                        rgba[target + 3] =
                            255;
                        break;

                    case 3:
                    {
                        var index =
                            scanlines[source];

                        var paletteOffset =
                            index *
                            3;

                        if (
                            palette is null ||
                            paletteOffset + 2 >=
                                palette.Length)
                        {
                            return null;
                        }

                        rgba[target] =
                            palette[
                                paletteOffset];
                        rgba[target + 1] =
                            palette[
                                paletteOffset +
                                1];
                        rgba[target + 2] =
                            palette[
                                paletteOffset +
                                2];
                        rgba[target + 3] =
                            transparency is not
                                null &&
                            index <
                                transparency.Length
                                ? transparency[
                                    index]
                                : (byte)255;
                        break;
                    }

                    case 4:
                    {
                        var gray =
                            scanlines[source];

                        rgba[target] =
                            gray;
                        rgba[target + 1] =
                            gray;
                        rgba[target + 2] =
                            gray;
                        rgba[target + 3] =
                            scanlines[
                                source + 1];
                        break;
                    }

                    case 6:
                        rgba[target] =
                            scanlines[source];
                        rgba[target + 1] =
                            scanlines[
                                source + 1];
                        rgba[target + 2] =
                            scanlines[
                                source + 2];
                        rgba[target + 3] =
                            scanlines[
                                source + 3];
                        break;
                }
            }

            return new NativeDecodedPng(
                width,
                height,
                rgba);
        }
        catch (
            Exception exception)
            when (
                exception is
                    IOException or
                    InvalidDataException or
                    ArgumentException or
                    OverflowException)
        {
            return null;
        }
    }

    private static bool IsChunk(
        ReadOnlySpan<byte> type,
        string expected) =>
        type.Length == 4 &&
        type[0] ==
            (byte)expected[0] &&
        type[1] ==
            (byte)expected[1] &&
        type[2] ==
            (byte)expected[2] &&
        type[3] ==
            (byte)expected[3];

    private static byte Add(
        byte value,
        byte predictor) =>
        unchecked(
            (byte)(
                value +
                predictor));

    private static byte Paeth(
        byte left,
        byte up,
        byte upperLeft)
    {
        var prediction =
            left +
            up -
            upperLeft;

        var leftDistance =
            Math.Abs(
                prediction -
                left);

        var upDistance =
            Math.Abs(
                prediction -
                up);

        var upperLeftDistance =
            Math.Abs(
                prediction -
                upperLeft);

        if (
            leftDistance <=
                upDistance &&
            leftDistance <=
                upperLeftDistance)
        {
            return left;
        }

        return upDistance <=
            upperLeftDistance
                ? up
                : upperLeft;
    }
}
