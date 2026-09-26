using System.Buffers.Binary;

namespace MapStudio.Core.Workspace;

public sealed class MapStudioStarterTerrainTextureGenerator
{
    public async Task<bool> EnsureAsync(
        string contentRoot,
        CancellationToken cancellationToken =
            default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            contentRoot);

        var textureDirectory =
            Path.Combine(
                Path.GetFullPath(
                    contentRoot),
                "Texture");

        Directory.CreateDirectory(
            textureDirectory);

        var created =
            false;

        created =
            await WriteIfMissingAsync(
                    Path.Combine(
                        textureDirectory,
                        "mapstudio_grass.bmp"),
                    CreateTexture(
                        256,
                        256,
                        detail:
                            false),
                    cancellationToken)
                .ConfigureAwait(false) ||
            created;

        created =
            await WriteIfMissingAsync(
                    Path.Combine(
                        textureDirectory,
                        "mapstudio_grass_detail.bmp"),
                    CreateTexture(
                        128,
                        128,
                        detail:
                            true),
                    cancellationToken)
                .ConfigureAwait(false) ||
            created;

        return created;
    }

    private static async Task<bool>
        WriteIfMissingAsync(
            string path,
            byte[] bytes,
            CancellationToken cancellationToken)
    {
        if (File.Exists(
                path))
        {
            return false;
        }

        await File.WriteAllBytesAsync(
                path,
                bytes,
                cancellationToken)
            .ConfigureAwait(false);

        return true;
    }

    internal static byte[] CreateTexture(
        int width,
        int height,
        bool detail)
    {
        width =
            Math.Clamp(
                width,
                32,
                1024);

        height =
            Math.Clamp(
                height,
                32,
                1024);

        var rowStride =
            (
                width *
                3 +
                3
            ) &
            ~3;

        var pixelBytes =
            checked(
                rowStride *
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
                24);

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
            var targetY =
                height -
                1 -
                y;

            var row =
                54 +
                targetY *
                    rowStride;

            for (
                var x = 0;
                x < width;
                x++)
            {
                var hash =
                    unchecked(
                        x *
                            73856093 ^
                        y *
                            19349663 ^
                        (
                            detail
                                ? 0x3461
                                : 0x7193
                        ));

                var noise =
                    Math.Abs(
                        hash %
                        (
                            detail
                                ? 42
                                : 28
                        ));

                byte r;
                byte g;
                byte b;

                if (detail)
                {
                    var value =
                        (byte)(
                            92 +
                            noise);

                    r =
                        value;
                    g =
                        value;
                    b =
                        value;
                }
                else
                {
                    r =
                        (byte)(
                            62 +
                            noise /
                                3);

                    g =
                        (byte)(
                            105 +
                            noise);

                    b =
                        (byte)(
                            48 +
                            noise /
                                4);
                }

                var offset =
                    row +
                    x *
                        3;

                output[offset] =
                    b;
                output[
                    offset +
                    1] =
                    g;
                output[
                    offset +
                    2] =
                    r;
            }
        }

        return output;
    }
}
