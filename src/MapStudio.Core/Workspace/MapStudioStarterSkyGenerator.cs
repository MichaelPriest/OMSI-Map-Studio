using System.Buffers.Binary;

namespace MapStudio.Core.Workspace;

public sealed class MapStudioStarterSkyGenerator
{
    public async Task<bool> EnsureAsync(
        string contentRoot,
        CancellationToken cancellationToken =
            default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            contentRoot);

        var root =
            Path.GetFullPath(
                contentRoot);

        var textureDirectory =
            Path.Combine(
                root,
                "Texture");

        var skyboxDirectory =
            Path.Combine(
                textureDirectory,
                "skybox");

        Directory.CreateDirectory(
            textureDirectory);

        Directory.CreateDirectory(
            skyboxDirectory);

        var day =
            CreateSkyBmp(
                512,
                256,
                night:
                    false);

        var night =
            CreateSkyBmp(
                512,
                256,
                night:
                    true);

        var created =
            false;

        created =
            await WriteIfMissingAsync(
                    Path.Combine(
                        textureDirectory,
                        "himmel01.bmp"),
                    day,
                    cancellationToken)
                .ConfigureAwait(false) ||
            created;

        created =
            await WriteIfMissingAsync(
                    Path.Combine(
                        textureDirectory,
                        "himmel05.bmp"),
                    night,
                    cancellationToken)
                .ConfigureAwait(false) ||
            created;

        created =
            await WriteIfMissingAsync(
                    Path.Combine(
                        skyboxDirectory,
                        "day01.bmp"),
                    day,
                    cancellationToken)
                .ConfigureAwait(false) ||
            created;

        created =
            await WriteIfMissingAsync(
                    Path.Combine(
                        skyboxDirectory,
                        "night01.bmp"),
                    night,
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

    internal static byte[] CreateSkyBmp(
        int width,
        int height,
        bool night)
    {
        width =
            Math.Clamp(
                width,
                64,
                2048);

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
            var v =
                y /
                (double)Math.Max(
                    1,
                    height -
                    1);

            var horizonDistance =
                Math.Abs(
                    v -
                    0.5) *
                2.0;

            var topHalf =
                v <=
                0.5;

            var color =
                night
                    ? Blend(
                        topHalf
                            ? new Rgb(
                                7,
                                14,
                                35)
                            : new Rgb(
                                7,
                                9,
                                18),
                        new Rgb(
                            30,
                            42,
                            70),
                        1.0 -
                            horizonDistance)
                    : Blend(
                        topHalf
                            ? new Rgb(
                                55,
                                121,
                                191)
                            : new Rgb(
                                86,
                                104,
                                124),
                        new Rgb(
                            188,
                            215,
                            237),
                        1.0 -
                            horizonDistance);

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
                var pixel =
                    color;

                if (
                    night &&
                    v <
                        0.52 &&
                    IsStar(
                        x,
                        y))
                {
                    var brightness =
                        (byte)(
                            190 +
                            (
                                (
                                    x *
                                    17 +
                                    y *
                                    31
                                ) %
                                66
                            ));

                    pixel =
                        new Rgb(
                            brightness,
                            brightness,
                            (byte)Math.Min(
                                255,
                                brightness +
                                8));
                }

                var offset =
                    row +
                    x *
                    3;

                output[offset] =
                    pixel.B;

                output[
                    offset +
                    1] =
                    pixel.G;

                output[
                    offset +
                    2] =
                    pixel.R;
            }
        }

        return output;
    }

    private static bool IsStar(
        int x,
        int y)
    {
        var hash =
            unchecked(
                x *
                    73856093 ^
                y *
                    19349663 ^
                0x5A17);

        return
            (
                hash &
                0x7FF
            ) ==
            0;
    }

    private static Rgb Blend(
        Rgb from,
        Rgb to,
        double amount)
    {
        amount =
            Math.Clamp(
                amount,
                0,
                1);

        static byte Mix(
            byte a,
            byte b,
            double t) =>
            (byte)Math.Clamp(
                (int)Math.Round(
                    a +
                    (
                        b -
                        a
                    ) *
                    t),
                0,
                255);

        return new Rgb(
            Mix(
                from.R,
                to.R,
                amount),
            Mix(
                from.G,
                to.G,
                amount),
            Mix(
                from.B,
                to.B,
                amount));
    }

    private readonly record struct Rgb(
        byte R,
        byte G,
        byte B);
}
