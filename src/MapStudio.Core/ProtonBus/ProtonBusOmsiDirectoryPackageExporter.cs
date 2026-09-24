using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Timetables;

namespace MapStudio.Core.ProtonBus;

public sealed record ProtonBusOmsiDirectoryExportOptions(
    ProtonBusOmsiMapExportOptions? MapOptions = null,
    bool IncludeTimetable = true,
    ProtonBusOmsiTimetableConversionOptions?
        TimetableOptions = null)
{
    public ProtonBusTargetProfile
        TargetProfile { get; init; } =
        ProtonBusTargetProfiles
            .Phase3;

    public bool CreateZipArchive
        { get; init; }

    public string? ArchiveFileName
        { get; init; }
}

public sealed record ProtonBusOmsiDirectoryExportProgress(
    string Stage,
    int CompletedTiles,
    int TotalTiles,
    string? CurrentTile = null);

public sealed record ProtonBusOmsiDirectoryPackageExportResult(
    bool IsExported,
    OmsiMapDescriptor? Descriptor,
    OmsiTimetableCatalog? Timetable,
    ProtonBusOmsiMapPackageExportResult? MapExport,
    IReadOnlyList<ProtonBusOmsiMapExportIssue> Issues)
{
    public string? ArchivePath
        { get; init; }
}

public sealed class ProtonBusOmsiDirectoryPackageExporter
{
    private readonly ProtonBusOmsiMapPackageExporter
        _mapExporter =
            new();

    private readonly OmsiTileReader
        _tileReader =
            new();

    private readonly OmsiTimetableCatalogReader
        _timetableReader =
            new();

