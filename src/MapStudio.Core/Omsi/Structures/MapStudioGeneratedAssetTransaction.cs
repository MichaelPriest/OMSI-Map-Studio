namespace MapStudio.Core.Omsi.Structures;

public sealed record MapStudioGeneratedAssetSnapshotRoot(
    string OriginalPath,
    string BackupPath,
    bool ExistedBefore);

public sealed record MapStudioGeneratedAssetSnapshot(
    string BackupRoot,
    IReadOnlyList<MapStudioGeneratedAssetSnapshotRoot> Roots);

public sealed class MapStudioGeneratedAssetTransaction
{
    public async Task<MapStudioGeneratedAssetSnapshot> CaptureAsync(
        string omsiRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(omsiRoot);

        var root = Path.GetFullPath(omsiRoot);
        var backupRoot =
            Path.Combine(
                root,
                ".mapstudio",
                "backups",
                "real-world-generated-assets",
                DateTime.UtcNow.ToString(
                    "yyyyMMdd-HHmmss",
                    System.Globalization.CultureInfo.InvariantCulture) +
                "-" +
                Guid.NewGuid().ToString("N")[..8]);

        Directory.CreateDirectory(backupRoot);

        var snapshots =
            new List<MapStudioGeneratedAssetSnapshotRoot>();

        foreach (var generatedRoot in GetGeneratedRoots(root))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var existed = Directory.Exists(generatedRoot);
            var backupPath =
                Path.Combine(
                    backupRoot,
                    Path.GetRelativePath(root, generatedRoot));

            if (existed)
            {
                await Task.Run(
                        () =>
                            CopyDirectory(
                                generatedRoot,
                                backupPath,
                                cancellationToken),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            snapshots.Add(
                new MapStudioGeneratedAssetSnapshotRoot(
                    generatedRoot,
                    backupPath,
                    existed));
        }

        return new MapStudioGeneratedAssetSnapshot(
            backupRoot,
            snapshots);
    }

    public void Restore(
        MapStudioGeneratedAssetSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        foreach (var root in snapshot.Roots)
        {
            if (Directory.Exists(root.OriginalPath))
            {
                Directory.Delete(
                    root.OriginalPath,
                    recursive: true);
            }

            if (!root.ExistedBefore)
            {
                continue;
            }

            if (!Directory.Exists(root.BackupPath))
            {
                throw new InvalidDataException(
                    "generatedAssetSnapshotMissing");
            }

            CopyDirectory(
                root.BackupPath,
                root.OriginalPath,
                CancellationToken.None);
        }
    }

    private static IReadOnlyList<string> GetGeneratedRoots(
        string omsiRoot) =>
        [
            Path.Combine(
                omsiRoot,
                "Splines",
                "MapStudio_RoadKit"),
            Path.Combine(
                omsiRoot,
                "Sceneryobjects",
                "MapStudio_Junctions"),
            Path.Combine(
                omsiRoot,
                "Sceneryobjects",
                "MapStudio_Buildings"),
            Path.Combine(
                omsiRoot,
                "Sceneryobjects",
                "MapStudio_Infrastructure")
        ];

    private static void CopyDirectory(
        string source,
        string destination,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destination);

        foreach (
            var directory in
                Directory.EnumerateDirectories(
                    source,
                    "*",
                    SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            Directory.CreateDirectory(
                Path.Combine(
                    destination,
                    Path.GetRelativePath(
                        source,
                        directory)));
        }

        foreach (
            var file in
                Directory.EnumerateFiles(
                    source,
                    "*",
                    SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var target =
                Path.Combine(
                    destination,
                    Path.GetRelativePath(
                        source,
                        file));

            Directory.CreateDirectory(
                Path.GetDirectoryName(target)!);

            File.Copy(
                file,
                target,
                overwrite: true);
        }
    }
}
