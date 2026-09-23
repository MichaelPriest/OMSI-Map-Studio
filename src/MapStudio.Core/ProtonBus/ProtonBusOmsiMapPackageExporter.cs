using MapStudio.Core.Omsi.Maps;

namespace MapStudio.Core.ProtonBus;

public sealed record ProtonBusOmsiMapTileSource(
    OmsiTileReference Tile,
    OmsiTileContent Content);

public sealed record ProtonBusOmsiMapTileExportResult(
    OmsiTileReference Tile,
    ProtonBusOmsiAssetResolutionResult Assets,
    ProtonBusOmsiTileExportResult? Geometry,
    ProtonBusOmsiFunctionalConversionResult? Functional);

public sealed record ProtonBusOmsiMapExportIssue(
    int? TileX,
    int? TileY,
    string Code,
    string Source,
    string? Detail = null);

public sealed record ProtonBusOmsiMapExportOptions(
    ProtonBusOmsiTileExportOptions? TileOptions = null)
{
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
    IReadOnlyList<ProtonBusOmsiMapExportIssue> Issues);

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

            var combinedScene =
                new ProtonBusExportScene(
                    geometry
                        .Scene
                        .Meshes
                        .Concat(
                            functional
                                .MarkerScene
                                .Meshes)
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
                    functional));
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
                    options.BusStops,
                Entrypoints =
                    options.Entrypoints,
                TrafficLights =
                    options.TrafficLights,
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
            issues);
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
            "sceneryAssetMissingAfterResolution";
}
