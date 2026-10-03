using System.Buffers.Binary;
using System.Text;

namespace MapStudio.Core.Workspace;

public sealed class MapStudioStarterVegetationGenerator
{
    public const string RootFolderName =
        "MapStudio_Vegetation";

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

        var vegetationRoot =
            Path.Combine(
                root,
                "Sceneryobjects",
                RootFolderName);

        Directory.CreateDirectory(
            vegetationRoot);

        var created =
            false;

        created =
            await EnsureAssetAsync(
                vegetationRoot,
                folderName:
                    "Starter_Tree",
                fileStem:
                    "starter_tree",
                friendlyName:
                    "Map Studio Starter Tree",
                minimumHeight:
                    8.0,
                maximumHeight:
                    12.0,
                minimumAspect:
                    0.45,
                maximumAspect:
                    0.60,
                shrub:
                    false,
                cancellationToken) ||
            created;

        created =
            await EnsureAssetAsync(
                vegetationRoot,
                folderName:
                    "Starter_Shrub",
                fileStem:
                    "starter_shrub",
                friendlyName:
                    "Map Studio Starter Shrub",
                minimumHeight:
                    1.0,
                maximumHeight:
                    2.2,
                minimumAspect:
                    0.85,
                maximumAspect:
                    1.35,
                shrub:
                    true,
                cancellationToken) ||
            created;

        return created;
    }

    private static async Task<bool>
        EnsureAssetAsync(
            string vegetationRoot,
            string folderName,
            string fileStem,
            string friendlyName,
            double minimumHeight,
            double maximumHeight,
            double minimumAspect,
            double maximumAspect,
            bool shrub,
            CancellationToken cancellationToken)
    {
        var directory =
            Path.Combine(
                vegetationRoot,
                folderName);

        var textureDirectory =
            Path.Combine(
                directory,
                "Texture");

        Directory.CreateDirectory(
            textureDirectory);

        var textureFileName =
            fileStem +
            ".tga";

        var texturePath =
            Path.Combine(
                textureDirectory,
                textureFileName);

        var scoPath =
            Path.Combine(
                directory,
                fileStem +
                ".sco");

        var created =
            false;

        if (!File.Exists(
                texturePath))
        {
            var texture =
                CreateVegetationTexture(
                    shrub
                        ? 96
                        : 128,
                    shrub
                        ? 72
                        : 160,
                    shrub);

            await File.WriteAllBytesAsync(
                    texturePath,
                    texture,
                    cancellationToken)
                .ConfigureAwait(false);

            created =
                true;
        }

        if (!File.Exists(
                scoPath))
        {
            var source =
                BuildSco(
                    friendlyName,
                    textureFileName,
                    minimumHeight,
                    maximumHeight,
                    minimumAspect,
                    maximumAspect);

            await File.WriteAllTextAsync(
                    scoPath,
                    source,
                    Encoding.ASCII,
                    cancellationToken)
                .ConfigureAwait(false);

            created =
                true;
        }

        return created;
    }

    private static string BuildSco(
        string friendlyName,
        string textureFileName,
        double minimumHeight,
        double maximumHeight,
        double minimumAspect,
        double maximumAspect)
    {
        static string Format(
            double value) =>
            value.ToString(
                "0.###",
                System.Globalization
                    .CultureInfo
                    .InvariantCulture);

        return
            "[friendlyname]\r\n" +
            friendlyName +
            "\r\n\r\n" +
            "[groups]\r\n" +
            "2\r\n" +
            "Map Studio\r\n" +
            "Vegetation\r\n\r\n" +
            "[tree]\r\n" +
            @"Texture\" +
            textureFileName +
            "\r\n" +
            Format(
                minimumHeight) +
            "\r\n" +
            Format(
                maximumHeight) +
            "\r\n" +
            Format(
                minimumAspect) +
            "\r\n" +
            Format(
                maximumAspect) +
            "\r\n";
    }

    private static byte[]
        CreateVegetationTexture(
            int width,
            int height,
            bool shrub)
    {
        var pixels =
            new byte[
                checked(
                    width *
                    height *
                    4)];

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
                var r =
                    (byte)0;

                var g =
                    (byte)0;

                var b =
                    (byte)0;

                var a =
                    (byte)0;

                if (shrub)
                {
                    if (
                        InsideEllipse(
                            x,
                            y,
                            width * 0.50,
                            height * 0.58,
                            width * 0.44,
                            height * 0.38) ||
                        InsideEllipse(
                            x,
                            y,
                            width * 0.30,
                            height * 0.62,
                            width * 0.26,
                            height * 0.28) ||
                        InsideEllipse(
                            x,
                            y,
                            width * 0.72,
                            height * 0.62,
                            width * 0.25,
                            height * 0.28))
                    {
                        var variation =
                            (
                                x * 13 +
                                y * 7
                            ) %
                            24;

                        r =
                            (byte)(
                                48 +
                                variation /
                                3);

                        g =
                            (byte)(
                                112 +
                                variation);

                        b =
                            (byte)(
                                52 +
                                variation /
                                2);

                        a =
                            255;
                    }
                }
                else
                {
                    var trunk =
                        x >=
                            width * 0.46 &&
                        x <=
                            width * 0.54 &&
                        y >=
                            height * 0.56 &&
                        y <=
                            height * 0.96;

                    var canopy =
                        InsideEllipse(
                            x,
                            y,
                            width * 0.50,
                            height * 0.34,
                            width * 0.34,
                            height * 0.27) ||
                        InsideEllipse(
                            x,
                            y,
                            width * 0.34,
                            height * 0.43,
                            width * 0.23,
                            height * 0.25) ||
                        InsideEllipse(
                            x,
                            y,
                            width * 0.68,
                            height * 0.43,
                            width * 0.23,
                            height * 0.25);

                    if (trunk)
                    {
                        r =
                            94;
                        g =
                            69;
                        b =
                            43;
                        a =
                            255;
                    }

                    if (canopy)
                    {
                        var variation =
                            (
                                x * 11 +
                                y * 17
                            ) %
                            30;

                        r =
                            (byte)(
                                42 +
                                variation /
                                4);

                        g =
                            (byte)(
                                104 +
                                variation);

                        b =
                            (byte)(
                                45 +
                                variation /
                                3);

                        a =
                            255;
                    }
                }

                var offset =
                    (
                        y *
                        width +
                        x
                    ) *
                    4;

                // Uncompressed 32-bit TGA stores BGRA.
                pixels[offset] =
                    b;
                pixels[
                    offset +
                    1] =
                    g;
                pixels[
                    offset +
                    2] =
                    r;
                pixels[
                    offset +
                    3] =
                    a;
            }
        }

        var output =
            new byte[
                checked(
                    18 +
                    pixels.Length)];

        output[2] =
            2;

        BinaryPrimitives
            .WriteUInt16LittleEndian(
                output.AsSpan(
                    12,
                    2),
                checked(
                    (ushort)width));

        BinaryPrimitives
            .WriteUInt16LittleEndian(
                output.AsSpan(
                    14,
                    2),
                checked(
                    (ushort)height));

        output[16] =
            32;

        // Top-left origin + 8 alpha bits.
        output[17] =
            0x28;

        pixels.CopyTo(
            output,
            18);

        return output;
    }

    private static bool InsideEllipse(
        int x,
        int y,
        double centerX,
        double centerY,
        double radiusX,
        double radiusY)
    {
        var dx =
            (
                x -
                centerX
            ) /
            radiusX;

        var dy =
            (
                y -
                centerY
            ) /
            radiusY;

        return
            dx * dx +
            dy * dy <=
            1.0;
    }
}
