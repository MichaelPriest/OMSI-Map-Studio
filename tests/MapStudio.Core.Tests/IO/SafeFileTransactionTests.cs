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

}
