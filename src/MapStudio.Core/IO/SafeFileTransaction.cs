namespace MapStudio.Core.IO;

public static class SafeFileTransaction
{
    public static async Task WriteAllAsync(
        IReadOnlyList<PendingFileWrite> writes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(writes);

        if (writes.Count == 0)
        {
            return;
        }

        var normalizedTargets =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var write in writes)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(
                write.TargetPath);

            ArgumentException.ThrowIfNullOrWhiteSpace(
                write.BackupPath);

            var target =
                Path.GetFullPath(
                    write.TargetPath);

            if (
                !normalizedTargets.Add(
                    target))
            {
                throw new InvalidOperationException(
                    "duplicateTarget");
            }

            if (!File.Exists(target))
            {
                throw new FileNotFoundException(
                    "Target file does not exist.",
                    target);
            }
        }

        var staged =
            new List<(
                PendingFileWrite Write,
                string TempPath)>(
                    writes.Count);

        var replaced =
            new List<PendingFileWrite>(
                writes.Count);

        try
        {
            foreach (var write in writes)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                var target =
                    Path.GetFullPath(
                        write.TargetPath);

                var targetDirectory =
                    Path.GetDirectoryName(
                        target) ??
                    throw new InvalidOperationException(
                        "targetDirectoryMissing");

                var backup =
                    Path.GetFullPath(
                        write.BackupPath);

                var backupDirectory =
                    Path.GetDirectoryName(
                        backup) ??
                    throw new InvalidOperationException(
                        "backupDirectoryMissing");

                Directory.CreateDirectory(
                    backupDirectory);

                File.Copy(
                    target,
                    backup,
                    overwrite: false);

                var tempPath =
                    Path.Combine(
                        targetDirectory,
                        $".{Path.GetFileName(target)}.mapstudio-{Guid.NewGuid():N}.tmp");

                await File.WriteAllBytesAsync(
                    tempPath,
                    write.Bytes,
                    cancellationToken);

                staged.Add((
                    write,
                    tempPath));
            }

            foreach (var item in staged)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                File.Replace(
                    item.TempPath,
                    item.Write.TargetPath,
                    destinationBackupFileName: null,
                    ignoreMetadataErrors: true);

                replaced.Add(
                    item.Write);
            }
        }
        catch
        {
            foreach (var write in
                replaced.AsEnumerable()
                    .Reverse())
            {
                try
                {
                    File.Copy(
                        write.BackupPath,
                        write.TargetPath,
                        overwrite: true);
                }
                catch
                {
                    // Keep the original exception.
                    // Backup remains available.
                }
            }

            throw;
        }
        finally
        {
            foreach (var item in staged)
            {
                try
                {
                    if (File.Exists(
                            item.TempPath))
                    {
                        File.Delete(
                            item.TempPath);
                    }
                }
                catch
                {
                    // Best effort temp cleanup.
                }
            }
        }
    }
}
