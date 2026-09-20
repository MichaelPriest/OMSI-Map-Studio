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

            var texture =
                _deviceHost.Device
                    .CreateTexture2D(
                        pixels,
                        Format
                            .R8G8B8A8_UNorm,
                        (uint)size.Width,
                        (uint)size.Height,
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
}
