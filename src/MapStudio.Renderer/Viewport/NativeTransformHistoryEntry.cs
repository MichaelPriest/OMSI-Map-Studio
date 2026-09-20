namespace MapStudio.Renderer.Viewport;

public sealed record NativeTransformHistoryEntry(
    NativePendingTransformEdit Before,
    NativePendingTransformEdit After);
