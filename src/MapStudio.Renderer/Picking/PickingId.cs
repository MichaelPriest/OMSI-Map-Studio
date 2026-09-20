namespace MapStudio.Renderer.Picking;

public enum PickingKind : byte
{
    None = 0,
    Object = 1,
    Spline = 2,
    Terrain = 3,
    Gizmo = 4
}

public readonly record struct PickingId(
    PickingKind Kind,
    int Value)
{
    public static PickingId None =>
        new(PickingKind.None, 0);

    public bool IsNone =>
        Kind == PickingKind.None ||
        Value <= 0;
}
