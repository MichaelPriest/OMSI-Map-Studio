using MapStudio.Core.Omsi.Maps;

namespace MapStudio.Native.Services;

public sealed record NativeTileDeleteResult(
    NativeMapSnapshot Snapshot,
    OmsiTileReference DeletedTile,
    string BackupDirectory,
    int DeletedFiles);
