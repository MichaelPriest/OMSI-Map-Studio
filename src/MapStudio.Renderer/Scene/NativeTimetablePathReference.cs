namespace MapStudio.Renderer.Scene;

public sealed record NativeTimetablePathReference(
    int EntityId,
    string PathIndexText,
    double? Length);
