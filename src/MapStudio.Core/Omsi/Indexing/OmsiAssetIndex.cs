using System.Globalization;
using Microsoft.Data.Sqlite;

namespace MapStudio.Core.Omsi.Indexing;

public enum OmsiAssetKind
{
    SceneryObject = 1,
    Spline = 2,
    Model = 3,
    Texture = 4
}

public sealed record OmsiAssetIndexEntry(
    string RelativePath,
    OmsiAssetKind Kind,
    long Size,
    long LastWriteUtcTicks);

public sealed record OmsiAssetIndexProgress(
    int ExaminedFiles,
    int CandidateFiles,
    string? RelativePath);

public sealed record OmsiAssetIndexRefreshResult(
    int ExaminedFiles,
    int TotalEntries,
    int AddedFiles,
    int UpdatedFiles,
    int UnchangedFiles,
    int RemovedFiles,
    long DurationMilliseconds);

public sealed record OmsiAssetIndexStatistics(
    int TotalEntries,
    int SceneryObjects,
    int Splines,
    int Models,
    int Textures,
    DateTimeOffset? RefreshedAtUtc);

public sealed class OmsiAssetIndex
{
    private const int SchemaVersion = 1;

    private static readonly HashSet<string>
        TextureExtensions =
            new(
                [
                    ".bmp",
                    ".dds",
                    ".png",
                    ".jpg",
                    ".jpeg",
                    ".tga"
                ],
                StringComparer.OrdinalIgnoreCase);

    private readonly string _databasePath;
    private readonly string _connectionString;

    public OmsiAssetIndex(
        string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            databasePath);

        _databasePath =
            Path.GetFullPath(
                databasePath);

