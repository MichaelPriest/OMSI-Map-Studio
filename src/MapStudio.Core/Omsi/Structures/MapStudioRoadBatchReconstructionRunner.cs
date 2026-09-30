using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Terrain;
using MapStudio.Core.Omsi.Config;
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
    IReadOnlyList<MapStudioGeneratedSceneryPlacement>? StructurePlacements = null)
{
    public int PlacedSplineCount =>
        Placements.Count;

    public int GeneratedJunctionCount =>
        JunctionPlacements.Count;

    public int GeneratedStructureCount =>
        StructurePlacements?.Count ??
        0;

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

    public async Task<MapStudioRoadBatchReconstructionResult>
        RunAsync(
            string omsiRoot,
            string mapDirectory,
            string osmXml,
            MapStudioGeographicAnchor anchor,
            CancellationToken cancellationToken = default,
            MapStudioGeoreferencedElevationSurface? elevation = null)
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
                            profile.RelativePath,
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

        foreach (var segment in graph.Segments)
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

            planned.Add(
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
                    splinePath));
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
                                        placement.LocalZ,
                                        placement.StartHeight,
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
                        asset.SceneryObjectPath,
                        junction.Position,
                        HeightMeters:
                            junctionHeight -
                            junctionTerrainHeight));
            }

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
                .ToArray());
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
