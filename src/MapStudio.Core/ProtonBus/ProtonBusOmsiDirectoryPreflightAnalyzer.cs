using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Timetables;

namespace MapStudio.Core.ProtonBus;

public enum ProtonBusOmsiPreflightSeverity
{
    Info,
    Warning,
    Error
}

public sealed record ProtonBusOmsiPreflightIssue(
    ProtonBusOmsiPreflightSeverity Severity,
    string Code,
    string Source,
    string? Detail = null,
    int? TileX = null,
    int? TileY = null);

public sealed record ProtonBusOmsiPreflightSummary(
    int TileCount,
    int ObjectCount,
    int SplineCount,
    int TerrainMeshCount,
    int SplineMeshCount,
    int SceneryMeshCount,
    int TextureCount,
    int VehiclePathCount,
    int PedestrianPathCount,
    int TrainPathCount,
    int BusStopCount,
    int EntrypointCount,
    int TrafficLightCount,
    int StreetLightCount,
    int GpsRouteCount,
    int GpsMeshCount,
    int MarkerMeshCount);

public sealed record ProtonBusOmsiDirectoryPreflightResult(
    bool CanExport,
    OmsiMapDescriptor? Descriptor,
    OmsiTimetableCatalog? Timetable,
    ProtonBusOmsiPreflightSummary Summary,
    IReadOnlyList<ProtonBusOmsiPreflightIssue> Issues)
{
    public int ErrorCount =>
        Issues.Count(
            issue =>
                issue.Severity ==
                ProtonBusOmsiPreflightSeverity.Error);

    public int WarningCount =>
        Issues.Count(
            issue =>
                issue.Severity ==
                ProtonBusOmsiPreflightSeverity.Warning);
}

public sealed class ProtonBusOmsiDirectoryPreflightAnalyzer
{
    private readonly OmsiTileReader
        _tileReader =
            new();

    private readonly OmsiTimetableCatalogReader
        _timetableReader =
            new();

    private readonly ProtonBusOmsiAssetResolver
        _assetResolver =
            new();

