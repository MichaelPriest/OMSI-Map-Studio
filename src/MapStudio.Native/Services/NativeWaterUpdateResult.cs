using MapStudio.Core.Omsi.Maps;

namespace MapStudio.Native.Services;

public sealed record NativeWaterUpdateResult(
    NativeMapSnapshot Snapshot,
    OmsiTileReference Tile,
    OmsiWaterGrid? Water,
    string BackupDirectory);
