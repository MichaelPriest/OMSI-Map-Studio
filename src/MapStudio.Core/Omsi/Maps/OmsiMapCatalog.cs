using System.Collections.Concurrent;
using System.Globalization;
using MapStudio.Core.Omsi.Config;

namespace MapStudio.Core.Omsi.Maps;

public sealed class OmsiMapCatalog
{
    private const int MaxConcurrentGlobalReads = 4;
    private static readonly TimeSpan PerMapTimeout =
        TimeSpan.FromSeconds(20);

    public async Task<IReadOnlyList<OmsiMapDescriptor>> DiscoverAsync(
        string omsiRoot,
        CancellationToken cancellationToken = default) =>
        (await DiscoverWithProgressAsync(
            omsiRoot,
            progress: null,
            cancellationToken)).Maps;

    public async Task<OmsiMapCatalogResult> DiscoverWithProgressAsync(
        string omsiRoot,
        IProgress<OmsiMapDiscoveryProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(omsiRoot);

        var mapsDirectory = Path.Combine(omsiRoot, "maps");

        if (!Directory.Exists(mapsDirectory))
        {
            return new OmsiMapCatalogResult(
                Array.Empty<OmsiMapDescriptor>(),
                SkippedMaps: 0);
        }

        var directories = Directory
            .EnumerateDirectories(mapsDirectory)
            .Where(directory =>
                File.Exists(
                    Path.Combine(
                        directory,
                        "global.cfg")))
            .OrderBy(static path => path)
            .Select((path, index) =>
                new
                {
                    Path = path,
                    Index = index
                })
            .ToArray();

        if (directories.Length == 0)
        {
            progress?.Report(
                new OmsiMapDiscoveryProgress(
                    Completed: 0,
                    Total: 0,
                    Skipped: 0,
                    DirectoryName: null));

            return new OmsiMapCatalogResult(
                Array.Empty<OmsiMapDescriptor>(),
                SkippedMaps: 0);
        }

        var results =
            new ConcurrentDictionary<int, OmsiMapDescriptor>();

        var completed = 0;
        var skipped = 0;

        await Parallel.ForEachAsync(
            directories,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism =
                    Math.Min(
                        MaxConcurrentGlobalReads,
                        Math.Max(
                            1,
                            Environment.ProcessorCount))
            },
            async (entry, token) =>
            {
                var directoryName =
                    Path.GetFileName(entry.Path);

                try
                {
                    using var timeout =
                        CancellationTokenSource
                            .CreateLinkedTokenSource(token);

                    timeout.CancelAfter(
                        PerMapTimeout);

                    var descriptor =
                        await ReadDescriptorAsync(
                            entry.Path,
                            timeout.Token);

                    results[entry.Index] =
                        descriptor;
                }
                catch (OperationCanceledException)
                    when (!token.IsCancellationRequested)
                {
                    Interlocked.Increment(
                        ref skipped);
                }
                catch (Exception exception)
                    when (IsRecoverableMapError(
                        exception))
                {
                    Interlocked.Increment(
                        ref skipped);
                }
                finally
                {
                    var current =
                        Interlocked.Increment(
                            ref completed);

                    progress?.Report(
                        new OmsiMapDiscoveryProgress(
                            Completed: current,
                            Total: directories.Length,
                            Skipped: Volatile.Read(
                                ref skipped),
                            DirectoryName:
                                directoryName));
                }
            });

        return new OmsiMapCatalogResult(
            results
                .OrderBy(pair => pair.Key)
                .Select(pair => pair.Value)
                .ToArray(),
            SkippedMaps:
                Volatile.Read(ref skipped));
    }

    public static async Task<OmsiMapDescriptor> OpenMapAsync(
        string directory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        var fullDirectory =
            Path.GetFullPath(directory);

        var globalConfigPath =
            Path.Combine(
                fullDirectory,
                "global.cfg");

        if (!File.Exists(globalConfigPath))
        {
            throw new FileNotFoundException(
                "The selected map does not contain global.cfg.",
                globalConfigPath);
        }

        return await ReadDescriptorAsync(
            fullDirectory,
            cancellationToken);
    }

    private static async Task<OmsiMapDescriptor> ReadDescriptorAsync(
        string directory,
        CancellationToken cancellationToken)
    {
        var globalConfigPath = Path.Combine(
            directory,
            "global.cfg");

        var document =
            await OmsiConfigParser.ParseFileAsync(
                globalConfigPath,
                cancellationToken);

        var directoryName =
            Path.GetFileName(directory);

        var displayName =
            document
                .FindFirstSection("name")
                ?.DataLines
                .FirstOrDefault()
            ?? directoryName;

        return new OmsiMapDescriptor(
            directoryName,
            displayName,
            directory,
            globalConfigPath,
            UsesWorldCoordinates(document),
            ReadTiles(document),
            ReadGroundTextures(document));
    }

    private static bool IsRecoverableMapError(
        Exception exception) =>
        exception is IOException or
        UnauthorizedAccessException or
        ArgumentException or
        NotSupportedException;

    public static bool UsesWorldCoordinates(
        OmsiConfigDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return document.FindFirstSection(
            "worldcoordinates") is not null;
    }

    public static IReadOnlyList<OmsiGroundTexture>
        ReadGroundTextures(
            OmsiConfigDocument document)
    {
        ArgumentNullException.ThrowIfNull(
            document);

        var textures =
            new List<OmsiGroundTexture>();

        foreach (
            var section in
                document.FindSections(
                    "groundtex"))
        {
            var values =
                section.DataLines
                    .Take(5)
                    .ToArray();

            if (
                values.Length < 5 ||
                string.IsNullOrWhiteSpace(
                    values[0]) ||
                string.IsNullOrWhiteSpace(
                    values[1]) ||
                !int.TryParse(
                    values[2],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var resolutionCode) ||
                !double.TryParse(
                    values[3],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var mainRepeating) ||
                !double.IsFinite(
                    mainRepeating) ||
                mainRepeating <= 0 ||
                !double.TryParse(
                    values[4],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var detailRepeating) ||
                !double.IsFinite(
                    detailRepeating) ||
                detailRepeating <= 0)
            {
                continue;
            }

            textures.Add(
                new OmsiGroundTexture(
                    values[0],
                    values[1],
                    resolutionCode,
                    mainRepeating,
                    detailRepeating));
        }

        return textures;
    }

    public static IReadOnlyList<OmsiTileReference> ReadTiles(
        OmsiConfigDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var tiles = new List<OmsiTileReference>();

        foreach (var section in document.FindSections("map"))
        {
            var values =
                section.DataLines
                    .Take(3)
                    .ToArray();

            if (values.Length < 3 ||
                !int.TryParse(
                    values[0],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var x) ||
                !int.TryParse(
                    values[1],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var y))
            {
                continue;
            }

            tiles.Add(new OmsiTileReference(
                x,
                y,
                values[2]));
        }

        return tiles;
    }
}
