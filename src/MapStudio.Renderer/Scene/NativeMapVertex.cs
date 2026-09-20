using System.Numerics;
using System.Runtime.InteropServices;

namespace MapStudio.Renderer.Scene;

[StructLayout(LayoutKind.Sequential)]
public readonly struct NativeMapVertex
{
    public const uint SizeInBytes =
        28;

    public NativeMapVertex(
        Vector3 position,
        Vector4 color)
    {
        Position = position;
        Color = color;
    }

    public Vector3 Position { get; }

    public Vector4 Color { get; }
}
