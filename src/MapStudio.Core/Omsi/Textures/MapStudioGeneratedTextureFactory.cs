using System.Buffers.Binary;

namespace MapStudio.Core.Omsi.Textures;

public readonly record struct MapStudioGeneratedRgb(
    byte R,
    byte G,
    byte B);

public static class MapStudioGeneratedTextureFactory
{
    public static async Task<bool> EnsureBmpAsync(
        string directory,
        string fileName,
        int width,
        int height,
        Func<int, int, MapStudioGeneratedRgb> pixel,
        CancellationToken cancellationToken =
            default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            directory);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            fileName);

        ArgumentNullException.ThrowIfNull(
            pixel);

        if (
            width <= 0 ||
            height <= 0 ||
            width > 4096 ||
            height > 4096)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width));
        }

        Directory.CreateDirectory(
            directory);

        var path =
            Path.Combine(
                directory,
                fileName);

        if (File.Exists(path))
        {
            return false;
        }

        await File.WriteAllBytesAsync(
                path,
                CreateBmp24(
                    width,
                    height,
                    pixel),
                cancellationToken)
            .ConfigureAwait(false);

        return true;
    }

    public static byte[] CreateBmp24(
        int width,
        int height,
        Func<int, int, MapStudioGeneratedRgb> pixel)
    {
        ArgumentNullException.ThrowIfNull(
            pixel);

        if (
            width <= 0 ||
            height <= 0 ||
            width > 4096 ||
            height > 4096)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width));
        }

        var rowStride =
            checked(
                (
                    width *
                    3 +
                    3
                ) &
                ~3);

        var pixelBytes =
            checked(
                rowStride *
                height);

        var data =
            new byte[
                checked(
                    54 +
                    pixelBytes)];

        data[0] =
            (byte)'B';

        data[1] =
            (byte)'M';

        BinaryPrimitives
            .WriteInt32LittleEndian(
                data.AsSpan(
                    2,
                    4),
                data.Length);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                data.AsSpan(
                    10,
                    4),
                54);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                data.AsSpan(
                    14,
                    4),
                40);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                data.AsSpan(
                    18,
                    4),
                width);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                data.AsSpan(
                    22,
                    4),
                height);

        BinaryPrimitives
            .WriteInt16LittleEndian(
                data.AsSpan(
                    26,
                    2),
                1);

        BinaryPrimitives
            .WriteInt16LittleEndian(
                data.AsSpan(
                    28,
                    2),
                24);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                data.AsSpan(
                    34,
                    4),
                pixelBytes);

        for (
            var y = 0;
            y < height;
            y++)
        {
            var targetY =
                height -
                1 -
                y;

            var rowOffset =
                54 +
                targetY *
                    rowStride;

            for (
                var x = 0;
                x < width;
                x++)
            {
                var color =
                    pixel(
                        x,
                        y);

                var offset =
                    rowOffset +
                    x *
                        3;

                data[offset] =
                    color.B;

                data[offset + 1] =
                    color.G;

                data[offset + 2] =
                    color.R;
            }
        }

        return data;
    }
}
