using System.Globalization;
using MapStudio.Core.Omsi.Config;

namespace MapStudio.Core.Omsi.Maps;

public sealed class OmsiMapCatalog
{
    private readonly OmsiTileReader _tileReader;

    public OmsiMapCatalog(OmsiTileReader? tileReader = null)
    {
        _tileReader = tileReader ?? new OmsiTileReader();
    }

    public async Task<IReadOnlyList<OmsiMapDescriptor>> DiscoverAsync(
        string omsiRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(omsiRoot);

        var mapsDirectory = Path.Combine(omsiRoot, "maps");

        if (!Directory.Exists(mapsDirectory))
        {
            return Array.Empty<OmsiMapDescriptor>();
        }

        var results = new List<OmsiMapDescriptor>();

        foreach (var directory in Directory.EnumerateDirectories(mapsDirectory).OrderBy(static path => path))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var globalConfigPath = Path.Combine(directory, "global.cfg");

            if (!File.Exists(globalConfigPath))
            {
                continue;
            }

            var document = await OmsiConfigParser.ParseFileAsync(globalConfigPath, cancellationToken);
            var directoryName = Path.GetFileName(directory);
            var displayName = document.FindFirstSection("name")?.DataLines.FirstOrDefault() ?? directoryName;
            var usesWorldCoordinates = UsesWorldCoordinates(document);
            var tileReferences = ReadTiles(document);
            var tiles = new List<OmsiTileReference>(tileReferences.Count);

            foreach (var tile in tileReferences)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var relativePath = tile.RelativeMapPath
                    .Replace('\\', Path.DirectorySeparatorChar)
                    .Replace('/', Path.DirectorySeparatorChar)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                var tilePath = Path.Combine(directory, relativePath);
                var summary = await _tileReader.ReadSummaryAsync(tilePath, cancellationToken);
                tiles.Add(tile with { Summary = summary });
            }

            results.Add(new OmsiMapDescriptor(
                directoryName,
                displayName,
                directory,
                globalConfigPath,
                usesWorldCoordinates,
                tiles));
        }

        return results;
    }

    public static bool UsesWorldCoordinates(OmsiConfigDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.FindFirstSection("worldcoordinates") is not null;
    }

    public static IReadOnlyList<OmsiTileReference> ReadTiles(OmsiConfigDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var tiles = new List<OmsiTileReference>();

        foreach (var section in document.FindSections("map"))
        {
            var values = section.DataLines.Take(3).ToArray();

            if (values.Length < 3 ||
                !int.TryParse(values[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ||
                !int.TryParse(values[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
            {
                continue;
            }

            tiles.Add(new OmsiTileReference(x, y, values[2]));
        }

        return tiles;
    }
}
