using MapStudio.Core.Omsi.Splines;

namespace MapStudio.Native.Services;

public sealed record NativeSplinePathUpdateResult(
    OmsiSplinePathDefinition Path,
    string AssetPath,
    string BackupPath);
