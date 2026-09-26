namespace MapStudio.Renderer.Scene;

public sealed record NativeReferenceOverlayGeometry(
    NativeMapVertex[] Vertices,
    string TexturePath)
{
    public int TriangleCount =>
        Vertices.Length /
        3;
}
