namespace MapStudio.Native.Services;

public sealed record NativeTrafficRulesUpdateResult(
    NativeMapSnapshot Snapshot,
    string BackupPath);
