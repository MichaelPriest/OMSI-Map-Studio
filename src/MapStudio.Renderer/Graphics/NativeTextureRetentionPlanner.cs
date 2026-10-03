namespace MapStudio.Renderer.Graphics;

public sealed record NativeTextureRetentionCandidate(
    string Path,
    long LastAccess,
    long EstimatedBytes);

public static class NativeTextureRetentionPlanner
{
    public static IReadOnlySet<string>
        SelectRetained(
            IEnumerable<
                NativeTextureRetentionCandidate>
                candidates,
            int maxCount,
            long maxBytes)
    {
        ArgumentNullException.ThrowIfNull(
            candidates);

        if (maxCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxCount));
        }

        if (maxBytes < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxBytes));
        }

        var retained =
            new HashSet<string>(
                StringComparer
                    .OrdinalIgnoreCase);

        var retainedBytes =
            0L;

        foreach (
            var candidate in
                candidates
                    .Where(
                        item =>
                            !string
                                .IsNullOrWhiteSpace(
                                    item.Path))
                    .OrderByDescending(
                        item =>
                            item.LastAccess)
                    .ThenBy(
                        item =>
                            item.Path,
                        StringComparer
                            .OrdinalIgnoreCase))
        {
            if (
                retained.Count >=
                    maxCount)
            {
                break;
            }

            if (
                retained.Contains(
                    candidate.Path))
            {
                continue;
            }

            var bytes =
                Math.Max(
                    0,
                    candidate
                        .EstimatedBytes);

            if (
                bytes >
                maxBytes -
                    retainedBytes)
            {
                continue;
            }

            retained.Add(
                candidate.Path);

            retainedBytes +=
                bytes;
        }

        return retained;
    }
}
