using System;
using System.IO;
using System.Threading.Tasks;
using MapStudio.Core.IO;
using Xunit;

namespace MapStudio.Core.Tests.IO;

public sealed class SafeFileTransactionTests
{
    [Fact]
    public async Task WriteAllAsync_RejectsTargetChangedAfterSnapshot()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "MapStudio-SafeFileTransaction-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var target = Path.Combine(root, "tile.map");
            var backup = Path.Combine(root, "backups", "tile.map.bak");
            var original = System.Text.Encoding.UTF8.GetBytes("[object]\noriginal\n");
            var external = System.Text.Encoding.UTF8.GetBytes("[object]\nchanged by another editor\n");
            await File.WriteAllBytesAsync(target, original);
            await File.WriteAllBytesAsync(target, external);

            var error = await Assert.ThrowsAsync<InvalidDataException>(
                () => SafeFileTransaction.WriteAllAsync(
                    [
                        new PendingFileWrite(
                            target,
                            backup,
                            System.Text.Encoding.UTF8.GetBytes("[object]\nour edit\n"),
                            original)
                    ]));

            Assert.Equal("fileChangedExternally", error.Message);
            Assert.Equal(external, await File.ReadAllBytesAsync(target));
            Assert.False(File.Exists(backup));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task WriteAllAsync_ReplacesTargetWhenSnapshotStillMatches()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "MapStudio-SafeFileTransaction-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var target = Path.Combine(root, "tile.map");
            var backup = Path.Combine(root, "backups", "tile.map.bak");
            var original = System.Text.Encoding.UTF8.GetBytes("[object]\noriginal\n");
            var updated = System.Text.Encoding.UTF8.GetBytes("[object]\nupdated\n");
            await File.WriteAllBytesAsync(target, original);

            await SafeFileTransaction.WriteAllAsync(
                [
                    new PendingFileWrite(
                        target,
                        backup,
                        updated,
                        original)
                ]);

            Assert.Equal(updated, await File.ReadAllBytesAsync(target));
            Assert.Equal(original, await File.ReadAllBytesAsync(backup));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task WriteAllAsync_RejectsMissingTargetBeforeStagingAnyWrites()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "MapStudio-SafeFileTransaction-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var target = Path.Combine(root, "tile.map");
            var backup = Path.Combine(root, "backups", "tile.map.bak");
            var original = System.Text.Encoding.UTF8.GetBytes("original");
            await File.WriteAllBytesAsync(target, original);

            // All targets are validated before any backup or temporary file
            // is created, so a missing later target cannot partially stage
            // an otherwise valid earlier write.
            var secondTarget = Path.Combine(root, "missing.map");
            await Assert.ThrowsAsync<FileNotFoundException>(
                () => SafeFileTransaction.WriteAllAsync(
                    [
                        new PendingFileWrite(
                            target,
                            backup,
                            System.Text.Encoding.UTF8.GetBytes("updated"),
                            original),
                        new PendingFileWrite(
                            secondTarget,
                            Path.Combine(root, "backups", "missing.bak"),
                            System.Text.Encoding.UTF8.GetBytes("second"))
                    ]));