    public async Task<
        ProtonBusOmsiDirectoryPreflightResult>
        AnalyzeAsync(
            string omsiRoot,
            string mapDirectory,
            ProtonBusMapDefinition definition,
            ProtonBusOmsiDirectoryExportOptions? options = null,
            IProgress<ProtonBusOmsiDirectoryExportProgress>? progress = null,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            omsiRoot);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            mapDirectory);

        ArgumentNullException.ThrowIfNull(
            definition);

        options ??=
            new();

        var issues =
            new List<
                ProtonBusOmsiPreflightIssue>();

        foreach (
            var validation
            in ProtonBusMapDefinitionValidator
                .Validate(
                    definition))
        {
            issues.Add(
                new(
                    validation.Severity switch
                    {
                        ProtonBusValidationSeverity.Error =>
                            ProtonBusOmsiPreflightSeverity.Error,
                        ProtonBusValidationSeverity.Warning =>
                            ProtonBusOmsiPreflightSeverity.Warning,
                        _ =>
                            ProtonBusOmsiPreflightSeverity.Info
                    },
                    validation.Code,
                    "map definition",
                    validation.Message));
        }

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
                Error(
                    "mapOpenFailed",
                    mapDirectory,
                    exception.Message));

            return BuildResult(
                null,
                null,
                issues);
        }

        if (
            descriptor.Tiles.Count ==
            0)
        {
            issues.Add(
                Error(
                    "mapTilesMissing",
                    descriptor.DirectoryPath,
                    "global.cfg does not contain any valid [map] entries."));

            return BuildResult(
                descriptor,
                null,
                issues);
        }

        var duplicateTile =
            descriptor.Tiles
                .GroupBy(
                    tile =>
                        (
                            tile.X,
                            tile.Y
                        ))
                .FirstOrDefault(
                    group =>
                        group.Count() >
                        1);

        if (
            duplicateTile is
                not null)
        {
            issues.Add(
                new(
                    ProtonBusOmsiPreflightSeverity.Error,
                    "duplicateTileCoordinates",
                    $"tile {duplicateTile.Key.X},{duplicateTile.Key.Y}",
                    "Each OMSI tile coordinate may appear only once.",
                    duplicateTile.Key.X,
                    duplicateTile.Key.Y));
        }

        progress?.Report(
            new(
                "preflight-tiles",
                0,
                descriptor.Tiles.Count));

        var tileSources =
            new List<
                ProtonBusOmsiMapTileSource>(
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
                        ProtonBusOmsiPreflightSeverity.Error,
                        "tilePathInvalid",
                        tile.RelativeMapPath,
                        "The tile path escapes the selected map directory or is invalid.",
                        tile.X,
                        tile.Y));

                continue;
            }

            if (
                !File.Exists(
                    fullPath))
            {
                issues.Add(
                    new(
                        ProtonBusOmsiPreflightSeverity.Error,
                        "tileFileMissing",
                        tile.RelativeMapPath,
                        fullPath,
                        tile.X,
                        tile.Y));

                continue;
            }

            try
            {
                var content =
                    await _tileReader
                        .ReadContentAsync(
                            fullPath,
                            cancellationToken)
                        .ConfigureAwait(false);

                if (
                    !content.Summary.Exists)
                {
                    issues.Add(
                        new(
                            ProtonBusOmsiPreflightSeverity.Error,
                            "tileReadFailed",
                            tile.RelativeMapPath,
                            "Tile content was unavailable after loading.",
                            tile.X,
                            tile.Y));

                    continue;
                }

                tileSources.Add(
                    new(
                        tile,
                        content));
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
                        ProtonBusOmsiPreflightSeverity.Error,
                        "tileReadFailed",
                        tile.RelativeMapPath,
                        exception.Message,
                        tile.X,
                        tile.Y));
            }

            progress?.Report(
                new(
                    "preflight-tiles",
                    index + 1,
                    descriptor.Tiles.Count,
                    tile.RelativeMapPath));
        }

        if (
            issues.Any(
                issue =>
                    issue.Severity ==
                        ProtonBusOmsiPreflightSeverity.Error &&
                    issue.Code is
                        "tilePathInvalid" or
                        "tileFileMissing" or
                        "tileReadFailed" or
                        "duplicateTileCoordinates"))
        {
            return BuildResult(
                descriptor,
                null,
                issues,
                tileSources);
        }

        var mapOptions =
            options.MapOptions ??
            new ProtonBusOmsiMapExportOptions();

        var assetsByTile =
            new Dictionary<
                (int X, int Y),
                ProtonBusOmsiAssetResolutionResult>();

        var contentByTile =
            tileSources.ToDictionary(
                source =>
                    (
                        source.Tile.X,
                        source.Tile.Y
                    ),
                source =>
                    source.Content);

        var textureTargets =
            new Dictionary<
                string,
                string>(
                    StringComparer.OrdinalIgnoreCase);

        var vehiclePaths =
            new List<
                ProtonBusVehiclePathDefinition>();

        var pedestrianPaths =
            new List<
                ProtonBusPedestrianPathDefinition>();

        var trainPaths =
            new List<
                ProtonBusTrainPathDefinition>();

        var trafficLights =
            new List<
                ProtonBusTrafficLightDefinition>(
                    mapOptions.TrafficLights);

        var streetLights =
            new List<
                ProtonBusStreetLightDefinition>(
                    mapOptions.AdditionalStreetLights);

        var terrainMeshCount =
            0;

        var splineMeshCount =
            0;

        var sceneryMeshCount =
            0;

        var markerMeshCount =
            0;

        progress?.Report(
            new(
                "preflight-assets",
                0,
                tileSources.Count));

        for (
            var index = 0;
            index <
                tileSources.Count;
            index++)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            var source =
                tileSources[index];

            var tile =
                source.Tile;

            var assets =
                await _assetResolver
                    .ResolveAsync(
                        omsiRoot,
                        source.Content,
                        cancellationToken)
                    .ConfigureAwait(false);

            assetsByTile[
                (
                    tile.X,
                    tile.Y
                )
            ] =
                assets;

            foreach (
                var issue
                in assets.Issues)
            {
                issues.Add(
                    new(
                        IsBlockingCode(
                            issue.Code)
                            ? ProtonBusOmsiPreflightSeverity.Error
                            : ProtonBusOmsiPreflightSeverity.Warning,
                        issue.Code,
                        issue.DeclaredPath,
                        issue.Detail ??
                        issue.ResolvedPath,
                        tile.X,
                        tile.Y));
            }

            foreach (
                var texture
                in assets.Textures)
            {
                if (
                    !ProtonBusTextureTranscoder
                        .CanTranscode(
                            texture.SourcePath))
                {
                    issues.Add(
                        new(
                            ProtonBusOmsiPreflightSeverity.Error,
                            "textureTranscodeUnsupported",
                            texture.DeclaredName,
                            texture.SourcePath,
                            tile.X,
                            tile.Y));

                    continue;
                }

                if (
                    textureTargets.TryGetValue(
                        texture.TargetFileName,
                        out var existing) &&
                    !string.Equals(
                        Path.GetFullPath(
                            existing),
                        Path.GetFullPath(
                            texture.SourcePath),
                        StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(
                        new(
                            ProtonBusOmsiPreflightSeverity.Error,
                            "textureTargetCollisionAcrossTiles",
                            texture.TargetFileName,
                            $"'{existing}' and '{texture.SourcePath}' resolve to the same target name.",
                            tile.X,
                            tile.Y));
                }
                else
                {
                    textureTargets[
                        texture.TargetFileName] =
                        texture.SourcePath;
                }
            }

            if (
                HasTileError(
                    issues,
                    tile))
            {
                continue;
            }

            try
            {
                var geometry =
                    ProtonBusOmsiTileExportBuilder
                        .Build(
                            tile,
                            source.Content,
                            assets.SplineDefinitions,
                            assets.SceneryAssets,
                            mapOptions.TileOptions);

                terrainMeshCount +=
                    geometry.TerrainMeshCount;

                splineMeshCount +=
                    geometry.SplineMeshCount;

                sceneryMeshCount +=
                    geometry.SceneryMeshCount;

                foreach (
                    var missing
                    in geometry
                        .MissingSplineDefinitions)
                {
                    issues.Add(
                        new(
                            ProtonBusOmsiPreflightSeverity.Error,
                            "splineDefinitionMissingAfterResolution",
                            missing,
                            null,
                            tile.X,
                            tile.Y));
                }

                foreach (
                    var missing
                    in geometry
                        .MissingSceneryAssets)
                {
                    issues.Add(
                        new(
                            ProtonBusOmsiPreflightSeverity.Error,
                            "sceneryAssetMissingAfterResolution",
                            missing,
                            null,
                            tile.X,
                            tile.Y));
                }

                if (
                    HasTileError(
                        issues,
                        tile))
                {
                    continue;
                }

                var functional =
                    ProtonBusOmsiFunctionalConverter
                        .Convert(
                            tile,
                            source.Content,
                            assets.SplineDefinitions,
                            assets.SceneryAssets,
                            mapOptions
                                .TileOptions?
                                .FunctionalOptions);

                vehiclePaths.AddRange(
                    functional.VehiclePaths);

                pedestrianPaths.AddRange(
                    functional.PedestrianPaths);

                trainPaths.AddRange(
                    functional.TrainPaths);

                streetLights.AddRange(
                    functional.StreetLights);

                markerMeshCount +=
                    functional
                        .MarkerScene
                        .Meshes
                        .Count;

                foreach (
                    var issue
                    in functional.Issues)
                {
                    issues.Add(
                        new(
                            IsBlockingCode(
                                issue.Code)
                                ? ProtonBusOmsiPreflightSeverity.Error
                                : ProtonBusOmsiPreflightSeverity.Warning,
                            issue.Code,
                            issue.Source,
                            issue.Detail,
                            tile.X,
                            tile.Y));
                }

                if (
                    mapOptions.ConvertTrafficLights)
                {
                    var traffic =
                        ProtonBusOmsiTrafficLightConverter
                            .Convert(
                                tile,
                                source.Content,
                                assets.SceneryAssets,
                                mapOptions
                                    .TrafficLightOptions);

                    trafficLights.AddRange(
                        traffic.TrafficLights);

                    markerMeshCount +=
                        traffic
                            .MarkerScene
                            .Meshes
                            .Count;

                    foreach (
                        var issue
                        in traffic.Issues)
                    {
                        issues.Add(
                            new(
                                IsBlockingCode(
                                    issue.Code)
                                    ? ProtonBusOmsiPreflightSeverity.Error
                                    : ProtonBusOmsiPreflightSeverity.Warning,
                                issue.Code,
                                issue.Source,
                                issue.Detail,
                                tile.X,
                                tile.Y));
                    }
                }
            }
            catch (Exception exception) when (
                exception is
                    ArgumentException or
                    InvalidDataException or
                    NotSupportedException)
            {
                issues.Add(
                    new(
                        ProtonBusOmsiPreflightSeverity.Error,
                        "tileConversionFailed",
                        $"tile {tile.X},{tile.Y}",
                        exception.Message,
                        tile.X,
                        tile.Y));
            }

            progress?.Report(
                new(
                    "preflight-assets",
                    index + 1,
                    tileSources.Count,
                    tile.RelativeMapPath));
        }

        OmsiTimetableCatalog?
            timetable =
                null;

        var busStops =
            new List<
                ProtonBusBusStopDefinition>(
                    mapOptions.BusStops);

        var entrypoints =
            new List<
                ProtonBusEntrypointDefinition>(
                    mapOptions.Entrypoints);

        var gpsRouteCount =
            0;

        var gpsMeshCount =
            0;

        if (
            options.IncludeTimetable)
        {
            progress?.Report(
                new(
                    "preflight-timetable",
                    tileSources.Count,
                    tileSources.Count));

            try
            {
                timetable =
                    await _timetableReader
                        .ReadAsync(
                            descriptor.DirectoryPath,
                            cancellationToken)
                        .ConfigureAwait(false);

                var timetableResult =
                    ProtonBusOmsiTimetableConverter
                        .Convert(
                            descriptor.Tiles,
                            timetable,
                            contentByTile,
                            assetsByTile,
                            options.TimetableOptions);

                busStops.AddRange(
                    timetableResult.BusStops);

                entrypoints.AddRange(
                    timetableResult.Entrypoints);

                markerMeshCount +=
                    timetableResult
                        .MarkerScene
                        .Meshes
                        .Count;

                foreach (
                    var issue
                    in timetableResult.Issues)
                {
                    issues.Add(
                        new(
                            IsBlockingCode(
                                issue.Code)
                                ? ProtonBusOmsiPreflightSeverity.Error
                                : ProtonBusOmsiPreflightSeverity.Warning,
                            issue.Code,
                            issue.Source,
                            issue.Detail));
                }

                if (
                    mapOptions.GenerateGpsRoutes &&
                    timetableResult
                        .TripEntrypoints
                        .Count >
                    0)
                {
                    var gps =
                        ProtonBusOmsiGpsRouteBuilder
                            .Build(
                                descriptor.Tiles,
                                timetable,
                                contentByTile,
                                assetsByTile,
                                timetableResult
                                    .TripEntrypoints,
                                mapOptions.GpsOptions);

                    gpsRouteCount =
                        gps.Routes.Count;

                    gpsMeshCount =
                        gps.Scene
                            .Meshes
                            .Count;

                    foreach (
                        var issue
                        in gps.Issues)
                    {
                        issues.Add(
                            new(
                                ProtonBusOmsiPreflightSeverity.Warning,
                                issue.Code,
                                $"trip '{issue.TripName}'",
                                issue.Detail));
                    }
                }
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
                    Error(
                        "timetablePreflightFailed",
                        descriptor.DirectoryPath,
                        exception.Message));
            }
        }

        ValidateDefinitions(
            busStops,
            entrypoints,
            vehiclePaths,
            pedestrianPaths,
            trainPaths,
            trafficLights,
            streetLights,
            issues);

        var summary =
            new ProtonBusOmsiPreflightSummary(
                TileCount:
                    tileSources.Count,
                ObjectCount:
                    tileSources.Sum(
                        source =>
                            source.Content
                                .Objects
                                .Count),
                SplineCount:
                    tileSources.Sum(
                        source =>
                            source.Content
                                .Splines
                                .Count),
                TerrainMeshCount:
                    terrainMeshCount,
                SplineMeshCount:
                    splineMeshCount,
                SceneryMeshCount:
                    sceneryMeshCount,
                TextureCount:
                    textureTargets.Count,
                VehiclePathCount:
                    vehiclePaths.Count,
                PedestrianPathCount:
                    pedestrianPaths.Count,
                TrainPathCount:
                    trainPaths.Count,
                BusStopCount:
                    busStops.Count,
                EntrypointCount:
                    entrypoints.Count,
                TrafficLightCount:
                    trafficLights.Count,
                StreetLightCount:
                    streetLights.Count,
                GpsRouteCount:
                    gpsRouteCount,
                GpsMeshCount:
                    gpsMeshCount,
                MarkerMeshCount:
                    markerMeshCount);

        progress?.Report(
            new(
                "preflight-complete",
                tileSources.Count,
                tileSources.Count));

        return new(
            !issues.Any(
                issue =>
                    issue.Severity ==
                    ProtonBusOmsiPreflightSeverity.Error),
            descriptor,
            timetable,
            summary,
            issues
                .OrderByDescending(
                    issue =>
                        issue.Severity)
                .ThenBy(
                    issue =>
                        issue.Code,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    private static ProtonBusOmsiDirectoryPreflightResult
        BuildResult(
            OmsiMapDescriptor? descriptor,
            OmsiTimetableCatalog? timetable,
            IReadOnlyList<
                ProtonBusOmsiPreflightIssue> issues,
            IReadOnlyList<
                ProtonBusOmsiMapTileSource>? tiles =
                null)
    {
        var sources =
            tiles ??
            Array.Empty<
                ProtonBusOmsiMapTileSource>();

        return new(
            false,
            descriptor,
            timetable,
            new(
                sources.Count,
                sources.Sum(
                    source =>
                        source.Content
                            .Objects
                            .Count),
                sources.Sum(
                    source =>
                        source.Content
                            .Splines
                            .Count),
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0),
            issues.ToArray());
    }

    private static bool HasTileError(
        IEnumerable<
            ProtonBusOmsiPreflightIssue> issues,
        OmsiTileReference tile) =>
        issues.Any(
            issue =>
                issue.Severity ==
                    ProtonBusOmsiPreflightSeverity.Error &&
                issue.TileX ==
                    tile.X &&
                issue.TileY ==
                    tile.Y);

    private static void ValidateDefinitions(
        IReadOnlyList<
            ProtonBusBusStopDefinition> busStops,
        IReadOnlyList<
            ProtonBusEntrypointDefinition> entrypoints,
        IReadOnlyList<
            ProtonBusVehiclePathDefinition> vehicles,
        IReadOnlyList<
            ProtonBusPedestrianPathDefinition> pedestrians,
        IReadOnlyList<
            ProtonBusTrainPathDefinition> trains,
        IReadOnlyList<
            ProtonBusTrafficLightDefinition> trafficLights,
        IReadOnlyList<
            ProtonBusStreetLightDefinition> streetLights,
        ICollection<
            ProtonBusOmsiPreflightIssue> issues)
    {
        AddDuplicate(
            busStops
                .Select(
                    stop =>
                        stop.Prefix),
            "duplicateBusStopPrefix",
            "bus stop prefix",
            issues);

        AddDuplicate(
            entrypoints
                .Select(
                    entrypoint =>
                        entrypoint.Name),
            "duplicateEntrypointName",
            "entrypoint name",
            issues);

        AddDuplicate(
            trafficLights
                .Select(
                    item =>
                        item.Prefix),
            "duplicateTrafficLightPrefix",
            "traffic-light prefix",
            issues);

        AddDuplicate(
            streetLights
                .Select(
                    item =>
                        item.Prefix),
            "duplicateStreetLightPrefix",
            "street-light prefix",
            issues);

        AddDuplicate(
            pedestrians
                .Select(
                    item =>
                        item.Prefix)
                .Concat(
                    vehicles.Select(
                        item =>
                            item.Prefix))
                .Concat(
                    trains.Select(
                        item =>
                            item.Prefix)),
            "duplicateMovingPathPrefix",
            "moving-path prefix",
            issues);

        foreach (
            var stop
            in busStops)
        {
            TryValidate(
                () =>
                    ProtonBusBusStopDefinitionWriter
                        .Validate(
                            stop),
                "invalidBusStopDefinition",
                stop.Prefix,
                issues);
        }

        TryValidate(
            () =>
                ProtonBusEntrypointDefinitionWriter
                    .ValidateAll(
                        entrypoints),
            "invalidEntrypointDefinition",
            "entrypoints",
            issues);

        foreach (
            var path
            in vehicles)
        {
            TryValidate(
                () =>
                    ProtonBusVehiclePathDefinitionWriter
                        .Validate(
                            path),
                "invalidVehiclePathDefinition",
                path.Prefix,
                issues);
        }

        foreach (
            var path
            in pedestrians)
        {
            TryValidate(
                () =>
                    ProtonBusPedestrianPathDefinitionWriter
                        .Validate(
                            path),
                "invalidPedestrianPathDefinition",
                path.Prefix,
                issues);
        }

        foreach (
            var path
            in trains)
        {
            TryValidate(
                () =>
                    ProtonBusTrainPathDefinitionWriter
                        .Validate(
                            path),
                "invalidTrainPathDefinition",
                path.Prefix,
                issues);
        }

        foreach (
            var traffic
            in trafficLights)
        {
            TryValidate(
                () =>
                    ProtonBusTrafficLightDefinitionWriter
                        .Validate(
                            traffic),
                "invalidTrafficLightDefinition",
                traffic.Prefix,
                issues);
        }

        foreach (
            var street
            in streetLights)
        {
            TryValidate(
                () =>
                    ProtonBusStreetLightDefinitionWriter
                        .Validate(
                            street),
                "invalidStreetLightDefinition",
                street.Prefix,
                issues);
        }
    }

    private static void AddDuplicate(
        IEnumerable<string> names,
        string code,
        string sourceLabel,
        ICollection<
            ProtonBusOmsiPreflightIssue> issues)
    {
        var duplicate =
            names
                .GroupBy(
                    value =>
                        value,
                    StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(
                    group =>
                        group.Count() >
                        1);

        if (
            duplicate is
                null)
        {
            return;
        }

        issues.Add(
            Error(
                code,
                duplicate.Key,
                $"Duplicate Proton Bus {sourceLabel}."));
    }

    private static void TryValidate(
        Action action,
        string code,
        string source,
        ICollection<
            ProtonBusOmsiPreflightIssue> issues)
    {
        try
        {
            action();
        }
        catch (Exception exception) when (
            exception is
                ArgumentException or
                InvalidDataException or
                NotSupportedException)
        {
            issues.Add(
                Error(
                    code,
                    source,
                    exception.Message));
        }
    }

    private static ProtonBusOmsiPreflightIssue Error(
        string code,
        string source,
        string? detail = null) =>
        new(
            ProtonBusOmsiPreflightSeverity.Error,
            code,
            source,
            detail);

    private static bool IsBlockingCode(
        string code) =>
        code is
            "duplicateTileCoordinates" or
            "splinePathInvalid" or
            "splineMissing" or
            "scoPathInvalid" or
            "scoMissing" or
            "meshPathInvalid" or
            "meshMissing" or
            "meshGeometryInvalid" or
            "splineTextureMissing" or
            "sceneryTextureMissing" or
            "textureTargetCollision" or
            "textureTargetCollisionAcrossTiles" or
            "textureTranscodeUnsupported" or
            "splineDefinitionMissingAfterResolution" or
            "sceneryAssetMissingAfterResolution" or
            "duplicateBusStopPrefix" or
            "duplicateEntrypointName" or
            "duplicateBusStopId" or
            "busStopTileIndexOutOfRange" or
            "busStopTileMissing" or
            "busStopObjectMissing" or
            "tripFirstStopMissing" or
            "duplicateTrafficLightPrefix" or
            "trafficLightControllerAmbiguous" or
            "trafficLightProgramsMissing" or
            "trafficLightProgramDurationInvalid" or
            "trafficLightProgramExceedsCycle" or
            "trafficLightTimingPrecisionUnsupported" or
            "trafficLightTimelineEmpty" or
            "trafficLightTickIntervalInvalid" or
            "trafficLightRepeatOverflow" or
            "trafficLightMultipleTriggerPaths";
}
