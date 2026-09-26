namespace MapStudio.Native.Services;

public sealed record NativeAssetPathDeleteResult(
    string AssetPath,
    string BackupPath,
    int RemainingPathCount);
