#pragma warning disable CA1416 // Runtime guarded: System.Drawing path is Windows-only.
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Pfim;

namespace MapStudio.Core.ProtonBus;

public static class ProtonBusTextureTranscoder
{
    private static readonly HashSet<string>
        SupportedExtensions =
            new(
                [
                    ".png",
                    ".bmp",
                    ".jpg",
                    ".jpeg",
                    ".gif",
                    ".dds",
                    ".tga"
                ],
                StringComparer.OrdinalIgnoreCase);

    public static bool CanTranscode(
        string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            sourcePath);

        return SupportedExtensions.Contains(
            Path.GetExtension(
                sourcePath));
    }

    public static void WritePng(
        string sourcePath,
        string targetPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            sourcePath);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            targetPath);

        if (
            !OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Proton Bus texture transcoding currently requires Windows.");
        }

        if (
            !File.Exists(
                sourcePath))
        {
            throw new FileNotFoundException(
                "Texture source was not found.",
                sourcePath);
        }

        var targetDirectory =
            Path.GetDirectoryName(
                Path.GetFullPath(
                    targetPath));

        if (
            !string.IsNullOrWhiteSpace(
                targetDirectory))
        {
            Directory.CreateDirectory(
                targetDirectory);
        }

        var extension =
            Path.GetExtension(
                sourcePath);

        if (
            !SupportedExtensions.Contains(
                extension))
        {
            throw new NotSupportedException(
                $"Texture format '{extension}' cannot currently be transcoded to PNG.");
        }

        if (
            string.Equals(
                extension,
                ".dds",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                extension,
                ".tga",
                StringComparison.OrdinalIgnoreCase))
        {
            WritePfimImage(
                sourcePath,
                targetPath);

            return;
        }

        if (
            extension.Equals(
                ".png",
                StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(
                ".bmp",
                StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(
                ".jpg",
                StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(
                ".jpeg",
                StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(
                ".gif",
                StringComparison.OrdinalIgnoreCase))
        {
            using var image =
                Image.FromFile(
                    sourcePath);

            image.Save(
                targetPath,
                System.Drawing.Imaging
                    .ImageFormat.Png);

            return;
        }

        throw new NotSupportedException(
            $"Texture format '{extension}' cannot currently be transcoded to PNG.");
    }

    private static void WritePfimImage(
        string sourcePath,
        string targetPath)
    {
        using var image =
            Pfimage.FromFile(
                sourcePath);

        var pixelFormat =
            image.Format switch
            {
                Pfim.ImageFormat.Rgb24 =>
                    PixelFormat.Format24bppRgb,
                Pfim.ImageFormat.Rgba32 =>
                    PixelFormat.Format32bppArgb,
                Pfim.ImageFormat.R5g5b5 =>
                    PixelFormat.Format16bppRgb555,
                Pfim.ImageFormat.R5g6b5 =>
                    PixelFormat.Format16bppRgb565,
                Pfim.ImageFormat.R5g5b5a1 =>
                    PixelFormat.Format16bppArgb1555,
                Pfim.ImageFormat.Rgb8 =>
                    PixelFormat.Format8bppIndexed,
                _ =>
                    throw new NotSupportedException(
                        $"Pfim decoded '{sourcePath}' as unsupported pixel format '{image.Format}'.")
            };

        var handle =
            GCHandle.Alloc(
                image.Data,
                GCHandleType.Pinned);

        try
        {
            var pointer =
                Marshal
                    .UnsafeAddrOfPinnedArrayElement(
                        image.Data,
                        0);

            using var bitmap =
                new Bitmap(
                    image.Width,
                    image.Height,
                    image.Stride,
                    pixelFormat,
                    pointer);

            if (
                pixelFormat ==
                PixelFormat.Format8bppIndexed)
            {
                var palette =
                    bitmap.Palette;

                for (
                    var index = 0;
                    index <
                    palette.Entries.Length;
                    index++)
                {
                    palette.Entries[
                        index] =
                        Color.FromArgb(
                            index,
                            index,
                            index);
                }

                bitmap.Palette =
                    palette;
            }

            bitmap.Save(
                targetPath,
                System.Drawing.Imaging
                    .ImageFormat.Png);
        }
        finally
        {
            handle.Free();
        }
    }
}

#pragma warning restore CA1416
