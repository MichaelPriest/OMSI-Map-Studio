using MapStudio.Renderer.Viewport;

namespace MapStudio.Native.Services;

public sealed record NativeSceneryPlacementBatchGroup(
    string SceneryObjectPath,
    IReadOnlyList<NativeSceneryPlacementRequest> Placements);
