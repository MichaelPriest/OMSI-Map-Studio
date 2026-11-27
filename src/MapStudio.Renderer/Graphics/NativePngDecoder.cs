using MapStudio.Core.IO;
namespace MapStudio.Renderer.Graphics;
public sealed record NativeDecodedPng(int Width, int Height, byte[] Rgba);
public static class NativePngDecoder
{
    public static NativeDecodedPng? TryDecodeFile(string path) => Convert(MapStudioPngDecoder.TryDecodeFile(path));
    public static NativeDecodedPng? TryDecode(ReadOnlySpan<byte> bytes) => Convert(MapStudioPngDecoder.TryDecode(bytes));
    private static NativeDecodedPng? Convert(MapStudioDecodedPng? png) =>
        png is null ? null : new NativeDecodedPng(png.Width, png.Height, png.Rgba);
}
