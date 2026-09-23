using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Timetables;

namespace MapStudio.Core.ProtonBus;

public sealed record ProtonBusOmsiMapTileSource(
    OmsiTileReference Tile,
    OmsiTileContent Content);

public sealed record ProtonBusOmsiMapTileExportResult(
    OmsiTileReference Tile,
    ProtonBusOmsiAssetResolutionResult Assets,
    ProtonBusOmsiTileExportResult? Geometry,
    ProtonBusOmsiFunctionalConversionResult? Functional)
{
    public ProtonBusOmsiTrafficLightConversionResult?
        TrafficLights { get; init; }
}

public sealed record ProtonBusOmsiMapExportIssue(
    int? TileX,
    int? TileY,
    string Code,
    string Source,
    string? Detail = null);

public sealed record ProtonBusOmsiTimetableExportSource(
    IReadOnlyList<OmsiTileReference> TileOrder,
    OmsiTimetableCatalog Catalog,
    ProtonBusOmsiTimetableConversionOptions? Options = null);

public sealed record ProtonBusOmsiMapExportOptions(
    ProtonBusOmsiTileExportOptions? TileOptions = null)
{
    public ProtonBusOmsiTimetableExportSource?
        Timetable { get; init; }

    public bool ConvertTrafficLights { get; init; } =
        true;

    public ProtonBusOmsiTrafficLightConversionOptions?
        TrafficLightOptions { get; init; }

    public IReadOnlyList<ProtonBusBusStopDefinition>
        BusStops { get; init; } =
        Array.Empty<ProtonBusBusStopDefinition>();

    public IReadOnlyList<ProtonBusEntrypointDefinition>
        Entrypoints { get; init; } =
        Array.Empty<ProtonBusEntrypointDefinition>();

    public IReadOnlyList<ProtonBusTrafficLightDefinition>
        TrafficLights { get; init; } =
        Array.Empty<ProtonBusTrafficLightDefinition>();

    public IReadOnlyList<ProtonBusStreetLightDefinition>
        AdditionalStreetLights { get; init; } =
        Array.Empty<ProtonBusStreetLightDefinition>();
}

public sealed record ProtonBusOmsiMapPackageExportResult(
    bool IsExported,
    ProtonBusMapPackageResult? Package,
    IReadOnlyList<ProtonBusOmsiMapTileExportResult> Tiles,
    IReadOnlyList<ProtonBusOmsiMapExportIssue> Issues)
{
    public ProtonBusOmsiTimetableConversionResult?
        Timetable { get; init; }
}

public sealed class ProtonBusOmsiMapPackageExporter
{
    private readonly ProtonBusOmsiAssetResolver
        _assetResolver =
            new();

