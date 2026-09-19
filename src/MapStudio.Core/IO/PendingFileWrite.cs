namespace MapStudio.Core.IO;

public sealed record PendingFileWrite(
    string TargetPath,
    string BackupPath,
    byte[] Bytes);
