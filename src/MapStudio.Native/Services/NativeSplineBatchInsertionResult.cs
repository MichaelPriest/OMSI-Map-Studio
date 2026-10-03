namespace MapStudio.Native.Services;

public sealed record NativeSplineBatchInsertionResult(
    NativeMapSnapshot Snapshot,
    IReadOnlyList<int> SplineIds,
    string BackupDirectory);
