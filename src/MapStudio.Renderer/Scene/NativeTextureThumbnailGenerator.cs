using System.Buffers.Binary;
using Vortice.WIC;
using WICPixelFormat =
    Vortice.WIC.PixelFormat;

namespace MapStudio.Renderer.Scene;

public sealed class NativeTextureThumbnailGenerator
{
    private sealed record PixelData(
        int Width,
        int Height,
        byte[] Rgba);

    public byte[] RenderBmp(
        string path,
        int width = 180,
        int height = 120)
    {
        if (
            string.IsNullOrWhiteSpace(
                path) ||
            !File.Exists(path) ||
            width is < 32 or > 1024 ||
            height is < 32 or > 1024)
        {
            return [];
        }

        var pixels =
            TryDecode(
                path);

        if (pixels is null)
        {
            return [];
        }

        return RenderBmp(
            pixels,
            width,
            height);
    }

    private static PixelData? TryDecode(
        string path)
    {
        if (
            string.Equals(
                Path.GetExtension(path),
                ".tga",
                StringComparison.OrdinalIgnoreCase))
        {
            var tga =
                TryDecodeTga(
                    path);

            if (tga is not null)
            {
                return tga;
            }
        }

        if (
            string.Equals(
                Path.GetExtension(path),
                ".dds",
                StringComparison.OrdinalIgnoreCase))
        {
            var alpha =
                TryDecodeAlphaDds(
                    path);

            if (alpha is not null)
            {
                return alpha;
            }
        }

        return TryDecodeWic(
            path);
    }

    private static PixelData? TryDecodeWic(
        string path)
    {
        try
        {
            using var factory =
                new IWICImagingFactory2();

            using var decoder =
                factory
                    .CreateDecoderFromFileName(
                        path);

            using var frame =
                decoder.GetFrame(0);

            using var converter =
                factory.CreateFormatConverter();

            converter.Initialize(
                frame,
                WICPixelFormat
                    .Format32bppRGBA);

            var size =
                converter.Size;

            if (
                size.Width <= 0 ||
                size.Height <= 0 ||
                size.Width > 16_384 ||
                size.Height > 16_384)
            {
                return null;
            }

            var stride =
                checked(
                    (uint)size.Width *
                    4u);

            var rgba =
                new byte[
                    checked(
                        size.Width *
                        size.Height *
                        4)];

            converter.CopyPixels(
                stride,
                rgba);

            return new PixelData(
                size.Width,
                size.Height,
                rgba);
        }
        catch (
            Exception exception)
            when (
                exception is
                    IOException or
                    UnauthorizedAccessException or
                    ArgumentException or
                    NotSupportedException or
                    OverflowException ||
                exception.GetType()
                    .Namespace?
                    .StartsWith(
                        "SharpGen",
                        StringComparison.Ordinal) ==
                    true)
        {
            return null;
        }
    }