    public async Task<ProtonBusOmsiMapPackageExportResult>
        ExportAsync(
            string omsiRoot,
            string outputRoot,
            ProtonBusMapDefinition definition,
            IReadOnlyList<ProtonBusOmsiMapTileSource> tiles,
            ProtonBusOmsiMapExportOptions? options = null,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            omsiRoot);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            outputRoot);

        ArgumentNullException.ThrowIfNull(
            definition);

        ArgumentNullException.ThrowIfNull(
            tiles);

        options ??=
            new();

        var issues =
            new List<ProtonBusOmsiMapExportIssue>();

        var tileResults =
            new List<ProtonBusOmsiMapTileExportResult>(
                tiles.Count);

        var duplicateTile =
            tiles
                .GroupBy(
                    item =>
                        (
                            item.Tile.X,
                            item.Tile.Y
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
                    duplicateTile.Key.X,
                    duplicateTile.Key.Y,
                    "duplicateTileCoordinates",
                    $"tile {duplicateTile.Key.X},{duplicateTile.Key.Y}",
                    "A Proton Bus map export can contain each OMSI tile coordinate only once."));

            return new(
                false,
                null,
                tileResults,
                issues);
        }

        var models =
            new List<ProtonBusModelExport>(
                tiles.Count);

        var textureByTarget =
            new Dictionary<string, ProtonBusTextureExport>(
                StringComparer.OrdinalIgnoreCase);

        var vehiclePaths =
            new List<ProtonBusVehiclePathDefinition>();

        var pedestrianPaths =
            new List<ProtonBusPedestrianPathDefinition>();

        var trainPaths =
            new List<ProtonBusTrainPathDefinition>();

        var streetLights =
            new List<ProtonBusStreetLightDefinition>();

        var trafficLights =
            new List<ProtonBusTrafficLightDefinition>(
                options.TrafficLights);

        var assetsByTile =
            new Dictionary<
                (int X, int Y),
                ProtonBusOmsiAssetResolutionResult>();

        var contentByTile =
            tiles.ToDictionary(
                source =>
                    (
                        source.Tile.X,
                        source.Tile.Y
                    ),
                source =>
                    source.Content);

        foreach (
            var source
            in tiles)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            ArgumentNullException.ThrowIfNull(
                source);

            ArgumentNullException.ThrowIfNull(
                source.Tile);

            ArgumentNullException.ThrowIfNull(
                source.Content);

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

            AddAssetIssues(
                tile,
                assets,
                issues);

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
                            tile.X,
                            tile.Y,
                            "textureTranscodeUnsupported",
                            texture.DeclaredName,
                            $"Source '{texture.SourcePath}' cannot currently be converted to PNG."));

                    continue;
                }

                var export =
                    new ProtonBusTextureExport(
                        texture.SourcePath,
                        texture.TargetFileName);

                if (
                    textureByTarget.TryGetValue(
                        texture.TargetFileName,
                        out var existing))
                {
                    if (
                        !string.Equals(
                            Path.GetFullPath(
                                existing.SourcePath),
                            Path.GetFullPath(
                                texture.SourcePath),
                            StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add(
                            new(
                                tile.X,
                                tile.Y,
                                "textureTargetCollisionAcrossTiles",
                                texture.TargetFileName,
                                $"'{existing.SourcePath}' and '{texture.SourcePath}' resolve to the same Proton Bus texture name."));
                    }

                    continue;
                }

                textureByTarget[
                    texture.TargetFileName] =
                    export;
            }

            if (
                HasBlockingIssue(
                    issues,
                    tile))
            {
                tileResults.Add(
                    new(
                        tile,
                        assets,
                        null,
                        null));

                continue;
            }

            var geometry =
                ProtonBusOmsiTileExportBuilder
                    .Build(
                        tile,
                        source.Content,
                        assets.SplineDefinitions,
                        assets.SceneryAssets,
                        options.TileOptions);

            foreach (
                var missing
                in geometry
                    .MissingSplineDefinitions)
            {
                issues.Add(
                    new(
                        tile.X,
                        tile.Y,
                        "splineDefinitionMissingAfterResolution",
                        missing));
            }

            foreach (
                var missing
                in geometry
                    .MissingSceneryAssets)
            {
                issues.Add(
                    new(
                        tile.X,
                        tile.Y,
                        "sceneryAssetMissingAfterResolution",
                        missing));
            }

            if (
                HasBlockingIssue(
                    issues,
                    tile))
            {
                tileResults.Add(
                    new(
                        tile,
                        assets,
                        geometry,
                        null));

                continue;
            }

            var functional =
                ProtonBusOmsiFunctionalConverter
                    .Convert(
                        tile,
                        source.Content,
                        assets.SplineDefinitions,
                        assets.SceneryAssets,
                        options
                            .TileOptions?
                            .FunctionalOptions);

            foreach (
                var issue
                in functional.Issues)
            {
                issues.Add(
                    new(
                        tile.X,
                        tile.Y,
                        issue.Code,
                        issue.Source,
                        issue.Detail));
            }

            ProtonBusOmsiTrafficLightConversionResult?
                traffic =
                    null;

            if (
                options.ConvertTrafficLights)
            {
                traffic =
                    ProtonBusOmsiTrafficLightConverter
                        .Convert(
                            tile,
                            source.Content,
                            assets.SceneryAssets,
                            options
                                .TrafficLightOptions);

                foreach (
                    var issue
                    in traffic.Issues)
                {
                    issues.Add(
                        new(
                            tile.X,
                            tile.Y,
                            issue.Code,
                            issue.Source,
                            issue.Detail));
                }

                trafficLights.AddRange(
                    traffic.TrafficLights);
            }

            var combinedScene =
                new ProtonBusExportScene(
                    geometry
                        .Scene
                        .Meshes
                        .Concat(
                            functional
                                .MarkerScene
                                .Meshes)
                        .Concat(
                            traffic?
                                .MarkerScene
                                .Meshes ??
                            Array.Empty<
                                ProtonBusExportMesh>())
                        .ToArray());

            models.Add(
                new(
                    $"tile_{tile.X}_{tile.Y}.3ds",
                    combinedScene));

            vehiclePaths.AddRange(
                functional.VehiclePaths);

            pedestrianPaths.AddRange(
                functional.PedestrianPaths);

            trainPaths.AddRange(
                functional.TrainPaths);

            streetLights.AddRange(
                functional.StreetLights);

            tileResults.Add(
                new(
                    tile,
                    assets,
                    geometry,
                    functional)
                {
                    TrafficLights =
                        traffic
                });
        }

        if (
            issues.Any(
                IsBlockingIssue))
        {
            return new(
                false,
                null,
                tileResults,
                issues);
        }

        streetLights.AddRange(
            options.AdditionalStreetLights);

        ValidateTrafficLightPrefixes(
            trafficLights,
            issues);

        ProtonBusOmsiTimetableConversionResult?
            timetableResult =
                null;

        var busStops =
            new List<ProtonBusBusStopDefinition>(
                options.BusStops);

        var entrypoints =
            new List<ProtonBusEntrypointDefinition>(
                options.Entrypoints);

        if (
            options.Timetable is
                { } timetableSource)
        {
            timetableResult =
                ProtonBusOmsiTimetableConverter
                    .Convert(
                        timetableSource
                            .TileOrder,
                        timetableSource
                            .Catalog,
                        contentByTile,
                        assetsByTile,
                        timetableSource
                            .Options);

            foreach (
                var issue
                in timetableResult
                    .Issues)
            {
                issues.Add(
                    new(
                        null,
                        null,
                        issue.Code,
                        issue.Source,
                        issue.Detail));
            }

            busStops.AddRange(
                timetableResult
                    .BusStops);

            entrypoints.AddRange(
                timetableResult
                    .Entrypoints);

            if (
                timetableResult
                    .MarkerScene
                    .Meshes
                    .Count >
                0)
            {
                models.Add(
                    new(
                        "timetable_markers.3ds",
                        timetableResult
                            .MarkerScene));
            }

            ValidateGeneratedTimetableNames(
                busStops,
                entrypoints,
                issues);
        }

        if (
            issues.Any(
                IsBlockingIssue))
        {
            return new(
                false,
                null,
                tileResults,
                issues)
            {
                Timetable =
                    timetableResult
            };
        }

        var request =
            new ProtonBusMapPackageRequest(
                definition,
                models,
                textureByTarget
                    .Values
                    .OrderBy(
                        item =>
                            item.FileName,
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray())
            {
                BusStops =
                    busStops,
                Entrypoints =
                    entrypoints,
                TrafficLights =
                    trafficLights,
                StreetLights =
                    streetLights,
                VehiclePaths =
                    vehiclePaths,
                PedestrianPaths =
                    pedestrianPaths,
                TrainPaths =
                    trainPaths
            };

        cancellationToken
            .ThrowIfCancellationRequested();

        var package =
            ProtonBusMapPackageWriter
                .Write(
                    outputRoot,
                    request);

        return new(
            true,
            package,
            tileResults,
            issues)
        {
            Timetable =
                timetableResult
        };
    }

    private static void ValidateTrafficLightPrefixes(
        IReadOnlyList<ProtonBusTrafficLightDefinition>
            trafficLights,
        ICollection<ProtonBusOmsiMapExportIssue>
            issues)
    {
        var duplicate =
            trafficLights
                .GroupBy(
                    light =>
                        light.Prefix,
                    StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(
                    group =>
                        group.Count() >
                        1);

        if (
            duplicate is
                not null)
        {
            issues.Add(
                new(
                    null,
                    null,
                    "duplicateTrafficLightPrefix",
                    duplicate.Key,
                    "Generated and explicit Proton Bus traffic lights must use unique prefixes."));
        }
    }

    private static void ValidateGeneratedTimetableNames(
        IReadOnlyList<ProtonBusBusStopDefinition>
            busStops,
        IReadOnlyList<ProtonBusEntrypointDefinition>
            entrypoints,
        ICollection<ProtonBusOmsiMapExportIssue>
            issues)
    {
        var duplicateStop =
            busStops
                .GroupBy(
                    stop =>
                        stop.Prefix,
                    StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(
                    group =>
                        group.Count() >
                        1);

        if (
            duplicateStop is
                not null)
        {
            issues.Add(
                new(
                    null,
                    null,
                    "duplicateBusStopPrefix",
                    duplicateStop.Key,
                    "Generated and explicit Proton Bus stops must use unique prefixes."));
        }

        var duplicateEntrypoint =
            entrypoints
                .GroupBy(
                    entrypoint =>
                        entrypoint.Name,
                    StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(
                    group =>
                        group.Count() >
                        1);

        if (
            duplicateEntrypoint is
                not null)
        {
            issues.Add(
                new(
                    null,
                    null,
                    "duplicateEntrypointName",
                    duplicateEntrypoint.Key,
                    "Generated and explicit Proton Bus entrypoints must use unique names."));
        }
    }

    private static void AddAssetIssues(
        OmsiTileReference tile,
        ProtonBusOmsiAssetResolutionResult assets,
        ICollection<ProtonBusOmsiMapExportIssue> output)
    {
        foreach (
            var issue
            in assets.Issues)
        {
            output.Add(
                new(
                    tile.X,
                    tile.Y,
                    issue.Code,
                    issue.DeclaredPath,
                    issue.Detail ??
                    issue.ResolvedPath));
        }
    }

    private static bool HasBlockingIssue(
        IEnumerable<ProtonBusOmsiMapExportIssue> issues,
        OmsiTileReference tile) =>
        issues.Any(
            issue =>
                issue.TileX ==
                    tile.X &&
                issue.TileY ==
                    tile.Y &&
                IsBlockingIssue(
                    issue));

    private static bool IsBlockingIssue(
        ProtonBusOmsiMapExportIssue issue) =>
        issue.Code is
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
