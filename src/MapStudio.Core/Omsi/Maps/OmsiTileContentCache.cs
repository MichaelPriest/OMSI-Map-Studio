namespace MapStudio.Core.Omsi.Maps;

public sealed class OmsiTileContentCache
{
    private readonly record struct FileStamp(
        bool Exists,
        long Length,
        long LastWriteTimeUtcTicks);

    private readonly record struct TileFingerprint(
        FileStamp Map,
        FileStamp Terrain,
        FileStamp Water,
        FileStamp TerrainRenderData,
        string TextureMaskSignature);

    private sealed record CacheEntry(
        TileFingerprint Fingerprint,
        OmsiTileContent Content,
        long LastAccess);

    private readonly object _gate =
        new();

    private readonly Dictionary<
        string,
        CacheEntry>
        _entries =
            new(
                StringComparer
                    .OrdinalIgnoreCase);

    private readonly OmsiTileReader
        _reader =
            new();

    private readonly int _capacity;

    private long _accessCounter;

    public OmsiTileContentCache(
        int capacity = 32)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacity));
        }

        _capacity =
            capacity;
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    public async Task<OmsiTileContent>
        ReadContentAsync(
            string tilePath,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentException
            .ThrowIfNullOrWhiteSpace(
                tilePath);

        var normalizedPath =
            Path.GetFullPath(
                tilePath);

        var before =
            CaptureFingerprint(
                normalizedPath);

        lock (_gate)
        {
            if (
                _entries.TryGetValue(
                    normalizedPath,
                    out var entry) &&
                entry.Fingerprint ==
                    before)
            {
                _entries[
                    normalizedPath] =
                    entry with
                    {
                        LastAccess =
                            ++_accessCounter
                    };

                return entry.Content;
            }

            _entries.Remove(
                normalizedPath);
        }

        var content =
            await _reader
                .ReadContentAsync(
                    normalizedPath,
                    cancellationToken)
                .ConfigureAwait(false);

        var after =
            CaptureFingerprint(
                normalizedPath);

        if (before != after)
        {
            return content;
        }

        lock (_gate)
        {
            _entries[
                normalizedPath] =
                new CacheEntry(
                    after,
                    content,
                    ++_accessCounter);

            TrimIfNeeded();
        }

        return content;
    }

    public void Invalidate(
        string tilePath)
    {
        if (string.IsNullOrWhiteSpace(
                tilePath))
        {
            return;
        }

        string normalizedPath;

        try
        {
            normalizedPath =
                Path.GetFullPath(
                    tilePath);
        }
        catch (
            Exception exception)
            when (
                exception is
                    IOException or
                    UnauthorizedAccessException or
                    ArgumentException or
                    NotSupportedException or
                    System.Security
                        .SecurityException)
        {
            return;
        }

        lock (_gate)
        {
            _entries.Remove(
                normalizedPath);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
        }
    }

    private void TrimIfNeeded()
    {
        while (
            _entries.Count >
                _capacity)
        {
            string? oldestKey =
                null;

            var oldestAccess =
                long.MaxValue;

            foreach (
                var pair in
                    _entries)
            {
                if (
                    pair.Value
                        .LastAccess >=
                    oldestAccess)
                {
                    continue;
                }

                oldestKey =
                    pair.Key;

                oldestAccess =
                    pair.Value
                        .LastAccess;
            }

            if (oldestKey is null)
            {
                return;
            }

            _entries.Remove(
                oldestKey);
        }
    }

    private static TileFingerprint
        CaptureFingerprint(
            string tilePath) =>
        new(
            CaptureFileStamp(
                tilePath),
            CaptureFileStamp(
                tilePath +
                ".terrain"),
            CaptureFileStamp(
                tilePath +
                ".water"),
            CaptureFileStamp(
                tilePath +
                ".terrain_0.rdy"),
            CaptureTextureMaskSignature(
                tilePath));

    private static FileStamp
        CaptureFileStamp(
            string path)
    {
        try
        {
            var info =
                new FileInfo(
                    path);

            info.Refresh();

            return info.Exists
                ? new FileStamp(
                    true,
                    info.Length,
                    info.LastWriteTimeUtc
                        .Ticks)
                : new FileStamp(
                    false,
                    0,
                    0);
        }
        catch (
            Exception exception)
            when (
                exception is
                    IOException or
                    UnauthorizedAccessException or
                    ArgumentException or
                    NotSupportedException or
                    System.Security
                        .SecurityException)
        {
            return new FileStamp(
                false,
                0,
                0);
        }
    }

    private static string
        CaptureTextureMaskSignature(
            string tilePath)
    {
        try
        {
            var mapDirectory =
                Path.GetDirectoryName(
                    tilePath);

            if (
                string.IsNullOrWhiteSpace(
                    mapDirectory))
            {
                return string.Empty;
            }

            var textureMapDirectory =
                Path.Combine(
                    mapDirectory,
                    "texture",
                    "map");

            if (!Directory.Exists(
                    textureMapDirectory))
            {
                return string.Empty;
            }

            var tileFileName =
                Path.GetFileName(
                    tilePath);

            return string.Join(
                "\n",
                Directory
                    .EnumerateFiles(
                        textureMapDirectory,
                        tileFileName +
                            ".*.dds",
                        SearchOption
                            .TopDirectoryOnly)
                    .OrderBy(
                        path =>
                            path,
                        StringComparer
                            .OrdinalIgnoreCase)
                    .Select(
                        path =>
                        {
                            var stamp =
                                CaptureFileStamp(
                                    path);

                            return
                                Path.GetFileName(
                                    path) +
                                "|" +
                                stamp.Exists +
                                "|" +
                                stamp.Length +
                                "|" +
                                stamp
                                    .LastWriteTimeUtcTicks;
                        }));
        }
        catch (
            Exception exception)
            when (
                exception is
                    IOException or
                    UnauthorizedAccessException or
                    ArgumentException or
                    NotSupportedException or
                    System.Security
                        .SecurityException)
        {
            return string.Empty;
        }
    }
}
