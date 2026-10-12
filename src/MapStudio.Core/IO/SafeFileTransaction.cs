using System.Security.Cryptography;

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

        var normalizedBackups =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        var originalBytesByTarget =
            new Dictionary<string, byte[]>(
                StringComparer.OrdinalIgnoreCase);

        // Validate the complete target/backup topology before touching the
        // filesystem. A backup must never alias a target in the same batch.
        foreach (var write in writes)
        {
            ArgumentNullException.ThrowIfNull(write);

            ArgumentException.ThrowIfNullOrWhiteSpace(
                write.TargetPath);

            ArgumentException.ThrowIfNullOrWhiteSpace(
                write.BackupPath);

            var target =
                Path.GetFullPath(
                    write.TargetPath);

            var backup =
                Path.GetFullPath(
                    write.BackupPath);

            if (!normalizedTargets.Add(target))
            {
                throw new InvalidOperationException(
                    "duplicateTarget");
            }

            if (!normalizedBackups.Add(backup))
            {
                throw new InvalidOperationException(
                    "duplicateBackup");
            }

            if (string.Equals(
                    target,
                    backup,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "backupAliasesTarget");
            }
        }

        if (normalizedTargets.Overlaps(normalizedBackups))
        {
            throw new InvalidOperationException(
                "backupAliasesTarget");
        }

        foreach (var write in writes)
        {
            var target =
                Path.GetFullPath(
                    write.TargetPath);

            if (!File.Exists(target))
            {
                throw new FileNotFoundException(
                    "Target file does not exist.",
                    target);
            }

            var originalBytes = File.ReadAllBytes(target);
            EnsureExpectedOriginal(write, originalBytes);
            originalBytesByTarget.Add(target, originalBytes);
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

                EnsureUnchangedSincePreflight(
                    target,
                    originalBytesByTarget[target]);

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

                // Re-check after staging. This narrows the race window and
                // refuses to overwrite a file changed by OMSI or another editor.
                EnsureUnchangedSincePreflight(
                    item.Write.TargetPath,
                    originalBytesByTarget[
                        Path.GetFullPath(item.Write.TargetPath)]);

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
                    // Roll back only while the target still contains the
                    // bytes written by this transaction. If another program
                    // has changed it since replacement, preserve that change
                    // and leave the backup available for manual recovery.
                    if (File.Exists(write.TargetPath) &&
                        File.ReadAllBytes(write.TargetPath)
                            .AsSpan()
                            .SequenceEqual(write.Bytes))
                    {
                        File.Copy(
                            write.BackupPath,
                            write.TargetPath,
                            overwrite: true);
                    }
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

    private static void EnsureExpectedOriginal(
        PendingFileWrite write,
        byte[] currentBytes)
    {
        if (write.ExpectedOriginalBytes is null)
        {
            return;
        }

        if (!HaveSameContent(
                write.ExpectedOriginalBytes,
                currentBytes))
        {
            throw new InvalidDataException(
                "fileChangedExternally");
        }
    }

    private static void EnsureUnchangedSincePreflight(
        string targetPath,
        byte[] originalBytes)
    {
        var currentBytes = File.ReadAllBytes(targetPath);

        if (!HaveSameContent(
                originalBytes,
                currentBytes))
        {
            throw new InvalidDataException(
                "fileChangedDuringTransaction");
        }
    }

    private static bool HaveSameContent(
        byte[] left,
        byte[] right)
    {
        var leftHash = SHA256.HashData(left);
        var rightHash = SHA256.HashData(right);

        return CryptographicOperations.FixedTimeEquals(
            leftHash,
            rightHash);
    }
}
