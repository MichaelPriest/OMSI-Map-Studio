namespace MapStudio.Native.Services;

public sealed record NativeAssetReplacementResult(
    NativeMapSnapshot Snapshot,
    int Replacements,
    int FilesSaved,
    string BackupDirectory);
