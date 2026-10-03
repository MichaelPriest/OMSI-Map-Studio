namespace MapStudio.Native.Services;

public sealed record NativeBackupRestoreResult(
    NativeMapSnapshot Snapshot,
    int FilesRestored,
    string SourceBackupDirectory,
    string RollbackBackupDirectory);
