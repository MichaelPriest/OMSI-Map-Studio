using System.Numerics;
using System.Runtime.InteropServices;

namespace MapStudio.Renderer.Scene;

[StructLayout(LayoutKind.Sequential)]
public readonly struct NativeMapVertex
{
    public const uint SizeInBytes =
        80;

    public NativeMapVertex(
        Vector3 position,
        Vector4 color)
        : this(
            position,
            color,
            Vector2.Zero,
            Vector2.Zero,
            Vector2.Zero,
            Vector3.UnitY,
            new Vector4(
                Vector3.UnitX,
                1.0f))
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
            texCoord,
            texCoord,
            Vector3.UnitY,
            new Vector4(
                Vector3.UnitX,
                1.0f))
    {
    }

    public NativeMapVertex(
        Vector3 position,
        Vector4 color,
        Vector2 texCoord,
        Vector2 maskTexCoord)
        : this(
            position,
            color,
            texCoord,
            maskTexCoord,
            texCoord,
            Vector3.UnitY,
            new Vector4(
                Vector3.UnitX,
                1.0f))
    {
    }

    public NativeMapVertex(
        Vector3 position,
        Vector4 color,
        Vector2 texCoord,
        Vector2 maskTexCoord,
        Vector2 detailTexCoord)
        : this(
            position,
            color,
            texCoord,
            maskTexCoord,
            detailTexCoord,
            Vector3.UnitY,
            new Vector4(
                Vector3.UnitX,
                1.0f))
    {
    }

    public NativeMapVertex(
        Vector3 position,
        Vector4 color,
        Vector2 texCoord,
        Vector2 maskTexCoord,
        Vector2 detailTexCoord,
        Vector3 normal,
        Vector4 tangent)
    {
        Position = position;
        Color = color;
        TexCoord = texCoord;
        MaskTexCoord =
            maskTexCoord;
        DetailTexCoord =
            detailTexCoord;
        Normal =
            normal;
        Tangent =
            tangent;
    }

    public Vector3 Position { get; }

    public Vector4 Color { get; }

    public Vector2 TexCoord { get; }

    public Vector2 MaskTexCoord { get; }

    public Vector2 DetailTexCoord { get; }

    public Vector3 Normal { get; }

    public Vector4 Tangent { get; }
}
