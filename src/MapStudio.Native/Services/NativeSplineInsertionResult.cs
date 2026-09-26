namespace MapStudio.Native.Services;

public sealed record NativeSplineInsertionResult(
    NativeMapSnapshot Snapshot,
    int SplineId);
