using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Terrain;
using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Indexing;
using MapStudio.Core.Omsi.Junctions;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Splines;

namespace MapStudio.Core.Omsi.Structures;

public sealed record MapStudioRoadBatchPlacement(
    int GraphSegmentId,
    string TraceId,
    int SplineId,
    string TilePath,
    int TileX,
    int TileY,
    double LocalX,
    double LocalZ,
    double Rotation,
    double LengthMeters,
    string SplinePath,
    bool Bridge,
    bool Tunnel);

public sealed record MapStudioRoadBatchReconstructionResult(
    IReadOnlyList<MapStudioRoadBatchPlacement> Placements,
    IReadOnlyList<MapStudioGeneratedSceneryPlacement> JunctionPlacements,
    int JunctionCandidateCount,
    int SkippedOutsideMapSegmentCount,
    int IgnoredWayCount,
    int MissingNodeReferenceCount,
    IReadOnlyList<string> BackupPaths,
    MapStudioRoadKitInstallResult RoadKit,
    IReadOnlyList<MapStudioGeneratedSceneryPlacement>? StructurePlacements = null,
    IReadOnlyList<MapStudioGeneratedSceneryPlacement>? TrafficSignalPlacements = null,
    IReadOnlyList<MapStudioGeneratedSceneryPlacement>? CrosswalkPlacements = null,
    IReadOnlyList<string>? ConsumedStreetFurnitureFeatureIds = null)
{
    public int PlacedSplineCount =>
        Placements.Count;

    public int InstalledOmsiSplineCount =>
        Placements.Count(
            placement =>
                !IsRoadKitSplinePath(
                    placement.SplinePath));

    public int RoadKitFallbackSplineCount =>
        Placements.Count -
        InstalledOmsiSplineCount;

    public int InstalledOmsiJunctionCount =>
        JunctionPlacements.Count(
            placement =>
                !IsGeneratedJunctionPath(
                    placement.SceneryObjectPath));

    public int GeneratedJunctionCount =>
        JunctionPlacements.Count -
        InstalledOmsiJunctionCount;

    public int GeneratedStructureCount =>
        StructurePlacements?.Count ??
        0;

    public int InstalledTrafficSignalCount =>
        TrafficSignalPlacements?.Count ??
        0;

    public int InstalledCrosswalkCount =>
        CrosswalkPlacements?.Count ??
        0;

    public int JunctionAccessoryCount =>
        InstalledTrafficSignalCount +
        InstalledCrosswalkCount;

    private static bool IsGeneratedJunctionPath(
        string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var normalized = path.Replace('/', '\\');

        return normalized.Contains(
            @"\" + MapStudioJunctionAssetGenerator.RootFolderName + @"\",
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRoadKitSplinePath(
        string path)
    {
        if (string.IsNullOrWhiteSpace(
                path))
        {
            return false;
        }

        var normalized =
            path
                .Replace(
                    '/',
                    '\\');

        return normalized.Contains(
            @"\" +
            MapStudioRoadKitGenerator
                .PackFolderName +
            @"\",
            StringComparison.OrdinalIgnoreCase);
    }

    public int ModifiedTileCount =>
        Placements
            .Select(
                placement =>
                    (
                        placement.TileX,
                        placement.TileY
                    ))
            .Concat(
                JunctionPlacements
                    .Select(
                        placement =>
                            (
                                placement.TileX,
                                placement.TileY
                            )))
            .Concat(
                (
                    StructurePlacements ??
                    Array.Empty<MapStudioGeneratedSceneryPlacement>()
                )
                    .Select(
                        placement =>
                            (
                                placement.TileX,
                                placement.TileY
                            )))
            .Concat(
                (
                    TrafficSignalPlacements ??
                    Array.Empty<MapStudioGeneratedSceneryPlacement>()
                )
                    .Select(
                        placement =>
                            (
                                placement.TileX,
                                placement.TileY
                            )))
            .Concat(
                (
                    CrosswalkPlacements ??
                    Array.Empty<MapStudioGeneratedSceneryPlacement>()
                )
                    .Select(
                        placement =>
                            (
                                placement.TileX,
                                placement.TileY
                            )))
            .Distinct()
            .Count();
}

public sealed class MapStudioRoadBatchReconstructionRunner
{
    private const double
        MinimumLayerVerticalSeparationMeters =
            4.8;

    private const double
        MaximumLayerRampGradient =
            0.08;

    private const double
        MaximumStraightSplineEndpointErrorMeters =
            0.02;

    private const double
        MaximumJunctionControlAssociationDistanceMeters =
            35.0;

    public async Task<MapStudioRoadBatchReconstructionResult>
        RunAsync(
            string omsiRoot,
            string mapDirectory,
            string osmXml,
            MapStudioGeographicAnchor anchor,
            CancellationToken cancellationToken = default,
            MapStudioGeoreferencedElevationSurface? elevation = null,
            IReadOnlyList<OmsiAssetIndexEntry>?
                installedRoadSplines = null,
            IReadOnlyList<OmsiAssetIndexEntry>?
                installedJunctionObjects = null,
            IReadOnlyList<OmsiAssetIndexEntry>?
                installedTrafficSignalObjects = null,
            IReadOnlyList<OmsiAssetIndexEntry>?
                installedCrosswalkAssets = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(omsiRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(mapDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(osmXml);

        var root =
            Path.GetFullPath(
                omsiRoot);

        var mapRoot =
            Path.GetFullPath(
                mapDirectory);

        var descriptor =
            await OmsiMapCatalog
                .OpenMapAsync(
                    mapRoot,
                    cancellationToken)
                .ConfigureAwait(false);

        if (descriptor.Tiles.Count == 0)
        {
            throw new InvalidDataException(
                "realWorldRoadMapHasNoTiles");
        }

        var mapBounds =
            new OmsiTileWorldBounds(
                OmsiTileGrid.GetOriginX(
                    descriptor.Tiles.Min(
                        tile =>
                            tile.X)),
                OmsiTileGrid.GetOriginZ(
                    descriptor.Tiles.Min(
                        tile =>
                            tile.Y)),
                OmsiTileGrid.GetOriginX(
                    descriptor.Tiles.Max(
                        tile =>
                            tile.X) +
                    1),
                OmsiTileGrid.GetOriginZ(
                    descriptor.Tiles.Max(
                        tile =>
                            tile.Y) +
                    1));

        var imported =
            new MapStudioOsmRoadImporter()
                .Parse(
                    osmXml);

        IReadOnlyDictionary<
            string,
            MapStudioOmsiRoadSplineMatch>
            installedRoadMatches =
                new Dictionary<
                    string,
                    MapStudioOmsiRoadSplineMatch>(
                        StringComparer.OrdinalIgnoreCase);

        if (
            installedRoadSplines is
                { Count: > 0 })
        {
            installedRoadMatches =
                await new MapStudioOmsiRoadSplineResolver()
                    .ResolveAsync(
                        root,
                        installedRoadSplines,
                        MapStudioStandardRoadCatalog
                            .Profiles,
                        cancellationToken)
                    .ConfigureAwait(false);
        }

        var traces =
            imported.Traces
                .Select(
                    trace =>
                    {
                        var profile =
                            MapStudioStandardRoadProfileSelector
                                .Select(
                                    trace.Highway,
                                    trace.LaneCount,
                                    trace.OneWay,
                                    trace.WidthMeters);

                        var splinePath =
                            !trace.Bridge &&
                            !trace.Tunnel &&
                            (trace.Layer ?? 0) ==
                                0 &&
                            installedRoadMatches
                                .TryGetValue(
                                    profile.Key,
                                    out var installedMatch)
                                ? installedMatch
                                    .RelativePath
                                : profile
                                    .RelativePath;

                        return new MapStudioRoadTrace(
                            trace.Id,
                            trace.Points
                                .Select(
                                    point =>
                                        MapStudioGeographicProjection
                                            .Project(
                                                anchor,
                                                point))
                                .ToArray(),
                            splinePath,
                            trace.LaneCount,
                            trace.OneWay,
                            trace.WidthMeters,
                            trace.ForwardLaneCount,
                            trace.BackwardLaneCount,
                            trace.Layer,
                            trace.Bridge,
                            trace.Tunnel,
                            SourceTopologyAuthoritative:
                                true);
                    })
                .Where(
                    trace =>
                        trace.Points.Count >= 2)
                .SelectMany(
                    trace =>
                        MapStudioRoadTraceClipper
                            .ClipToBounds(
                                trace,
                                mapBounds))
                .Where(
                    trace =>
                        trace.Points.Count >= 2)
                .ToArray();

        var graph =
            new MapStudioRoadGraphBuilder()
                .Build(
                    traces);

        var nodeById =
            graph.Nodes.ToDictionary(
                node =>
                    node.Id);

        var segmentsByNode =
            graph.Segments
                .SelectMany(
                    segment =>
                        new[]
                        {
                            (
                                NodeId:
                                    segment.FromNodeId,
                                Segment:
                                    segment
                            ),
                            (
                                NodeId:
                                    segment.ToNodeId,
                                Segment:
                                    segment
                            )
                        })
                .GroupBy(
                    item =>
                        item.NodeId)
                .ToDictionary(
                    group =>
                        group.Key,
                    group =>
                        group
                            .Select(
                                item =>
                                    item.Segment)
                            .ToArray());

        var junctionControls =
            new MapStudioOsmJunctionControlReader()
                .Parse(
                    osmXml,
                    anchor);

        var signalizedJunctionNodeIds =
            ResolveSignalizedJunctionNodeIds(
                graph,
                junctionControls);

        IReadOnlyDictionary<int, MapStudioOmsiJunctionMatch>
            installedJunctionMatches =
                new Dictionary<int, MapStudioOmsiJunctionMatch>();

        if (installedJunctionObjects is { Count: > 0 })
        {
            var targets =
                BuildInstalledJunctionTargets(
                    graph,
                    signalizedJunctionNodeIds);

            if (targets.Count > 0)
            {
                installedJunctionMatches =
                    await new MapStudioOmsiJunctionResolver()
                        .ResolveAsync(
                            root,
                            installedJunctionObjects,
                            targets,
                            cancellationToken)
                        .ConfigureAwait(false);
            }
        }

        var installedTrafficSignalPath =
            ResolveInstalledSceneryAssetPath(
                root,
                installedTrafficSignalObjects,
                MapStudioOriginalOmsiAssetRole
                    .TrafficSignalObject);

        var installedCrosswalkPath =
            ResolveInstalledSceneryAssetPath(
                root,
                installedCrosswalkAssets,
                MapStudioOriginalOmsiAssetRole
                    .CrosswalkObject);

        var placementSegments =
            ApplyInstalledJunctionTrims(
                graph.Segments,
                installedJunctionMatches);

        var structuralElevations =
            BuildStructuralEndpointElevations(
                graph.Segments,
                nodeById,
                segmentsByNode,
                anchor,
                elevation);

        var roadKit =
            await new MapStudioRoadKitGenerator()
                .InstallOrUpdateAsync(
                    root,
                    cancellationToken)
                .ConfigureAwait(false);

        var tiles =
            new Dictionary<
                (int X, int Y),
                TileState>();

        var maximumUsedId =
            0;

        foreach (var tile in descriptor.Tiles)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            if (
                !OmsiMapPathResolver
                    .TryResolveTilePath(
                        mapRoot,
                        tile.RelativeMapPath,
                        out var tilePath) ||
                !File.Exists(tilePath))
            {
                throw new InvalidDataException(
                    "realWorldRoadTilePathInvalid");
            }

            var document =
                await OmsiConfigParser
                    .ParseFileAsync(
                        tilePath,
                        cancellationToken)
                    .ConfigureAwait(false);

            maximumUsedId =
                Math.Max(
                    maximumUsedId,
                    OmsiTileElementIdScanner
                        .FindMaxUsedId(
                            document));

            if (
                !tiles.TryAdd(
                    (
                        tile.X,
                        tile.Y
                    ),
                    new TileState(
                        tile.X,
                        tile.Y,
                        tilePath,
                        document)))
            {
                throw new InvalidDataException(
                    "realWorldRoadDuplicateTileCoordinate");
            }
        }

        var planned =
            new List<PlannedSpline>();

        var skippedOutside =
            0;

        foreach (var segment in placementSegments)
        {
            var startTile =
                (
                    X:
                        OmsiTileGrid
                            .WorldToTileX(
                                segment.Start.X),
                    Y:
                        OmsiTileGrid
                            .WorldToTileY(
                                segment.Start.Z)
                );

            var endTile =
                (
                    X:
                        OmsiTileGrid
                            .WorldToTileX(
                                segment.End.X),
                    Y:
                        OmsiTileGrid
                            .WorldToTileY(
                                segment.End.Z)
                );

            if (
                !tiles.TryGetValue(
                    startTile,
                    out var tile) ||
                !tiles.ContainsKey(
                    endTile))
            {
                skippedOutside++;
                continue;
            }

            if (
                maximumUsedId ==
                int.MaxValue)
            {
                throw new InvalidDataException(
                    "objectIdExhausted");
            }

            maximumUsedId++;

            var rotation =
                ResolveRotationDegrees(
                    segment.Start,
                    segment.End);

            var (
                placementBridge,
                placementTunnel
            ) =
                ResolvePlacementStructure(
                    segment);

            var splinePath =
                MapStudioStandardRoadCatalog
                    .ResolvePlacementRelativePath(
                        segment.ProfileId,
                        placementBridge,
                        placementTunnel);

            double startHeight;
            double endHeight;

            if (
                structuralElevations.TryGetValue(
                    segment.Id,
                    out var structural))
            {
                startHeight =
                    structural.StartHeight;

                endHeight =
                    structural.EndHeight;
            }
            else
            {
                startHeight =
                    SampleTerrainHeight(
                        elevation,
                        anchor,
                        segment.Start);

                endHeight =
                    SampleTerrainHeight(
                        elevation,
                        anchor,
                        segment.End,
                        startHeight);
            }

            var gradientPercent =
                ResolveGradientPercent(
                    startHeight,
                    endHeight,
                    segment.LengthMeters);

            var plannedSpline =
                new PlannedSpline(
                    segment,
                    tile,
                    maximumUsedId,
                    OmsiTileGrid
                        .WorldToLocalX(
                            segment.Start.X,
                            tile.X),
                    OmsiTileGrid
                        .WorldToLocalZ(
                            segment.Start.Z,
                            tile.Y),
                    startHeight,
                    endHeight,
                    gradientPercent,
                    rotation,
                    splinePath);

            ValidateStraightSplineEndpoint(
                plannedSpline);

            planned.Add(
                plannedSpline);
        }

        var structurePlan =
            new MapStudioRoadStructureSceneryPlanner()
                .Build(
                    planned
                        .Select(
                            item =>
                                new MapStudioRoadStructureSpline(
                                    item.Segment,
                                    item.StartHeight,
                                    item.EndHeight))
                        .ToArray(),
                    anchor,
                    elevation);

        if (planned.Count == 0)
        {
            return new MapStudioRoadBatchReconstructionResult(
                Array.Empty<MapStudioRoadBatchPlacement>(),
                Array.Empty<MapStudioGeneratedSceneryPlacement>(),
                graph.Junctions.Count,
                skippedOutside,
                imported.IgnoredWayCount,
                imported.MissingNodeReferenceCount,
                Array.Empty<string>(),
                roadKit);
        }

        var writes =
            new List<TileWrite>();

        foreach (
            var group in
                planned
                    .GroupBy(
                        placement =>
                            (
                                placement.Tile.X,
                                placement.Tile.Y
                            )))
        {
            var placements =
                group.ToArray();

            var tile =
                placements[0]
                    .Tile;

            var appended =
                OmsiTileSplineInserter
                    .AppendMany(
                        tile.Document,
                        placements
                            .Select(
                                placement =>
                                    new OmsiNewPlacedSpline(
                                        "0",
                                        placement.SplinePath,
                                        placement.SplineId,
                                        -1,
                                        -1,
                                        placement.LocalX,
                                        placement.StartHeight,
                                        placement.LocalZ,
                                        placement.Rotation,
                                        placement.Segment
                                            .LengthMeters,
                                        0,
                                        placement.GradientPercent,
                                        placement.GradientPercent,
                                        false,
                                        Array.Empty<string>()))
                            .ToArray());

            writes.Add(
                new TileWrite(
                    tile,
                    appended.Bytes));
        }

        var backupRoot =
            Path.Combine(
                root,
                ".mapstudio",
                "backups",
                "real-world-road-batch",
                DateTime.UtcNow
                    .ToString(
                        "yyyyMMdd-HHmmss",
                        System.Globalization
                            .CultureInfo
                            .InvariantCulture) +
                "-" +
                Guid.NewGuid()
                    .ToString("N")[..8]);

        Directory.CreateDirectory(
            backupRoot);

        var backupPaths =
            new List<string>(
                writes.Count);

        foreach (var write in writes)
        {
            var relative =
                Path.GetRelativePath(
                    mapRoot,
                    write.Tile.Path);

            var backupPath =
                Path.Combine(
                    backupRoot,
                    relative);

            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    backupPath)!);

            File.Copy(
                write.Tile.Path,
                backupPath,
                overwrite: true);

            write.BackupPath =
                backupPath;

            backupPaths.Add(
                backupPath);
        }

        try
        {
            foreach (var write in writes)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                write.TemporaryPath =
                    write.Tile.Path +
                    ".mapstudio-tmp-" +
                    Guid.NewGuid()
                        .ToString("N");

                await File
                    .WriteAllBytesAsync(
                        write.TemporaryPath,
                        write.Bytes,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            foreach (var write in writes)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                File.Move(
                    write.TemporaryPath!,
                    write.Tile.Path,
                    overwrite: true);

                write.TemporaryPath =
                    null;
            }
        }
        catch
        {
            foreach (var write in writes)
            {
                if (
                    !string.IsNullOrWhiteSpace(
                        write.BackupPath) &&
                    File.Exists(
                        write.BackupPath))
                {
                    File.Copy(
                        write.BackupPath,
                        write.Tile.Path,
                        overwrite: true);
                }
            }

            throw;
        }
        finally
        {
            foreach (var write in writes)
            {
                if (
                    !string.IsNullOrWhiteSpace(
                        write.TemporaryPath) &&
                    File.Exists(
                        write.TemporaryPath))
                {
                    File.Delete(
                        write.TemporaryPath);
                }
            }
        }

        MapStudioGeneratedSceneryBatchWriteResult
            sceneryWrite =
                new(
                    Array.Empty<MapStudioGeneratedSceneryPlacement>(),
                    Array.Empty<string>(),
                    Array.Empty<string>());

        var consumedStreetFurnitureFeatureIds =
            new HashSet<string>(
                StringComparer.Ordinal);

        try
        {
            var sceneryRequests =
                new List<MapStudioGeneratedSceneryPlacementRequest>();

            var junctionGenerator =
                new MapStudioJunctionAssetGenerator();

            foreach (var junction in graph.Junctions)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                var connected =
                    graph.Segments
                        .Where(
                            segment =>
                                segment.FromNodeId ==
                                    junction.NodeId ||
                                segment.ToNodeId ==
                                    junction.NodeId)
                        .Select(
                            segment =>
                                (
                                    Segment:
                                        segment,
                                    Profile:
                                        ResolveProfile(
                                            segment.ProfileId)
                                ))
                        .Where(
                            item =>
                                item.Profile?.IsPedestrian !=
                                    true)
                        .ToArray();

                if (connected.Length < 3)
                {
                    continue;
                }

                var arms =
                    connected
                        .Select(
                            item =>
                            {
                                var other =
                                    item.Segment.FromNodeId ==
                                        junction.NodeId
                                        ? item.Segment.End
                                        : item.Segment.Start;

                                var profile =
                                    item.Profile;

                                var width =
                                    Math.Max(
                                        item.Segment.WidthMeters ??
                                            0,
                                        profile?.TotalWidthMeters ??
                                            7.0);

                                return new MapStudioJunctionArm(
                                    ResolveRotationDegrees(
                                        junction.Position,
                                        other),
                                    width,
                                    item.Segment.LaneCount ??
                                        profile?.LaneCount ??
                                        2,
                                    item.Segment.OneWay ??
                                        profile?.OneWay ??
                                        false,
                                    profile?.LaneWidthMeters ??
                                        3.5);
                            })
                        .ToArray();

                var structureKind =
                    connected.All(
                        item =>
                            ResolvePlacementStructure(
                                item.Segment)
                                .Bridge)
                        ? MapStudioJunctionStructureKind.Bridge
                        : connected.All(
                            item =>
                                ResolvePlacementStructure(
                                    item.Segment)
                                    .Tunnel)
                            ? MapStudioJunctionStructureKind.Tunnel
                            : MapStudioJunctionStructureKind.Ground;

                var junctionHeight =
                    ResolveJunctionPlacementHeight(
                        junction.NodeId,
                        junction.Position,
                        connected
                            .Select(
                                item =>
                                    item.Segment)
                            .ToArray(),
                        structureKind,
                        structuralElevations,
                        anchor,
                        elevation);

                string junctionAssetPath;
                double junctionRotation;

                if (
                    structureKind ==
                        MapStudioJunctionStructureKind.Ground &&
                    installedJunctionMatches
                        .TryGetValue(
                            junction.NodeId,
                            out var installedJunction))
                {
                    junctionAssetPath =
                        Path.Combine(
                            root,
                            installedJunction
                                .RelativePath
                                .Replace(
                                    '/',
                                    Path.DirectorySeparatorChar)
                                .Replace(
                                    '\\',
                                    Path.DirectorySeparatorChar));

                    junctionRotation =
                        installedJunction.RotationDegrees;
                }
                else
                {
                    var asset =
                        await junctionGenerator
                            .GenerateAsync(
                                root,
                                new MapStudioJunctionSpec(
                                    "OSM Junction " +
                                        junction.NodeId,
                                    arms,
                                    StructureKind:
                                        structureKind),
                                cancellationToken)
                            .ConfigureAwait(false);

                    junctionAssetPath =
                        asset.SceneryObjectPath;

                    junctionRotation =
                        0;
                }

                var junctionTerrainHeight =
                    SampleTerrainHeight(
                        elevation,
                        anchor,
                        junction.Position,
                        junctionHeight);

                sceneryRequests.Add(
                    new MapStudioGeneratedSceneryPlacementRequest(
                        "osm-junction-" +
                            junction.NodeId,
                        junctionAssetPath,
                        junction.Position,
                        HeightMeters:
                            junctionHeight -
                            junctionTerrainHeight,
                        Rotation:
                            junctionRotation));
            }

            AppendJunctionAccessoryRequests(
                sceneryRequests,
                graph,
                installedJunctionMatches,
                junctionControls,
                installedTrafficSignalPath,
                installedCrosswalkPath,
                consumedStreetFurnitureFeatureIds,
                anchor,
                elevation);

            var bridgeAssets =
                new Dictionary<
                    string,
                    string>(
                    StringComparer.OrdinalIgnoreCase);

            var bridgeGenerator =
                new MapStudioBridgePierAssetGenerator();

            foreach (
                var group in
                    structurePlan
                        .BridgePiers
                        .GroupBy(
                            item =>
                                item.AssetName,
                            StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                var sample =
                    group.First();

                var asset =
                    await bridgeGenerator
                        .GenerateAsync(
                            root,
                            sample.Spec,
                            cancellationToken)
                        .ConfigureAwait(false);

                bridgeAssets[
                    group.Key] =
                    asset.SceneryObjectPath;
            }

            foreach (
                var item in
                    structurePlan.BridgePiers)
            {
                sceneryRequests.Add(
                    new MapStudioGeneratedSceneryPlacementRequest(
                        item.Id,
                        bridgeAssets[
                            item.AssetName],
                        item.WorldCenter,
                        0,
                        item.Rotation));
            }

            var tunnelAssets =
                new Dictionary<
                    string,
                    string>(
                    StringComparer.OrdinalIgnoreCase);

            var tunnelGenerator =
                new MapStudioTunnelPortalAssetGenerator();

            foreach (
                var group in
                    structurePlan
                        .TunnelPortals
                        .GroupBy(
                            item =>
                                item.AssetName,
                            StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                var sample =
                    group.First();

                var asset =
                    await tunnelGenerator
                        .GenerateAsync(
                            root,
                            sample.Spec,
                            cancellationToken)
                        .ConfigureAwait(false);

                tunnelAssets[
                    group.Key] =
                    asset.SceneryObjectPath;
            }

            foreach (
                var item in
                    structurePlan.TunnelPortals)
            {
                sceneryRequests.Add(
                    new MapStudioGeneratedSceneryPlacementRequest(
                        item.Id,
                        tunnelAssets[
                            item.AssetName],
                        item.WorldCenter,
                        item.HeightMeters -
                            SampleTerrainHeight(
                                elevation,
                                anchor,
                                item.WorldCenter,
                                item.HeightMeters),
                        item.Rotation));
            }

            if (sceneryRequests.Count > 0)
            {
                sceneryWrite =
                    await new MapStudioGeneratedSceneryBatchWriter()
                        .WriteAsync(
                            root,
                            mapRoot,
                            sceneryRequests,
                            cancellationToken)
                        .ConfigureAwait(false);

                backupPaths.AddRange(
                    sceneryWrite.BackupPaths);
            }
        }
        catch
        {
            foreach (var write in writes)
            {
                if (
                    !string.IsNullOrWhiteSpace(
                        write.BackupPath) &&
                    File.Exists(
                        write.BackupPath))
                {
                    File.Copy(
                        write.BackupPath,
                        write.Tile.Path,
                        overwrite: true);
                }
            }

            throw;
        }

        return new MapStudioRoadBatchReconstructionResult(
            planned
                .Select(
                    placement =>
                        new MapStudioRoadBatchPlacement(
                            placement.Segment.Id,
                            placement.Segment.TraceId,
                            placement.SplineId,
                            placement.Tile.Path,
                            placement.Tile.X,
                            placement.Tile.Y,
                            placement.LocalX,
                            placement.LocalZ,
                            placement.Rotation,
                            placement.Segment.LengthMeters,
                            placement.SplinePath,
                            ResolvePlacementStructure(
                                placement.Segment)
                                .Bridge,
                            ResolvePlacementStructure(
                                placement.Segment)
                                .Tunnel))
                .ToArray(),
            sceneryWrite
                .Placements
                .Where(
                    placement =>
                        placement.Id.StartsWith(
                            "osm-junction-",
                            StringComparison.Ordinal))
                .ToArray(),
            graph.Junctions.Count,
            skippedOutside,
            imported.IgnoredWayCount,
            imported.MissingNodeReferenceCount,
            backupPaths,
            roadKit,
            sceneryWrite
                .Placements
                .Where(
                    placement =>
                        placement.Id.StartsWith(
                            "osm-bridge-pier-",
                            StringComparison.Ordinal) ||
                        placement.Id.StartsWith(
                            "osm-tunnel-portal-",
                            StringComparison.Ordinal))
                .ToArray(),
            sceneryWrite
                .Placements
                .Where(
                    placement =>
                        placement.Id.StartsWith(
                            "osm-traffic-signal-",
                            StringComparison.Ordinal))
                .ToArray(),
            sceneryWrite
                .Placements
                .Where(
                    placement =>
                        placement.Id.StartsWith(
                            "osm-crosswalk-",
                            StringComparison.Ordinal))
                .ToArray(),
            consumedStreetFurnitureFeatureIds
                .OrderBy(
                    id =>
                        id,
                    StringComparer.Ordinal)
                .ToArray());
    }

    private static string? ResolveInstalledSceneryAssetPath(
        string omsiRoot,
        IReadOnlyList<OmsiAssetIndexEntry>? entries,
        MapStudioOriginalOmsiAssetRole preferredRole)
    {
        if (entries is not { Count: > 0 })
        {
            return null;
        }

        foreach (
            var entry in
                entries
                    .Where(
                        item =>
                            item.Kind ==
                                OmsiAssetKind.SceneryObject)
                    .OrderBy(
                        item =>
                            MapStudioOriginalOmsiAssetCatalog
                                .Classify(
                                    item) ==
                                preferredRole
                                ? 0
                                : 1)
                    .ThenBy(
                        item =>
                            item.RelativePath,
                        StringComparer.OrdinalIgnoreCase))
        {
            var fullPath =
                Path.Combine(
                    omsiRoot,
                    entry.RelativePath
                        .Replace(
                            '/',
                            Path.DirectorySeparatorChar)
                        .Replace(
                            '\\',
                            Path.DirectorySeparatorChar));

            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }

    private static void AppendJunctionAccessoryRequests(
        ICollection<MapStudioGeneratedSceneryPlacementRequest> requests,
        MapStudioRoadGraph graph,
        IReadOnlyDictionary<int, MapStudioOmsiJunctionMatch>
            installedJunctionMatches,
        IReadOnlyList<MapStudioOsmJunctionControl> controls,
        string? trafficSignalAssetPath,
        string? crosswalkAssetPath,
        ISet<string> consumedStreetFurnitureFeatureIds,
        MapStudioGeographicAnchor anchor,
        MapStudioGeoreferencedElevationSurface? elevation)
    {
        if (
            controls.Count == 0 ||
            (
                string.IsNullOrWhiteSpace(
                    trafficSignalAssetPath) &&
                string.IsNullOrWhiteSpace(
                    crosswalkAssetPath)
            ))
        {
            return;
        }

        var signalArms =
            new HashSet<(int JunctionId, int SegmentId)>();

        var crosswalkArms =
            new HashSet<(int JunctionId, int SegmentId)>();

        foreach (var control in controls)
        {
            var junction =
                graph.Junctions
                    .Select(
                        item =>
                            (
                                Junction:
                                    item,
                                Distance:
                                    item.Position
                                        .DistanceTo(
                                            control.WorldPoint)
                            ))
                    .Where(
                        item =>
                            item.Distance <=
                                MaximumJunctionControlAssociationDistanceMeters)
                    .OrderBy(
                        item =>
                            item.Distance)
                    .Select(
                        item =>
                            item.Junction)
                    .FirstOrDefault();

            if (junction is null)
            {
                continue;
            }

            var connected =
                graph.Segments
                    .Where(
                        segment =>
                            segment.FromNodeId ==
                                junction.NodeId ||
                            segment.ToNodeId ==
                                junction.NodeId)
                    .Where(
                        segment =>
                            ResolveProfile(
                                segment.ProfileId)
                                ?.IsPedestrian !=
                            true)
                    .ToArray();

            if (connected.Length < 3)
            {
                continue;
            }

            if (
                control.Kind ==
                    MapStudioOsmJunctionControlKind
                        .TrafficSignal &&
                !string.IsNullOrWhiteSpace(
                    trafficSignalAssetPath))
            {
                var controlDistance =
                    junction.Position
                        .DistanceTo(
                            control.WorldPoint);

                var targetSegments =
                    controlDistance <=
                        3.0
                        ? connected
                        : new[]
                        {
                            FindBestJunctionArm(
                                junction,
                                connected,
                                control.WorldPoint)
                        }
                            .Where(
                                segment =>
                                    segment is not null)
                            .Cast<MapStudioRoadGraphSegment>()
                            .ToArray();

                foreach (
                    var segment in targetSegments)
                {
                    if (
                        !signalArms.Add(
                            (
                                junction.NodeId,
                                segment.Id
                            )))
                    {
                        continue;
                    }

                    var other =
                        segment.FromNodeId ==
                            junction.NodeId
                            ? segment.End
                            : segment.Start;

                    var armDistance =
                        junction.Position
                            .DistanceTo(
                                other);

                    if (armDistance <= 0.001)
                    {
                        continue;
                    }

                    var ux =
                        (
                            other.X -
                            junction.Position.X
                        ) /
                        armDistance;

                    var uz =
                        (
                            other.Z -
                            junction.Position.Z
                        ) /
                        armDistance;

                    var width =
                        Math.Max(
                            segment.WidthMeters ??
                                0,
                            ResolveProfile(
                                segment.ProfileId)
                                ?.TotalWidthMeters ??
                                7.0);

                    var trimDistance =
                        installedJunctionMatches
                            .TryGetValue(
                                junction.NodeId,
                                out var match) &&
                        match.TrimDistanceBySegmentId
                            .TryGetValue(
                                segment.Id,
                                out var trim)
                            ? trim
                            : Math.Min(
                                8.0,
                                segment.LengthMeters *
                                0.25);

                    var longitudinal =
                        Math.Max(
                            trimDistance +
                                0.75,
                            Math.Min(
                                controlDistance,
                                trimDistance +
                                    4.0));

                    var lateral =
                        Math.Max(
                            2.5,
                            width /
                            2.0) +
                        0.6;

                    var worldPoint =
                        new MapStudioRoadPoint(
                            junction.Position.X +
                                ux *
                                longitudinal -
                                uz *
                                lateral,
                            junction.Position.Z +
                                uz *
                                longitudinal +
                                ux *
                                lateral);

                    var terrainHeight =
                        SampleTerrainHeight(
                            elevation,
                            anchor,
                            worldPoint);

                    consumedStreetFurnitureFeatureIds.Add(
                        "osm-street-furniture-" +
                            control.NodeId);

                    requests.Add(
                        new MapStudioGeneratedSceneryPlacementRequest(
                            "osm-traffic-signal-" +
                                control.NodeId +
                                "-" +
                                junction.NodeId +
                                "-" +
                                segment.Id,
                            trafficSignalAssetPath,
                            worldPoint,
                            HeightMeters:
                                terrainHeight -
                                terrainHeight,
                            Rotation:
                                ResolveRotationDegrees(
                                    junction.Position,
                                    other)));
                }

                continue;
            }

            if (
                control.Kind !=
                    MapStudioOsmJunctionControlKind
                        .Crosswalk ||
                string.IsNullOrWhiteSpace(
                    crosswalkAssetPath))
            {
                continue;
            }

            var distanceFromCenter =
                junction.Position
                    .DistanceTo(
                        control.WorldPoint);

            if (distanceFromCenter <= 1.0)
            {
                continue;
            }

            var crosswalkSegment =
                FindBestJunctionArm(
                    junction,
                    connected,
                    control.WorldPoint);

            if (
                crosswalkSegment is null ||
                !crosswalkArms.Add(
                    (
                        junction.NodeId,
                        crosswalkSegment.Id
                    )))
            {
                continue;
            }

            var crosswalkOther =
                crosswalkSegment.FromNodeId ==
                    junction.NodeId
                    ? crosswalkSegment.End
                    : crosswalkSegment.Start;

            var armLength =
                junction.Position
                    .DistanceTo(
                        crosswalkOther);

            if (armLength <= 0.001)
            {
                continue;
            }

            var armUx =
                (
                    crosswalkOther.X -
                    junction.Position.X
                ) /
                armLength;

            var armUz =
                (
                    crosswalkOther.Z -
                    junction.Position.Z
                ) /
                armLength;

            var minimumDistance =
                installedJunctionMatches
                    .TryGetValue(
                        junction.NodeId,
                        out var crosswalkMatch) &&
                crosswalkMatch.TrimDistanceBySegmentId
                    .TryGetValue(
                        crosswalkSegment.Id,
                        out var crosswalkTrim)
                    ? crosswalkTrim +
                        1.0
                    : 4.0;

            var placementDistance =
                Math.Max(
                    minimumDistance,
                    distanceFromCenter);

            var crosswalkPoint =
                new MapStudioRoadPoint(
                    junction.Position.X +
                        armUx *
                        placementDistance,
                    junction.Position.Z +
                        armUz *
                        placementDistance);

            consumedStreetFurnitureFeatureIds.Add(
                "osm-street-furniture-" +
                    control.NodeId);

            requests.Add(
                new MapStudioGeneratedSceneryPlacementRequest(
                    "osm-crosswalk-" +
                        control.NodeId +
                        "-" +
                        junction.NodeId +
                        "-" +
                        crosswalkSegment.Id,
                    crosswalkAssetPath,
                    crosswalkPoint,
                    Rotation:
                        NormalizeRotationDegrees(
                            ResolveRotationDegrees(
                                junction.Position,
                                crosswalkOther) +
                            90.0)));
        }
    }

    private static MapStudioRoadGraphSegment?
        FindBestJunctionArm(
            MapStudioRoadJunction junction,
            IReadOnlyList<MapStudioRoadGraphSegment> connected,
            MapStudioRoadPoint controlPoint)
    {
        var targetAngle =
            ResolveRotationDegrees(
                junction.Position,
                controlPoint);

        return connected
            .Select(
                segment =>
                {
                    var other =
                        segment.FromNodeId ==
                            junction.NodeId
                            ? segment.End
                            : segment.Start;

                    var armAngle =
                        ResolveRotationDegrees(
                            junction.Position,
                            other);

                    return (
                        Segment:
                            segment,
                        Error:
                            Math.Abs(
                                SignedAngleDeltaDegrees(
                                    armAngle,
                                    targetAngle))
                    );
                })
            .OrderBy(
                item =>
                    item.Error)
            .ThenBy(
                item =>
                    item.Segment.Id)
            .Select(
                item =>
                    item.Segment)
            .FirstOrDefault();
    }

    private static double SignedAngleDeltaDegrees(
        double from,
        double to)
    {
        var delta =
            NormalizeRotationDegrees(
                to) -
            NormalizeRotationDegrees(
                from);

        while (delta > 180.0)
        {
            delta -= 360.0;
        }

        while (delta <= -180.0)
        {
            delta += 360.0;
        }

        return delta;
    }

    private static double NormalizeRotationDegrees(
        double value)
    {
        value %= 360.0;

        return value < 0
            ? value +
                360.0
            : value;
    }

    private static IReadOnlyDictionary<
        int,
        StructuralEndpointElevation>
        BuildStructuralEndpointElevations(
            IReadOnlyList<MapStudioRoadGraphSegment>
                segments,
            IReadOnlyDictionary<
                int,
                MapStudioRoadGraphNode>
                nodeById,
            IReadOnlyDictionary<
                int,
                MapStudioRoadGraphSegment[]>
                segmentsByNode,
            MapStudioGeographicAnchor anchor,
            MapStudioGeoreferencedElevationSurface?
                elevation)
    {
        var result =
            new Dictionary<
                int,
                StructuralEndpointElevation>();

        foreach (
            var traceGroup in
                segments
                    .GroupBy(
                        segment =>
                            segment.TraceId,
                        StringComparer
                            .OrdinalIgnoreCase))
        {
            var ordered =
                traceGroup
                    .OrderBy(
                        segment =>
                            segment.Id)
                    .ToArray();

            for (
                var index = 0;
                index <
                    ordered.Length;)
            {
                if (!IsStructuralSegment(
                    ordered[index]))
                {
                    index++;

                    continue;
                }

                var runStart =
                    index;

                var runEnd =
                    index;

                while (
                    runEnd + 1 <
                        ordered.Length &&
                    ordered[runEnd]
                        .ToNodeId ==
                    ordered[
                        runEnd +
                        1]
                        .FromNodeId &&
                    HasCompatibleGradeSeparation(
                        ordered[runEnd],
                        ordered[
                            runEnd +
                            1]) &&
                    IsStructuralSegment(
                        ordered[
                            runEnd +
                            1]))
                {
                    runEnd++;
                }

                var first =
                    ordered[
                        runStart];

                var last =
                    ordered[
                        runEnd];

                var startHeight =
                    SampleTerrainHeight(
                        elevation,
                        anchor,
                        first.Start);

                var endHeight =
                    SampleTerrainHeight(
                        elevation,
                        anchor,
                        last.End,
                        startHeight);

                startHeight +=
                    ResolveStructuralJunctionBoundaryOffset(
                        first.FromNodeId,
                        first,
                        nodeById,
                        segmentsByNode);

                endHeight +=
                    ResolveStructuralJunctionBoundaryOffset(
                        last.ToNodeId,
                        last,
                        nodeById,
                        segmentsByNode);

                var totalLength =
                    0.0;

                for (
                    var runIndex =
                        runStart;
                    runIndex <=
                        runEnd;
                    runIndex++)
                {
                    totalLength +=
                        Math.Max(
                            0,
                            ordered[
                                runIndex]
                                .LengthMeters);
                }

                if (
                    totalLength <=
                    0.0001)
                {
                    index =
                        runEnd +
                        1;

                    continue;
                }

                var layerDirection =
                    ResolveLayerVerticalDirection(
                        first);

                var targetSeparation =
                    ResolveLayerTargetSeparationMeters(
                        first);

                var traveled =
                    0.0;

                for (
                    var runIndex =
                        runStart;
                    runIndex <=
                        runEnd;
                    runIndex++)
                {
                    var segment =
                        ordered[
                            runIndex];

                    var segmentStartHeight =
                        LerpHeight(
                            startHeight,
                            endHeight,
                            traveled /
                            totalLength);

                    segmentStartHeight +=
                        ResolveLayerClearanceOffset(
                            elevation,
                            anchor,
                            segment.Start,
                            segmentStartHeight,
                            traveled,
                            totalLength,
                            layerDirection,
                            targetSeparation);

                    traveled +=
                        Math.Max(
                            0,
                            segment.LengthMeters);

                    var segmentEndHeight =
                        LerpHeight(
                            startHeight,
                            endHeight,
                            traveled /
                            totalLength);

                    segmentEndHeight +=
                        ResolveLayerClearanceOffset(
                            elevation,
                            anchor,
                            segment.End,
                            segmentEndHeight,
                            traveled,
                            totalLength,
                            layerDirection,
                            targetSeparation);

                    result[
                        segment.Id] =
                        new StructuralEndpointElevation(
                            segmentStartHeight,
                            segmentEndHeight);
                }

                index =
                    runEnd +
                    1;
            }
        }

        return result;
    }

    private static double ResolveJunctionPlacementHeight(
        int nodeId,
        MapStudioRoadPoint position,
        IReadOnlyList<MapStudioRoadGraphSegment>
            connected,
        MapStudioJunctionStructureKind
            structureKind,
        IReadOnlyDictionary<
            int,
            StructuralEndpointElevation>
            structuralElevations,
        MapStudioGeographicAnchor anchor,
        MapStudioGeoreferencedElevationSurface?
            elevation)
    {
        if (
            structureKind !=
                MapStudioJunctionStructureKind
                    .Ground)
        {
            var heights =
                new List<double>(
                    connected.Count);

            foreach (var segment in connected)
            {
                if (
                    !structuralElevations.TryGetValue(
                        segment.Id,
                        out var structural))
                {
                    continue;
                }

                heights.Add(
                    segment.FromNodeId ==
                        nodeId
                        ? structural.StartHeight
                        : structural.EndHeight);
            }

            if (
                heights.Count ==
                    connected.Count &&
                heights.Count >
                    0)
            {
                return heights.Average();
            }
        }

        return SampleTerrainHeight(
            elevation,
            anchor,
            position);
    }

    private static double SampleTerrainHeight(
        MapStudioGeoreferencedElevationSurface?
            elevation,
        MapStudioGeographicAnchor anchor,
        MapStudioRoadPoint point,
        double fallback = 0) =>
        elevation?.SampleRelativeHeightOrDefault(
            anchor,
            point,
            fallback) ??
        fallback;

    private static bool IsStructuralSegment(
        MapStudioRoadGraphSegment segment)
    {
        var structure =
            ResolvePlacementStructure(
                segment);

        return structure.Bridge ||
            structure.Tunnel ||
            (segment.Layer ?? 0) !=
                0;
    }

    private static bool HasCompatibleGradeSeparation(
        MapStudioRoadGraphSegment left,
        MapStudioRoadGraphSegment right)
    {
        var leftStructure =
            ResolvePlacementStructure(
                left);

        var rightStructure =
            ResolvePlacementStructure(
                right);

        return leftStructure ==
                rightStructure &&
            ResolveEffectiveStructuralLayer(
                left) ==
            ResolveEffectiveStructuralLayer(
                right);
    }

    private static int ResolveEffectiveStructuralLayer(
        MapStudioRoadGraphSegment segment)
    {
        if (
            segment.Layer is
                { } layer &&
            layer !=
                0)
        {
            return layer;
        }

        var structure =
            ResolvePlacementStructure(
                segment);

        if (structure.Bridge)
        {
            return 1;
        }

        if (structure.Tunnel)
        {
            return -1;
        }

        return 0;
    }

    private static double
        ResolveStructuralJunctionBoundaryOffset(
            int nodeId,
            MapStudioRoadGraphSegment segment,
            IReadOnlyDictionary<
                int,
                MapStudioRoadGraphNode>
                nodeById,
            IReadOnlyDictionary<
                int,
                MapStudioRoadGraphSegment[]>
                segmentsByNode)
    {
        if (
            !IsStructuralSegment(
                segment) ||
            !nodeById.TryGetValue(
                nodeId,
                out var node) ||
            !node.IsJunction ||
            !segmentsByNode.TryGetValue(
                nodeId,
                out var incident) ||
            incident.Length <
                3 ||
            incident.Any(
                candidate =>
                    !IsStructuralSegment(
                        candidate) ||
                    !HasCompatibleGradeSeparation(
                        segment,
                        candidate)))
        {
            return 0;
        }

        return ResolveLayerVerticalDirection(
                segment) *
            ResolveLayerTargetSeparationMeters(
                segment);
    }

    private static (
        bool Bridge,
        bool Tunnel)
        ResolvePlacementStructure(
            MapStudioRoadGraphSegment segment)
    {
        if (
            segment.Bridge !=
            segment.Tunnel)
        {
            return
                (
                    segment.Bridge,
                    segment.Tunnel
                );
        }

        var layer =
            segment.Layer ??
            0;

        return layer switch
        {
            > 0 =>
                (
                    true,
                    false
                ),
            < 0 =>
                (
                    false,
                    true
                ),
            _ =>
                (
                    false,
                    false
                )
        };
    }

    private static int ResolveLayerVerticalDirection(
        MapStudioRoadGraphSegment segment)
    {
        var structure =
            ResolvePlacementStructure(
                segment);

        if (structure.Bridge)
        {
            return 1;
        }

        if (structure.Tunnel)
        {
            return -1;
        }

        return Math.Sign(
            segment.Layer ??
            0);
    }

    private static double
        ResolveLayerTargetSeparationMeters(
            MapStudioRoadGraphSegment segment)
    {
        var layer =
            Math.Abs(
                ResolveEffectiveStructuralLayer(
                    segment));

        return layer ==
                0
            ? 0
            : MinimumLayerVerticalSeparationMeters *
                layer;
    }

    private static double ResolveLayerClearanceOffset(
        MapStudioGeoreferencedElevationSurface?
            elevation,
        MapStudioGeographicAnchor anchor,
        MapStudioRoadPoint point,
        double baselineHeight,
        double traveled,
        double totalLength,
        int direction,
        double targetSeparation)
    {
        if (
            direction ==
                0 ||
            targetSeparation <=
                0 ||
            traveled <=
                0.0001 ||
            traveled >=
                totalLength -
                    0.0001)
        {
            return 0;
        }

        var terrainHeight =
            SampleTerrainHeight(
                elevation,
                anchor,
                point,
                baselineHeight);

        var currentSeparation =
            direction *
            (
                baselineHeight -
                terrainHeight
            );

        var needed =
            Math.Max(
                0,
                targetSeparation -
                    currentSeparation);

        if (
            needed <=
            0.0001)
        {
            return 0;
        }

        var rampCapacity =
            Math.Min(
                traveled,
                totalLength -
                    traveled) *
            MaximumLayerRampGradient;

        return direction *
            Math.Min(
                needed,
                Math.Max(
                    0,
                    rampCapacity));
    }

    private static double LerpHeight(
        double start,
        double end,
        double amount) =>
        start +
        (
            end -
            start
        ) *
        Math.Clamp(
            amount,
            0,
            1);

    private static IReadOnlySet<int>
        ResolveSignalizedJunctionNodeIds(
            MapStudioRoadGraph graph,
            IReadOnlyList<MapStudioOsmJunctionControl>
                controls)
    {
        var result =
            new HashSet<int>();

        foreach (
            var control in
                controls.Where(
                    item =>
                        item.Kind ==
                            MapStudioOsmJunctionControlKind
                                .TrafficSignal))
        {
            var junction =
                graph.Junctions
                    .Select(
                        item =>
                            (
                                Junction:
                                    item,
                                Distance:
                                    item.Position
                                        .DistanceTo(
                                            control.WorldPoint)
                            ))
                    .Where(
                        item =>
                            item.Distance <=
                                MaximumJunctionControlAssociationDistanceMeters)
                    .OrderBy(
                        item =>
                            item.Distance)
                    .ThenBy(
                        item =>
                            item.Junction.NodeId)
                    .Select(
                        item =>
                            item.Junction)
                    .FirstOrDefault();

            if (junction is not null)
            {
                result.Add(
                    junction.NodeId);
            }
        }

        return result;
    }

    private static IReadOnlyList<MapStudioOmsiJunctionTarget>
        BuildInstalledJunctionTargets(
            MapStudioRoadGraph graph,
            IReadOnlySet<int>?
                signalizedJunctionNodeIds = null)
    {
        var result =
            new List<MapStudioOmsiJunctionTarget>();

        foreach (var junction in graph.Junctions)
        {
            var connected =
                graph.Segments
                    .Where(
                        segment =>
                            segment.FromNodeId ==
                                junction.NodeId ||
                            segment.ToNodeId ==
                                junction.NodeId)
                    .Where(
                        segment =>
                        {
                            var profile =
                                ResolveProfile(
                                    segment.ProfileId);

                            return profile?.IsPedestrian !=
                                true;
                        })
                    .ToArray();

            if (
                connected.Length is < 3 or > 6 ||
                connected.Any(
                    segment =>
                    {
                        var structure =
                            ResolvePlacementStructure(
                                segment);

                        return
                            structure.Bridge ||
                            structure.Tunnel;
                    }))
            {
                continue;
            }

            var arms =
                connected
                    .Select(
                        segment =>
                        {
                            var other =
                                segment.FromNodeId ==
                                    junction.NodeId
                                    ? segment.End
                                    : segment.Start;

                            var profile =
                                ResolveProfile(
                                    segment.ProfileId);

                            return new MapStudioOmsiJunctionTargetArm(
                                segment.Id,
                                ResolveRotationDegrees(
                                    junction.Position,
                                    other),
                                Math.Max(
                                    segment.WidthMeters ??
                                        0,
                                    profile?.TotalWidthMeters ??
                                        7.0),
                                segment.LengthMeters);
                        })
                    .ToArray();

            result.Add(
                new MapStudioOmsiJunctionTarget(
                    junction.NodeId,
                    arms,
                    PreferFunctionalTrafficControl:
                        signalizedJunctionNodeIds
                            ?.Contains(
                                junction.NodeId) ==
                        true));
        }

        return result;
    }

    private static IReadOnlyList<MapStudioRoadGraphSegment>
        ApplyInstalledJunctionTrims(
            IReadOnlyList<MapStudioRoadGraphSegment> segments,
            IReadOnlyDictionary<int, MapStudioOmsiJunctionMatch> matches)
    {
        if (matches.Count == 0)
        {
            return segments;
        }

        var trims =
            new Dictionary<
                int,
                (double Start, double End)>();

        foreach (var match in matches.Values)
        {
            foreach (var pair in match.TrimDistanceBySegmentId)
            {
                var segment =
                    segments.FirstOrDefault(
                        segment =>
                            segment.Id ==
                                pair.Key);

                if (segment is null)
                {
                    continue;
                }

                var current =
                    trims.GetValueOrDefault(
                        segment.Id);

                if (segment.FromNodeId == match.NodeId)
                {
                    current.Start =
                        Math.Max(
                            current.Start,
                            pair.Value);
                }
                else if (segment.ToNodeId == match.NodeId)
                {
                    current.End =
                        Math.Max(
                            current.End,
                            pair.Value);
                }

                trims[segment.Id] =
                    current;
            }
        }

        return segments
            .Select(
                segment =>
                {
                    if (
                        !trims.TryGetValue(
                            segment.Id,
                            out var trim) ||
                        (
                            trim.Start <= 0.001 &&
                            trim.End <= 0.001
                        ))
                    {
                        return segment;
                    }

                    var originalLength =
                        segment.Start
                            .DistanceTo(
                                segment.End);

                    if (
                        originalLength <= 0.001 ||
                        trim.Start + trim.End >=
                            originalLength - 0.5)
                    {
                        return segment;
                    }

                    var start =
                        MapStudioRoadPoint.Lerp(
                            segment.Start,
                            segment.End,
                            trim.Start /
                                originalLength);

                    var end =
                        MapStudioRoadPoint.Lerp(
                            segment.Start,
                            segment.End,
                            1.0 -
                            trim.End /
                                originalLength);

                    return segment with
                    {
                        Start =
                            start,
                        End =
                            end,
                        LengthMeters =
                            start.DistanceTo(
                                end)
                    };
                })
            .ToArray();
    }

    private static MapStudioStandardRoadProfile?
        ResolveProfile(
            string relativePath) =>
        MapStudioStandardRoadCatalog
            .Profiles
            .FirstOrDefault(
                profile =>
                    string.Equals(
                        profile.RelativePath,
                        relativePath,
                        StringComparison.OrdinalIgnoreCase));

    private static double ResolveGradientPercent(
        double startHeight,
        double endHeight,
        double lengthMeters)
    {
        if (
            !double.IsFinite(startHeight) ||
            !double.IsFinite(endHeight) ||
            !double.IsFinite(lengthMeters) ||
            lengthMeters <= 0.001)
        {
            return 0;
        }

        var gradient =
            (
                endHeight -
                startHeight
            ) /
            lengthMeters *
            100.0;

        return double.IsFinite(gradient)
            ? gradient
            : 0;
    }

    private static void ValidateStraightSplineEndpoint(
        PlannedSpline placement)
    {
        var yaw =
            placement.Rotation *
            Math.PI /
            180.0;

        var worldStartX =
            OmsiTileGrid.GetOriginX(
                placement.Tile.X) +
            placement.LocalX;

        var worldStartZ =
            OmsiTileGrid.GetOriginZ(
                placement.Tile.Y) +
            placement.LocalZ;

        var calculatedEndX =
            worldStartX +
            Math.Sin(
                yaw) *
            placement.Segment
                .LengthMeters;

        var calculatedEndZ =
            worldStartZ +
            Math.Cos(
                yaw) *
            placement.Segment
                .LengthMeters;

        var dx =
            calculatedEndX -
            placement.Segment
                .End.X;

        var dz =
            calculatedEndZ -
            placement.Segment
                .End.Z;

        var error =
            Math.Sqrt(
                dx * dx +
                dz * dz);

        if (
            !double.IsFinite(
                error) ||
            error >
                MaximumStraightSplineEndpointErrorMeters)
        {
            throw new InvalidDataException(
                $"realWorldRoadSplineEndpointMismatch trace={placement.Segment.TraceId} segment={placement.Segment.Id} error={error:G17}");
        }
    }

    private static double ResolveRotationDegrees(
        MapStudioRoadPoint start,
        MapStudioRoadPoint end)
    {
        var degrees =
            Math.Atan2(
                end.X - start.X,
                end.Z - start.Z) *
            180.0 /
            Math.PI;

        return degrees < 0
            ? degrees + 360.0
            : degrees;
    }

    private sealed record TileState(
        int X,
        int Y,
        string Path,
        OmsiConfigDocument Document);

    private readonly record struct
        StructuralEndpointElevation(
            double StartHeight,
            double EndHeight);

    private sealed record PlannedSpline(
        MapStudioRoadGraphSegment Segment,
        TileState Tile,
        int SplineId,
        double LocalX,
        double LocalZ,
        double StartHeight,
        double EndHeight,
        double GradientPercent,
        double Rotation,
        string SplinePath);

    private sealed class TileWrite
    {
        public TileWrite(
            TileState tile,
            byte[] bytes)
        {
            Tile = tile;
            Bytes = bytes;
        }

        public TileState Tile { get; }
        public byte[] Bytes { get; }
        public string? BackupPath { get; set; }
        public string? TemporaryPath { get; set; }
    }
}
