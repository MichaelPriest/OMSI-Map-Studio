using System.Numerics;
using System.Runtime.InteropServices;

namespace MapStudio.Renderer.Scene;

[StructLayout(LayoutKind.Sequential)]
public readonly struct NativeMapVertex
{
    public const uint SizeInBytes =
        44;

    public NativeMapVertex(
        Vector3 position,
        Vector4 color)
        : this(
            position,
            color,
            Vector2.Zero,
            Vector2.Zero)
    {
    }

    public NativeMapVertex(
        Vector3 position,
        Vector4 color,
        Vector2 texCoord)
        : this(
            position,
            color,
            texCoord,
            texCoord)
    {
    }

    public NativeMapVertex(
        Vector3 position,
        Vector4 color,
        Vector2 texCoord,
        Vector2 maskTexCoord)
    {
        Position = position;
        Color = color;
        TexCoord = texCoord;
        MaskTexCoord =
            maskTexCoord;
    }

    public Vector3 Position { get; }

    public Vector4 Color { get; }

    public Vector2 TexCoord { get; }

    public Vector2 MaskTexCoord { get; }
}