    private static PixelData? TryDecodeAlphaDds(
        string path)
    {
        try
        {
            using var stream =
                new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read);

            if (stream.Length < 128)
            {
                return null;
            }

            var header =
                new byte[128];

            stream.ReadExactly(
                header);

            if (
                header[0] != (byte)'D' ||
                header[1] != (byte)'D' ||
                header[2] != (byte)'S' ||
                header[3] != (byte)' ')
            {
                return null;
            }

            var span =
                header.AsSpan();

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

            var pixelFlags =
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

            const uint ddpfAlpha =
                0x00000002;

            if (
                width <= 0 ||
                height <= 0 ||
                width > 16_384 ||
                height > 16_384 ||
                (pixelFlags & ddpfAlpha) ==
                    0 ||
                fourCc != 0 ||
                bitCount != 8 ||
                alphaMask != 0xFF)
            {
                return null;
            }

            var count =
                checked(
                    width *
                    height);

            if (
                stream.Length <
                128L +
                count)
            {
                return null;
            }

            var alpha =
                new byte[count];

            stream.ReadExactly(
                alpha);

            var rgba =
                new byte[
                    checked(
                        count *
                        4)];

            for (
                var index = 0;
                index < count;
                index++)
            {
                var value =
                    alpha[index];

                var offset =
                    index *
                    4;

                rgba[offset] =
                    value;

                rgba[offset + 1] =
                    value;

                rgba[offset + 2] =
                    value;

                rgba[offset + 3] =
                    255;
            }

            return new PixelData(
                width,
                height,
                rgba);
        }
        catch (
            Exception exception)
            when (
                exception is
                    IOException or
                    UnauthorizedAccessException or
                    ArgumentException or
                    NotSupportedException or
                    OverflowException)
        {
            return null;
        }
    }

    private static PixelData? TryDecodeTga(
        string path)
    {
        try
        {
            using var stream =
                new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read);

            Span<byte> header =
                stackalloc byte[18];

            stream.ReadExactly(
                header);

            var idLength =
                header[0];

            var colorMapType =
                header[1];

            var imageType =
                header[2];

            var width =
                BinaryPrimitives
                    .ReadUInt16LittleEndian(
                        header.Slice(
                            12,
                            2));

            var height =
                BinaryPrimitives
                    .ReadUInt16LittleEndian(
                        header.Slice(
                            14,
                            2));

            var pixelDepth =
                header[16];

            var descriptor =
                header[17];

            if (
                colorMapType != 0 ||
                imageType is not
                    (2 or 10) ||
                width == 0 ||
                height == 0 ||
                pixelDepth is not
                    (24 or 32))
            {
                return null;
            }

            if (idLength > 0)
            {
                stream.Seek(
                    idLength,
                    SeekOrigin.Current);
            }

            var bytesPerPixel =
                pixelDepth /
                8;

            var pixelCount =
                checked(
                    (int)width *
                    (int)height);

            var source =
                new byte[
                    checked(
                        pixelCount *
                        bytesPerPixel)];

            if (imageType == 2)
            {
                stream.ReadExactly(
                    source);
            }
            else if (
                !TryDecodeTgaRle(
                    stream,
                    source,
                    bytesPerPixel,
                    pixelCount))
            {
                return null;
            }

            var rgba =
                new byte[
                    checked(
                        pixelCount *
                        4)];

            var topOrigin =
                (descriptor &
                    0x20) !=
                0;

            var rightOrigin =
                (descriptor &
                    0x10) !=
                0;

            for (
                var sourceIndex = 0;
                sourceIndex <
                    pixelCount;
                sourceIndex++)
            {
                var sourceX =
                    sourceIndex %
                    width;

                var sourceY =
                    sourceIndex /
                    width;

                var targetX =
                    rightOrigin
                        ? width -
                            1 -
                            sourceX
                        : sourceX;

                var targetY =
                    topOrigin
                        ? sourceY
                        : height -
                            1 -
                            sourceY;

                var inputOffset =
                    sourceIndex *
                    bytesPerPixel;

                var outputOffset =
                    (
                        targetY *
                        width +
                        targetX
                    ) *
                    4;

                rgba[outputOffset] =
                    source[
                        inputOffset +
                        2];

                rgba[
                    outputOffset +
                    1] =
                    source[
                        inputOffset +
                        1];

                rgba[
                    outputOffset +
                    2] =
                    source[
                        inputOffset];

                rgba[
                    outputOffset +
                    3] =
                    bytesPerPixel ==
                    4
                        ? source[
                            inputOffset +
                            3]
                        : (byte)255;
            }

            return new PixelData(
                width,
                height,
                rgba);
        }
        catch (
            Exception exception)
            when (
                exception is
                    IOException or
                    UnauthorizedAccessException or
                    ArgumentException or
                    NotSupportedException or
                    OverflowException)
        {
            return null;
        }
    }

    private static bool TryDecodeTgaRle(
        Stream stream,
        byte[] destination,
        int bytesPerPixel,
        int pixelCount)
    {
        var pixelIndex =
            0;

        Span<byte> pixel =
            stackalloc byte[4];

        while (
            pixelIndex <
            pixelCount)
        {
            var packetHeader =
                stream.ReadByte();

            if (packetHeader < 0)
            {
                return false;
            }

            var count =
                (packetHeader &
                    0x7F) +
                1;

            if (
                pixelIndex +
                    count >
                pixelCount)
            {
                return false;
            }

            if (
                (packetHeader &
                    0x80) !=
                0)
            {
                stream.ReadExactly(
                    pixel[
                        ..bytesPerPixel]);

                for (
                    var repeat = 0;
                    repeat < count;
                    repeat++)
                {
                    pixel[
                        ..bytesPerPixel]
                        .CopyTo(
                            destination
                                .AsSpan(
                                    pixelIndex *
                                        bytesPerPixel,
                                    bytesPerPixel));

                    pixelIndex++;
                }

                continue;
            }

            var byteCount =
                checked(
                    count *
                    bytesPerPixel);

            stream.ReadExactly(
                destination.AsSpan(
                    pixelIndex *
                        bytesPerPixel,
                    byteCount));

            pixelIndex +=
                count;
        }

        return true;
    }

    private static byte[] RenderBmp(
        PixelData source,
        int width,
        int height)
    {
        var canvas =
            new uint[
                width *
                height];

        for (
            var y = 0;
            y < height;
            y++)
        {
            for (
                var x = 0;
                x < width;
                x++)
            {
                var checker =
                    (
                        x /
                            10 +
                        y /
                            10
                    ) %
                    2 ==
                    0
                        ? (byte)30
                        : (byte)48;

                canvas[
                    y *
                    width +
                    x] =
                    PackColor(
                        checker,
                        checker,
                        checker);
            }
        }

        var scale =
            Math.Min(
                (double)width /
                    source.Width,
                (double)height /
                    source.Height);

        if (
            !double.IsFinite(
                scale) ||
            scale <= 0)
        {
            return [];
        }

        var drawWidth =
            Math.Max(
                1,
                (int)Math.Round(
                    source.Width *
                    scale));

        var drawHeight =
            Math.Max(
                1,
                (int)Math.Round(
                    source.Height *
                    scale));

        var offsetX =
            (
                width -
                drawWidth
            ) /
            2;

        var offsetY =
            (
                height -
                drawHeight
            ) /
            2;

        for (
            var y = 0;
            y < drawHeight;
            y++)
        {
            var sourceY =
                Math.Clamp(
                    (int)(
                        (long)y *
                        source.Height /
                        drawHeight),
                    0,
                    source.Height -
                        1);

            for (
                var x = 0;
                x < drawWidth;
                x++)
            {
                var sourceX =
                    Math.Clamp(
                        (int)(
                            (long)x *
                            source.Width /
                            drawWidth),
                        0,
                        source.Width -
                            1);

                var input =
                    (
                        sourceY *
                        source.Width +
                        sourceX
                    ) *
                    4;

                var target =
                    (
                        offsetY +
                        y
                    ) *
                    width +
                    offsetX +
                    x;

                var background =
                    canvas[target];

                var alpha =
                    source.Rgba[
                        input +
                        3];

                canvas[target] =
                    Composite(
                        source.Rgba[
                            input],
                        source.Rgba[
                            input +
                            1],
                        source.Rgba[
                            input +
                            2],
                        alpha,
                        background);
            }
        }

        return EncodeBmp(
            width,
            height,
            canvas);
    }

    private static uint Composite(
        byte r,
        byte g,
        byte b,
        byte alpha,
        uint background)
    {
        if (alpha == 255)
        {
            return PackColor(
                r,
                g,
                b);
        }

        var inverse =
            255 -
            alpha;

        var bgR =
            (byte)(
                (
                    background >>
                    16
                ) &
                0xFF);

        var bgG =
            (byte)(
                (
                    background >>
                    8
                ) &
                0xFF);

        var bgB =
            (byte)(
                background &
                0xFF);

        return PackColor(
            Blend(
                r,
                bgR,
                alpha,
                inverse),
            Blend(
                g,
                bgG,
                alpha,
                inverse),
            Blend(
                b,
                bgB,
                alpha,
                inverse));
    }

    private static byte Blend(
        byte foreground,
        byte background,
        int alpha,
        int inverse) =>
        (byte)(
            (
                foreground *
                    alpha +
                background *
                    inverse +
                127
            ) /
            255);

    private static uint PackColor(
        byte r,
        byte g,
        byte b) =>
        (uint)(
            b |
            g << 8 |
            r << 16 |
            0xFF << 24);

    private static byte[] EncodeBmp(
        int width,
        int height,
        uint[] pixels)
    {
        var stride =
            checked(
                width *
                4);

        var pixelBytes =
            checked(
                stride *
                height);

        var output =
            new byte[
                checked(
                    54 +
                    pixelBytes)];

        output[0] =
            (byte)'B';

        output[1] =
            (byte)'M';

        BinaryPrimitives
            .WriteInt32LittleEndian(
                output.AsSpan(
                    2,
                    4),
                output.Length);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                output.AsSpan(
                    10,
                    4),
                54);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                output.AsSpan(
                    14,
                    4),
                40);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                output.AsSpan(
                    18,
                    4),
                width);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                output.AsSpan(
                    22,
                    4),
                height);

        BinaryPrimitives
            .WriteInt16LittleEndian(
                output.AsSpan(
                    26,
                    2),
                1);

        BinaryPrimitives
            .WriteInt16LittleEndian(
                output.AsSpan(
                    28,
                    2),
                32);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                output.AsSpan(
                    34,
                    4),
                pixelBytes);

        for (
            var y = 0;
            y < height;
            y++)
        {
            var sourceY =
                height -
                1 -
                y;

            var destination =
                54 +
                y *
                stride;

            for (
                var x = 0;
                x < width;
                x++)
            {
                var color =
                    pixels[
                        sourceY *
                        width +
                        x];

                var offset =
                    destination +
                    x *
                    4;

                output[offset] =
                    (byte)(
                        color &
                        0xFF);

                output[
                    offset +
                    1] =
                    (byte)(
                        (
                            color >>
                            8
                        ) &
                        0xFF);

                output[
                    offset +
                    2] =
                    (byte)(
                        (
                            color >>
                            16
                        ) &
                        0xFF);

                output[
                    offset +
                    3] =
                    0xFF;
            }
        }

        return output;
    }
}