            // Initial target validation is all-or-nothing: no replacement occurred.
            Assert.Equal(original, await File.ReadAllBytesAsync(target));
            Assert.False(File.Exists(backup));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }


    [Fact]
    public async Task WriteAllAsync_RemainsCompatibleWithoutExplicitSnapshot()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "MapStudio-SafeFileTransaction-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var target = Path.Combine(root, "terrain.terrain");
            var backup = Path.Combine(root, "backups", "terrain.terrain.bak");
            var original = System.Text.Encoding.UTF8.GetBytes("terrain-original");
            var updated = System.Text.Encoding.UTF8.GetBytes("terrain-updated");
            await File.WriteAllBytesAsync(target, original);

            await SafeFileTransaction.WriteAllAsync(
                [
                    new PendingFileWrite(
                        target,
                        backup,
                        updated)
                ]);

            Assert.Equal(updated, await File.ReadAllBytesAsync(target));
            Assert.Equal(original, await File.ReadAllBytesAsync(backup));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }


    [Fact]
    public async Task WriteAllAsync_RejectsDuplicateBackupPathsBeforeWriting()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "MapStudio-SafeFileTransaction-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var firstTarget = Path.Combine(root, "first.map");
            var secondTarget = Path.Combine(root, "second.map");
            var sharedBackup = Path.Combine(root, "backups", "shared.bak");
            var firstOriginal = System.Text.Encoding.UTF8.GetBytes("first-original");
            var secondOriginal = System.Text.Encoding.UTF8.GetBytes("second-original");
            await File.WriteAllBytesAsync(firstTarget, firstOriginal);
            await File.WriteAllBytesAsync(secondTarget, secondOriginal);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => SafeFileTransaction.WriteAllAsync(
                    [
                        new PendingFileWrite(firstTarget, sharedBackup, System.Text.Encoding.UTF8.GetBytes("first-updated")),
                        new PendingFileWrite(secondTarget, sharedBackup, System.Text.Encoding.UTF8.GetBytes("second-updated"))
                    ]));

            Assert.Equal("duplicateBackup", error.Message);
            Assert.Equal(firstOriginal, await File.ReadAllBytesAsync(firstTarget));
            Assert.Equal(secondOriginal, await File.ReadAllBytesAsync(secondTarget));
            Assert.False(Directory.Exists(Path.Combine(root, "backups")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task WriteAllAsync_RejectsBackupThatAliasesAnotherTarget()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "MapStudio-SafeFileTransaction-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var firstTarget = Path.Combine(root, "first.map");
            var secondTarget = Path.Combine(root, "second.map");
            var firstOriginal = System.Text.Encoding.UTF8.GetBytes("first-original");
            var secondOriginal = System.Text.Encoding.UTF8.GetBytes("second-original");
            await File.WriteAllBytesAsync(firstTarget, firstOriginal);
            await File.WriteAllBytesAsync(secondTarget, secondOriginal);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => SafeFileTransaction.WriteAllAsync(
                    [
                        new PendingFileWrite(
                            firstTarget,
                            secondTarget,
                            System.Text.Encoding.UTF8.GetBytes("first-updated")),
                        new PendingFileWrite(
                            secondTarget,
                            Path.Combine(root, "backups", "second.bak"),
                            System.Text.Encoding.UTF8.GetBytes("second-updated"))
                    ]));

            Assert.Equal("backupAliasesTarget", error.Message);
            Assert.Equal(firstOriginal, await File.ReadAllBytesAsync(firstTarget));
            Assert.Equal(secondOriginal, await File.ReadAllBytesAsync(secondTarget));
            Assert.False(Directory.Exists(Path.Combine(root, "backups")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task WriteAllAsync_RejectsExistingBackupBeforeCreatingAnyBackups()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "MapStudio-SafeFileTransaction-" + Guid.NewGuid().ToString("N"));
        var backupDirectory = Path.Combine(root, "backups");
        Directory.CreateDirectory(backupDirectory);

        try
        {
            var firstTarget = Path.Combine(root, "first.map");
            var secondTarget = Path.Combine(root, "second.map");
            var firstBackup = Path.Combine(backupDirectory, "first.bak");
            var secondBackup = Path.Combine(backupDirectory, "second.bak");
            var firstOriginal = System.Text.Encoding.UTF8.GetBytes("first-original");
            var secondOriginal = System.Text.Encoding.UTF8.GetBytes("second-original");
            await File.WriteAllBytesAsync(firstTarget, firstOriginal);
            await File.WriteAllBytesAsync(secondTarget, secondOriginal);
            var protectedBackup = System.Text.Encoding.UTF8.GetBytes("existing-backup");
            await File.WriteAllBytesAsync(secondBackup, protectedBackup);

            var error = await Assert.ThrowsAsync<IOException>(
                () => SafeFileTransaction.WriteAllAsync(
                    [
                        new PendingFileWrite(firstTarget, firstBackup, System.Text.Encoding.UTF8.GetBytes("first-updated")),
                        new PendingFileWrite(secondTarget, secondBackup, System.Text.Encoding.UTF8.GetBytes("second-updated"))
                    ]));

            Assert.Equal("backupAlreadyExists", error.Message);
            Assert.Equal(firstOriginal, await File.ReadAllBytesAsync(firstTarget));
            Assert.Equal(secondOriginal, await File.ReadAllBytesAsync(secondTarget));
            Assert.False(File.Exists(firstBackup));
            Assert.Equal(protectedBackup, await File.ReadAllBytesAsync(secondBackup));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