    public async Task<
        ProtonBusOmsiDirectoryPackageExportResult>
        ExportAsync(
            string omsiRoot,
            string mapDirectory,
            string outputRoot,
            ProtonBusMapDefinition definition,
            ProtonBusOmsiDirectoryExportOptions? options = null,
            IProgress<ProtonBusOmsiDirectoryExportProgress>? progress = null,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            omsiRoot);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            mapDirectory);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            outputRoot);

        ArgumentNullException.ThrowIfNull(
            definition);

        options ??=
            new();

        definition =
            ProtonBusTargetProfiles
                .Apply(
                    definition,
                    options
                        .TargetProfile);

        var issues =
            new List<ProtonBusOmsiMapExportIssue>();

        OmsiMapDescriptor descriptor;

        try
        {
            descriptor =
                await OmsiMapCatalog
                    .OpenMapAsync(
                        mapDirectory,
                        cancellationToken)
                    .ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is
                FileNotFoundException or
                InvalidDataException or
                IOException or
                UnauthorizedAccessException or
                ArgumentException or
                NotSupportedException)
        {
            issues.Add(
                new(
                    null,
                    null,
                    "mapOpenFailed",
                    mapDirectory,
                    exception.Message));

            return new(
                false,
                null,
                null,
                null,
                issues);
        }

        if (
            descriptor.Tiles.Count ==
            0)
        {
            issues.Add(
                new(
                    null,
                    null,
                    "mapTilesMissing",
                    descriptor.DirectoryPath,
                    "global.cfg does not contain any valid [map] entries."));

            return new(
                false,
                descriptor,
                null,
                null,
                issues);
        }

        progress?.Report(
            new(
                "tiles",
                0,
                descriptor.Tiles.Count));

        var tileSources =
            new List<ProtonBusOmsiMapTileSource>(
                descriptor.Tiles.Count);

        for (
            var index = 0;
            index <
                descriptor.Tiles.Count;
            index++)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            var tile =
                descriptor.Tiles[index];

            if (
                !OmsiMapPathResolver
                    .TryResolveTilePath(
                        descriptor.DirectoryPath,
                        tile.RelativeMapPath,
                        out var fullPath))
            {
                issues.Add(
                    new(
                        tile.X,
                        tile.Y,
                        "tilePathInvalid",
                        tile.RelativeMapPath,
                        "The tile path escapes the selected OMSI map directory or is otherwise invalid."));

                continue;
            }

            if (
                !File.Exists(
                    fullPath))
            {
                issues.Add(
                    new(
                        tile.X,
                        tile.Y,
                        "tileFileMissing",
                        tile.RelativeMapPath,
                        fullPath));

                continue;
            }

            OmsiTileContent content;

            try
            {
                content =
                    await _tileReader
                        .ReadContentAsync(
                            fullPath,
                            cancellationToken)
                        .ConfigureAwait(false);
            }
            catch (Exception exception) when (
                exception is
                    InvalidDataException or
                    IOException or
                    UnauthorizedAccessException or
                    ArgumentException or
                    NotSupportedException)
            {
                issues.Add(
                    new(
                        tile.X,
                        tile.Y,
                        "tileReadFailed",
                        tile.RelativeMapPath,
                        exception.Message));

                continue;
            }

            if (
                !content.Summary.Exists)
            {
                issues.Add(
                    new(
                        tile.X,
                        tile.Y,
                        "tileReadFailed",
                        tile.RelativeMapPath,
                        "Tile content was not available after loading."));

                continue;
            }

            tileSources.Add(
                new(
                    tile,
                    content));

            progress?.Report(
                new(
                    "tiles",
                    index + 1,
                    descriptor.Tiles.Count,
                    tile.RelativeMapPath));
        }

        if (
            issues.Any(
                issue =>
                    issue.Code is
                        "tilePathInvalid" or
                        "tileFileMissing" or
                        "tileReadFailed"))
        {
            return new(
                false,
                descriptor,
                null,
                null,
                issues);
        }

        OmsiTimetableCatalog?
            timetable =
                null;

        var mapOptions =
            options.MapOptions ??
            new ProtonBusOmsiMapExportOptions();

        if (
            options.IncludeTimetable)
        {
            progress?.Report(
                new(
                    "timetable",
                    descriptor.Tiles.Count,
                    descriptor.Tiles.Count));

            try
            {
                timetable =
                    await _timetableReader
                        .ReadAsync(
                            descriptor.DirectoryPath,
                            cancellationToken)
                        .ConfigureAwait(false);
            }
            catch (Exception exception) when (
                exception is
                    InvalidDataException or
                    IOException or
                    UnauthorizedAccessException or
                    ArgumentException or
                    NotSupportedException)
            {
                issues.Add(
                    new(
                        null,
                        null,
                        "timetableReadFailed",
                        descriptor.DirectoryPath,
                        exception.Message));

                return new(
                    false,
                    descriptor,
                    null,
                    null,
                    issues);
            }

            mapOptions =
                mapOptions with
                {
                    Timetable =
                        new(
                            descriptor.Tiles,
                            timetable,
                            options.TimetableOptions)
                };
        }

        progress?.Report(
            new(
                "export",
                descriptor.Tiles.Count,
                descriptor.Tiles.Count));

        var mapResult =
            await _mapExporter
                .ExportAsync(
                    omsiRoot,
                    outputRoot,
                    definition,
                    tileSources,
                    mapOptions,
                    cancellationToken)
                .ConfigureAwait(false);

        issues.AddRange(
            mapResult.Issues);

        progress?.Report(
            new(
                "complete",
                descriptor.Tiles.Count,
                descriptor.Tiles.Count));

        string? archivePath =
            null;

        if (
            mapResult.IsExported &&
            options.CreateZipArchive &&
            mapResult.Package is
                { } package)
        {
            try
            {
                var archiveFileName =
                    string.IsNullOrWhiteSpace(
                        options.ArchiveFileName)
                        ? definition.MapName +
                          "-ProtonBus.zip"
                        : options
                            .ArchiveFileName!
                            .Trim();

                if (
                    !archiveFileName.EndsWith(
                        ".zip",
                        StringComparison.OrdinalIgnoreCase))
                {
                    archiveFileName +=
                        ".zip";
                }

                if (
                    !string.Equals(
                        Path.GetFileName(
                            archiveFileName),
                        archiveFileName,
                        StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        "Archive file name must not contain a directory path.");
                }

                archivePath =
                    ProtonBusPackageArchiveWriter
                        .Write(
                            package,
                            Path.Combine(
                                outputRoot,
                                archiveFileName));
            }
            catch (Exception exception) when (
                exception is
                    ArgumentException or
                    InvalidDataException or
                    IOException or
                    UnauthorizedAccessException or
                    NotSupportedException)
            {
                issues.Add(
                    new(
                        null,
                        null,
                        "archiveWriteFailed",
                        outputRoot,
                        exception.Message));

                return new(
                    false,
                    descriptor,
                    timetable,
                    mapResult,
                    issues.ToArray());
            }
        }

        return new(
            mapResult.IsExported,
            descriptor,
            timetable,
            mapResult,
            issues.ToArray())
        {
            ArchivePath =
                archivePath
        };
    }
}
