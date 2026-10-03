using MapStudio.Core.Omsi.Maps;

namespace MapStudio.Native.Services;

public sealed record NativeTileCreateResult(
    NativeMapSnapshot Snapshot,
    OmsiTileReference Tile,
    string BackupDirectory,
    IReadOnlyList<string> CreatedFiles);
