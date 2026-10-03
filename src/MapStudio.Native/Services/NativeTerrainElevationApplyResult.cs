namespace MapStudio.Native.Services;

public sealed record NativeTerrainElevationApplyResult(
    NativeMapSnapshot Snapshot,
    int ChangedSamples,
    string BackupDirectory);
