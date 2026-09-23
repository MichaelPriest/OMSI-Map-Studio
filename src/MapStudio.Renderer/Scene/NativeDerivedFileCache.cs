namespace MapStudio.Renderer.Scene;

public sealed class NativeDerivedFileCache<T>
{
    private readonly record struct FileStamp(
        bool Exists,
        long Length,
        long LastWriteTimeUtcTicks);

    private sealed record CacheEntry(
        FileStamp Stamp,
        T Value,
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

    private readonly int _capacity;

    private long _accessCounter;

    public NativeDerivedFileCache(
        int capacity = 512)
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

    public bool TryGet(
        string path,
        out T value)
    {
        value =
            default!;

        if (
            !TryNormalizePath(
                path,
                out var normalizedPath) ||
            !TryCaptureStamp(
                normalizedPath,
                out var currentStamp))
        {
            return false;
        }

        lock (_gate)
        {
            if (
                !_entries.TryGetValue(
                    normalizedPath,
                    out var entry))
            {
                return false;
            }

            if (
                entry.Stamp !=
                    currentStamp)
            {
                _entries.Remove(
                    normalizedPath);

                return false;
            }

            var access =
                ++_accessCounter;

            _entries[
                normalizedPath] =
                entry with
                {
                    LastAccess =
                        access
                };

            value =
                entry.Value;

            return true;
        }
    }

    public void Set(
        string path,
        T value)
    {
        if (
            !TryNormalizePath(
                path,
                out var normalizedPath) ||
            !TryCaptureStamp(
                normalizedPath,
                out var stamp))
        {
            return;
        }

        lock (_gate)
        {
            _entries[
                normalizedPath] =
                new CacheEntry(
                    stamp,
                    value,
                    ++_accessCounter);

            TrimIfNeeded();
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

    private static bool TryNormalizePath(
        string path,
        out string normalizedPath)
    {
        normalizedPath =
            string.Empty;

        if (string.IsNullOrWhiteSpace(
                path))
        {
            return false;
        }

        try
        {
            normalizedPath =
                Path.GetFullPath(
                    path);

            return true;
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
            return false;
        }
    }

    private static bool TryCaptureStamp(
        string normalizedPath,
        out FileStamp stamp)
    {
        try
        {
            var info =
                new FileInfo(
                    normalizedPath);

            info.Refresh();

            if (!info.Exists)
            {
                stamp =
                    new FileStamp(
                        false,
                        0,
                        0);

                return true;
            }

            stamp =
                new FileStamp(
                    true,
                    info.Length,
                    info.LastWriteTimeUtc
                        .Ticks);

            return true;
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
            stamp =
                default;

            return false;
        }
    }
}
