using System.Buffers.Binary;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.WIC;
using WICPixelFormat =
    Vortice.WIC.PixelFormat;

namespace MapStudio.Renderer.Graphics;

internal sealed class NativeGpuTexture :
    IDisposable
{
    public NativeGpuTexture(
        ID3D11Texture2D texture,
        ID3D11ShaderResourceView view)
    {
        Texture = texture;
        View = view;
    }

    public ID3D11Texture2D Texture
    {
        get;
    }

    public ID3D11ShaderResourceView View
    {
        get;
    }

    public void Dispose()
    {
        View.Dispose();
        Texture.Dispose();
    }
}

internal sealed class NativeGpuTextureLoader
{
    private readonly D3D11DeviceHost
        _deviceHost;

    public NativeGpuTextureLoader(
        D3D11DeviceHost deviceHost)
    {
        _deviceHost =
            deviceHost ??
            throw new ArgumentNullException(
                nameof(deviceHost));
    }

    public NativeGpuTexture? TryLoadAlphaMask(
        string path)
    {
        if (
            string.IsNullOrWhiteSpace(
                path) ||
            !File.Exists(path))
        {
            return null;
        }

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

            var pixelFormatFlags =
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
                width > 4096 ||
                height > 4096 ||
                (pixelFormatFlags &
                    ddpfAlpha) == 0 ||
                fourCc != 0 ||
                bitCount != 8 ||
                alphaMask != 0xFF)
            {
                return null;
            }

            var pixelCount =
                checked(
                    width *
                    height);

            if (
                stream.Length <
                128L +
                pixelCount)
            {
                return null;
            }

            var alpha =
                new byte[pixelCount];

            stream.ReadExactly(
                alpha);

            var rgba =
                new byte[
                    checked(
                        pixelCount *
                        4)];

            for (
                var index = 0;
                index < pixelCount;
                index++)
            {
                var target =
                    index *
                    4;

                rgba[target] =
                    255;

                rgba[target + 1] =
                    255;

                rgba[target + 2] =
                    255;

                rgba[target + 3] =
                    alpha[index];
            }

            return CreateRgbaTexture(
                rgba,
                width,
                height);
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

    public NativeGpuTexture? TryLoad(
        string path)
    {
        if (
            string.IsNullOrWhiteSpace(
                path) ||
            !File.Exists(path))
        {
            return null;
        }

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

            converter
                .Initialize(
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

            var pixels =
                new byte[
                    checked(
                        size.Width *
                        size.Height *
                        4)];

            converter.CopyPixels(
                stride,
                pixels);

            return CreateRgbaTexture(
                pixels,
                size.Width,
                size.Height);
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
    }    private NativeGpuTexture CreateRgbaTexture(
        byte[] pixels,
        int width,
        int height)
    {
        var texture =
            _deviceHost.Device
                .CreateTexture2D(
                    pixels,
                    Format
                        .R8G8B8A8_UNorm,
                    (uint)width,
                    (uint)height,
                    mipLevels: 1,
                    bindFlags:
                        BindFlags
                            .ShaderResource);

        var view =
            _deviceHost.Device
                .CreateShaderResourceView(
                    texture);

        return new NativeGpuTexture(
            texture,
            view);
    }


}