        _connectionString =
            new SqliteConnectionStringBuilder
            {
                DataSource =
                    _databasePath,
                Mode =
                    SqliteOpenMode
                        .ReadWriteCreate,
                Cache =
                    SqliteCacheMode.Shared
            }
            .ToString();
    }

    public string DatabasePath =>
        _databasePath;

    public async Task InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        var parent =
            Path.GetDirectoryName(
                _databasePath);

        if (!string.IsNullOrWhiteSpace(parent))
        {
            Directory.CreateDirectory(
                parent);
        }

        await using var connection =
            new SqliteConnection(
                _connectionString);

        await connection
            .OpenAsync(
                cancellationToken)
            .ConfigureAwait(false);

        await using var command =
            connection.CreateCommand();

        command.CommandText =
            """
            PRAGMA journal_mode=WAL;
            PRAGMA synchronous=NORMAL;

            CREATE TABLE IF NOT EXISTS metadata (
                key TEXT PRIMARY KEY NOT NULL,
                value TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS assets (
                relative_path TEXT PRIMARY KEY COLLATE NOCASE NOT NULL,
                kind INTEGER NOT NULL,
                size INTEGER NOT NULL,
                last_write_utc_ticks INTEGER NOT NULL,
                seen_generation INTEGER NOT NULL,
                indexed_utc_ticks INTEGER NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_assets_kind
                ON assets(kind);
            """;

        await command
            .ExecuteNonQueryAsync(
                cancellationToken)
            .ConfigureAwait(false);

        await WriteMetadataAsync(
                connection,
                transaction: null,
                key: "schema_version",
                value:
                    SchemaVersion.ToString(
                        CultureInfo.InvariantCulture),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<OmsiAssetIndexRefreshResult>
        RefreshAsync(
            string omsiRoot,
            IProgress<OmsiAssetIndexProgress>? progress = null,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            omsiRoot);

        var started =
            DateTimeOffset.UtcNow;

        var root =
            Path.GetFullPath(
                omsiRoot);

        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException(
                root);
        }

        await InitializeAsync(
                cancellationToken)
            .ConfigureAwait(false);

        var scan =
            await Task.Run(
                    () =>
                        ScanInstallation(
                            root,
                            progress,
                            cancellationToken),
                    cancellationToken)
                .ConfigureAwait(false);

        await using var connection =
            new SqliteConnection(
                _connectionString);

        await connection
            .OpenAsync(
                cancellationToken)
            .ConfigureAwait(false);

        var existing =
            await ReadExistingAsync(
                    connection,
                    cancellationToken)
                .ConfigureAwait(false);

        var previousGeneration =
            await ReadGenerationAsync(
                    connection,
                    cancellationToken)
                .ConfigureAwait(false);

        var generation =
            previousGeneration ==
                int.MaxValue
                ? 1
                : previousGeneration + 1;

        var added = 0;
        var updated = 0;
        var unchanged = 0;

        using var transaction =
            connection.BeginTransaction();

        await using var upsert =
            connection.CreateCommand();

        upsert.Transaction =
            transaction;

        upsert.CommandText =
            """
            INSERT INTO assets (
                relative_path,
                kind,
                size,
                last_write_utc_ticks,
                seen_generation,
                indexed_utc_ticks
            )
            VALUES (
                $relative_path,
                $kind,
                $size,
                $last_write_utc_ticks,
                $seen_generation,
                $indexed_utc_ticks
            )
            ON CONFLICT(relative_path)
            DO UPDATE SET
                kind = excluded.kind,
                size = excluded.size,
                last_write_utc_ticks = excluded.last_write_utc_ticks,
                seen_generation = excluded.seen_generation,
                indexed_utc_ticks = excluded.indexed_utc_ticks;
            """;

        var relativePathParameter =
            upsert.Parameters.Add(
                "$relative_path",
                SqliteType.Text);
        var kindParameter =
            upsert.Parameters.Add(
                "$kind",
                SqliteType.Integer);
        var sizeParameter =
            upsert.Parameters.Add(
                "$size",
                SqliteType.Integer);
        var lastWriteParameter =
            upsert.Parameters.Add(
                "$last_write_utc_ticks",
                SqliteType.Integer);
        var seenGenerationParameter =
            upsert.Parameters.Add(
                "$seen_generation",
                SqliteType.Integer);
        var indexedParameter =
            upsert.Parameters.Add(
                "$indexed_utc_ticks",
                SqliteType.Integer);

        await using var markSeen =
            connection.CreateCommand();

        markSeen.Transaction =
            transaction;

        markSeen.CommandText =
            """
            UPDATE assets
            SET seen_generation = $seen_generation
            WHERE relative_path = $relative_path;
            """;

        var markSeenGenerationParameter =
            markSeen.Parameters.Add(
                "$seen_generation",
                SqliteType.Integer);
        var markSeenPathParameter =
            markSeen.Parameters.Add(
                "$relative_path",
                SqliteType.Text);

        var indexedUtcTicks =
            DateTimeOffset.UtcNow
                .UtcDateTime
                .Ticks;

        foreach (var asset in scan.Assets)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            if (
                existing.TryGetValue(
                    asset.RelativePath,
                    out var previous) &&
                previous.Kind == asset.Kind &&
                previous.Size == asset.Size &&
                previous.LastWriteUtcTicks ==
                    asset.LastWriteUtcTicks)
            {
                markSeenGenerationParameter.Value =
                    generation;
                markSeenPathParameter.Value =
                    asset.RelativePath;

                await markSeen
                    .ExecuteNonQueryAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

                unchanged++;
                continue;
            }

            relativePathParameter.Value =
                asset.RelativePath;
            kindParameter.Value =
                (int)asset.Kind;
            sizeParameter.Value =
                asset.Size;
            lastWriteParameter.Value =
                asset.LastWriteUtcTicks;
            seenGenerationParameter.Value =
                generation;
            indexedParameter.Value =
                indexedUtcTicks;

            await upsert
                .ExecuteNonQueryAsync(
                    cancellationToken)
                .ConfigureAwait(false);

            if (
                existing.ContainsKey(
                    asset.RelativePath))
            {
                updated++;
            }
            else
            {
                added++;
            }
        }

        await using var removeStale =
            connection.CreateCommand();

        removeStale.Transaction =
            transaction;

        removeStale.CommandText =
            """
            DELETE FROM assets
            WHERE seen_generation <> $generation;
            """;

        removeStale.Parameters.AddWithValue(
            "$generation",
            generation);

        var removed =
            await removeStale
                .ExecuteNonQueryAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        await WriteMetadataAsync(
                connection,
                transaction,
                "generation",
                generation.ToString(
                    CultureInfo.InvariantCulture),
                cancellationToken)
            .ConfigureAwait(false);

        await WriteMetadataAsync(
                connection,
                transaction,
                "refreshed_utc_ticks",
                DateTimeOffset.UtcNow
                    .UtcDateTime
                    .Ticks
                    .ToString(
                        CultureInfo.InvariantCulture),
                cancellationToken)
            .ConfigureAwait(false);

        transaction.Commit();

        var totalEntries =
            scan.Assets.Count;

        return new OmsiAssetIndexRefreshResult(
            ExaminedFiles:
                scan.ExaminedFiles,
            TotalEntries:
                totalEntries,
            AddedFiles:
                added,
            UpdatedFiles:
                updated,
            UnchangedFiles:
                unchanged,
            RemovedFiles:
                removed,
            DurationMilliseconds:
                (long)(
                    DateTimeOffset.UtcNow -
                    started)
                .TotalMilliseconds);
    }

    public async Task<OmsiAssetIndexStatistics>
        GetStatisticsAsync(
            CancellationToken cancellationToken = default)
    {
        await InitializeAsync(
                cancellationToken)
            .ConfigureAwait(false);

        await using var connection =
            new SqliteConnection(
                _connectionString);

        await connection
            .OpenAsync(
                cancellationToken)
            .ConfigureAwait(false);

        var counts =
            new Dictionary<
                OmsiAssetKind,
                int>();

        await using (
            var command =
                connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT kind, COUNT(*)
                FROM assets
                GROUP BY kind;
                """;

            await using var reader =
                await command
                    .ExecuteReaderAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

            while (
                await reader
                    .ReadAsync(
                        cancellationToken)
                    .ConfigureAwait(false))
            {
                if (
                    Enum.IsDefined(
                        typeof(OmsiAssetKind),
                        reader.GetInt32(0)))
                {
                    counts[
                        (OmsiAssetKind)
                            reader.GetInt32(0)] =
                        reader.GetInt32(1);
                }
            }
        }

        var refreshedTicksText =
            await ReadMetadataAsync(
                    connection,
                    "refreshed_utc_ticks",
                    cancellationToken)
                .ConfigureAwait(false);

        DateTimeOffset? refreshedAtUtc =
            null;

        if (
            long.TryParse(
                refreshedTicksText,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var refreshedTicks) &&
            refreshedTicks > 0)
        {
            refreshedAtUtc =
                new DateTimeOffset(
                    new DateTime(
                        refreshedTicks,
                        DateTimeKind.Utc));
        }

        int Count(
            OmsiAssetKind kind) =>
            counts.TryGetValue(
                kind,
                out var value)
                ? value
                : 0;

        var sceneryObjects =
            Count(
                OmsiAssetKind
                    .SceneryObject);
        var splines =
            Count(
                OmsiAssetKind
                    .Spline);
        var models =
            Count(
                OmsiAssetKind
                    .Model);
        var textures =
            Count(
                OmsiAssetKind
                    .Texture);

        return new OmsiAssetIndexStatistics(
            TotalEntries:
                sceneryObjects +
                splines +
                models +
                textures,
            SceneryObjects:
                sceneryObjects,
            Splines:
                splines,
            Models:
                models,
            Textures:
                textures,
            RefreshedAtUtc:
                refreshedAtUtc);
    }

    public async Task<
        IReadOnlyList<OmsiAssetIndexEntry>>
        GetEntriesAsync(
            OmsiAssetKind? kind = null,
            int limit = 100_000,
            CancellationToken cancellationToken = default)
    {
        if (
            limit <= 0 ||
            limit > 1_000_000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit));
        }

        await InitializeAsync(
                cancellationToken)
            .ConfigureAwait(false);

        await using var connection =
            new SqliteConnection(
                _connectionString);

        await connection
            .OpenAsync(
                cancellationToken)
            .ConfigureAwait(false);

        await using var command =
            connection.CreateCommand();

        command.CommandText =
            kind is null
                ? """
                  SELECT
                      relative_path,
                      kind,
                      size,
                      last_write_utc_ticks
                  FROM assets
                  ORDER BY relative_path
                  LIMIT $limit;
                  """
                : """
                  SELECT
                      relative_path,
                      kind,
                      size,
                      last_write_utc_ticks
                  FROM assets
                  WHERE kind = $kind
                  ORDER BY relative_path
                  LIMIT $limit;
                  """;

        command.Parameters.AddWithValue(
            "$limit",
            limit);

        if (kind is not null)
        {
            command.Parameters.AddWithValue(
                "$kind",
                (int)kind.Value);
        }

        var entries =
            new List<OmsiAssetIndexEntry>();

        await using var reader =
            await command
                .ExecuteReaderAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        while (
            await reader
                .ReadAsync(
                    cancellationToken)
                .ConfigureAwait(false))
        {
            entries.Add(
                new OmsiAssetIndexEntry(
                    reader.GetString(0),
                    (OmsiAssetKind)
                        reader.GetInt32(1),
                    reader.GetInt64(2),
                    reader.GetInt64(3)));
        }

        return entries;
    }

    private static AssetScanResult
        ScanInstallation(
            string root,
            IProgress<OmsiAssetIndexProgress>? progress,
            CancellationToken cancellationToken)
    {
        var examined = 0;

        var assets =
            new Dictionary<
                string,
                OmsiAssetIndexEntry>(
                StringComparer.OrdinalIgnoreCase);

        foreach (
            var rootName in
                new[]
                {
                    "Sceneryobjects",
                    "Splines",
                    "Texture"
                })
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            var assetRoot =
                Path.Combine(
                    root,
                    rootName);

            if (!Directory.Exists(assetRoot))
            {
                continue;
            }

            foreach (
                var filePath in
                    EnumerateFilesSafe(
                        assetRoot,
                        cancellationToken))
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                examined++;

                if (!TryClassify(
                    filePath,
                    out var kind))
                {
                    if (
                        examined % 512 == 0)
                    {
                        progress?.Report(
                            new OmsiAssetIndexProgress(
                                examined,
                                assets.Count,
                                null));
                    }

                    continue;
                }

                try
                {
                    var info =
                        new FileInfo(
                            filePath);

                    var relativePath =
                        NormalizeRelativePath(
                            root,
                            filePath);

                    assets[
                        relativePath] =
                        new OmsiAssetIndexEntry(
                            relativePath,
                            kind,
                            info.Length,
                            info.LastWriteTimeUtc
                                .Ticks);

                    if (
                        examined % 256 == 0)
                    {
                        progress?.Report(
                            new OmsiAssetIndexProgress(
                                examined,
                                assets.Count,
                                relativePath));
                    }
                }
                catch (
                    Exception exception)
                    when (
                        exception is
                            IOException or
                            UnauthorizedAccessException or
                            FileNotFoundException or
                            DirectoryNotFoundException)
                {
                    // A file can disappear while an external tool
                    // updates the OMSI installation. The next scan
                    // can pick it up again.
                }
            }
        }

        progress?.Report(
            new OmsiAssetIndexProgress(
                examined,
                assets.Count,
                null));

        return new AssetScanResult(
            examined,
            assets.Values
                .OrderBy(
                    static asset =>
                        asset.RelativePath,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    private static IEnumerable<string>
        EnumerateFilesSafe(
            string root,
            CancellationToken cancellationToken)
    {
        var pending =
            new Stack<string>();

        pending.Push(root);

        while (pending.Count > 0)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            var current =
                pending.Pop();

            string[] files;
            string[] directories;

            try
            {
                files =
                    Directory
                        .GetFiles(current);

                directories =
                    Directory
                        .GetDirectories(current);
            }
            catch (
                Exception exception)
                when (
                    exception is
                        IOException or
                        UnauthorizedAccessException or
                        DirectoryNotFoundException)
            {
                continue;
            }

            foreach (var file in files)
            {
                yield return file;
            }

            foreach (
                var directory in directories)
            {
                try
                {
                    if (
                        (File.GetAttributes(
                            directory) &
                            FileAttributes
                                .ReparsePoint) != 0)
                    {
                        continue;
                    }
                }
                catch (
                    Exception exception)
                    when (
                        exception is
                            IOException or
                            UnauthorizedAccessException or
                            DirectoryNotFoundException)
                {
                    continue;
                }

                pending.Push(
                    directory);
            }
        }
    }

    private static bool TryClassify(
        string path,
        out OmsiAssetKind kind)
    {
        var extension =
            Path.GetExtension(path);

        if (
            string.Equals(
                extension,
                ".sco",
                StringComparison.OrdinalIgnoreCase))
        {
            kind =
                OmsiAssetKind
                    .SceneryObject;
            return true;
        }

        if (
            string.Equals(
                extension,
                ".sli",
                StringComparison.OrdinalIgnoreCase))
        {
            kind =
                OmsiAssetKind
                    .Spline;
            return true;
        }

        if (
            string.Equals(
                extension,
                ".o3d",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                extension,
                ".x",
                StringComparison.OrdinalIgnoreCase))
        {
            kind =
                OmsiAssetKind
                    .Model;
            return true;
        }

        if (
            TextureExtensions.Contains(
                extension))
        {
            kind =
                OmsiAssetKind
                    .Texture;
            return true;
        }

        kind = default;
        return false;
    }

    private static string NormalizeRelativePath(
        string root,
        string path) =>
        Path.GetRelativePath(
                root,
                Path.GetFullPath(path))
            .Replace(
                Path.DirectorySeparatorChar,
                '/')
            .Replace(
                Path.AltDirectorySeparatorChar,
                '/');

    private static async Task<
        Dictionary<string, OmsiAssetIndexEntry>>
        ReadExistingAsync(
            SqliteConnection connection,
            CancellationToken cancellationToken)
    {
        var result =
            new Dictionary<
                string,
                OmsiAssetIndexEntry>(
                StringComparer.OrdinalIgnoreCase);

        await using var command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                relative_path,
                kind,
                size,
                last_write_utc_ticks
            FROM assets;
            """;

        await using var reader =
            await command
                .ExecuteReaderAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        while (
            await reader
                .ReadAsync(
                    cancellationToken)
                .ConfigureAwait(false))
        {
            var entry =
                new OmsiAssetIndexEntry(
                    reader.GetString(0),
                    (OmsiAssetKind)
                        reader.GetInt32(1),
                    reader.GetInt64(2),
                    reader.GetInt64(3));

            result[
                entry.RelativePath] =
                entry;
        }

        return result;
    }

    private static async Task<int>
        ReadGenerationAsync(
            SqliteConnection connection,
            CancellationToken cancellationToken)
    {
        var value =
            await ReadMetadataAsync(
                    connection,
                    "generation",
                    cancellationToken)
                .ConfigureAwait(false);

        return
            int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var generation)
                ? Math.Max(
                    0,
                    generation)
                : 0;
    }

    private static async Task<string?>
        ReadMetadataAsync(
            SqliteConnection connection,
            string key,
            CancellationToken cancellationToken)
    {
        await using var command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT value
            FROM metadata
            WHERE key = $key
            LIMIT 1;
            """;

        command.Parameters.AddWithValue(
            "$key",
            key);

        var value =
            await command
                .ExecuteScalarAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        return value as string;
    }

    private static async Task
        WriteMetadataAsync(
            SqliteConnection connection,
            SqliteTransaction? transaction,
            string key,
            string value,
            CancellationToken cancellationToken)
    {
        await using var command =
            connection.CreateCommand();

        command.Transaction =
            transaction;

        command.CommandText =
            """
            INSERT INTO metadata(key, value)
            VALUES($key, $value)
            ON CONFLICT(key)
            DO UPDATE SET value = excluded.value;
            """;

        command.Parameters.AddWithValue(
            "$key",
            key);
        command.Parameters.AddWithValue(
            "$value",
            value);

        await command
            .ExecuteNonQueryAsync(
                cancellationToken)
            .ConfigureAwait(false);
    }

    private sealed record AssetScanResult(
        int ExaminedFiles,
        IReadOnlyList<OmsiAssetIndexEntry> Assets);
}
