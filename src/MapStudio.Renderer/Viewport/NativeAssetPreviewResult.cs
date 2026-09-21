using MapStudio.Core.Omsi.Indexing;

namespace MapStudio.Renderer.Viewport;

public sealed record NativeAssetPreviewResult(
    OmsiAssetKind Kind,
    string RelativePath,
    bool IsRenderable,
    int TriangleCount,
    int SourceMeshCount,
    string? ErrorCode,
    byte[]? ThumbnailBmp = null);
