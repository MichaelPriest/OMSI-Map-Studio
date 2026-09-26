using MapStudio.Core.Omsi.Maps;

namespace MapStudio.Native.Services;

public sealed record NativeSplineAdvancedUpdateResult(
    NativeMapSnapshot Snapshot,
    OmsiPlacedSpline Spline,
    string BackupPath);
