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
    MapStudioRoadKitInstallResult RoadKit)
{
    public int PlacedSplineCount =>
        Placements.Count;

    public int GeneratedJunctionCount =>
        JunctionPlacements.Count;

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
            .Distinct()
            .Count();
}

public sealed class MapStudioRoadBatchReconstructionRunner
{
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
                .ToArray();

        var graph =
            new MapStudioRoadGraphBuilder()
                .Build(
                    traces);

        var roadKit =
            await new MapStudioRoadKitGenerator()
                .InstallOrUpdateAsync(
                    root,
                    cancellationToken)
                .ConfigureAwait(false);

        var descriptor =
            await OmsiMapCatalog
                .OpenMapAsync(
                    mapRoot,
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

            var splinePath =
                MapStudioStandardRoadCatalog
                    .ResolvePlacementRelativePath(
                        segment.ProfileId,
                        segment.Bridge,
                        segment.Tunnel);

            var startHeight =
                elevation?.SampleRelativeHeightOrDefault(
                    anchor,
                    segment.Start) ??
                0;

            var endHeight =
                elevation?.SampleRelativeHeightOrDefault(
                    anchor,
                    segment.End,
                    startHeight) ??
                startHeight;

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
                    gradientPercent,
                    rotation,
                    splinePath));
        }

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
            junctionWrite =
                new(
                    Array.Empty<MapStudioGeneratedSceneryPlacement>(),
                    Array.Empty<string>(),
                    Array.Empty<string>());

        try
        {
            var junctionRequests =
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
                            item.Segment.Bridge)
                        ? MapStudioJunctionStructureKind.Bridge
                        : connected.All(
                            item =>
                                item.Segment.Tunnel)
                            ? MapStudioJunctionStructureKind.Tunnel
                            : MapStudioJunctionStructureKind.Ground;

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

                junctionRequests.Add(
                    new MapStudioGeneratedSceneryPlacementRequest(
                        "osm-junction-" +
                            junction.NodeId,
                        asset.SceneryObjectPath,
                        junction.Position,
                        HeightMeters:
                            elevation?.SampleRelativeHeightOrDefault(
                                anchor,
                                junction.Position) ?? 0));
            }

            if (junctionRequests.Count > 0)
            {
                junctionWrite =
                    await new MapStudioGeneratedSceneryBatchWriter()
                        .WriteAsync(
                            root,
                            mapRoot,
                            junctionRequests,
                            cancellationToken)
                        .ConfigureAwait(false);

                backupPaths.AddRange(
                    junctionWrite.BackupPaths);
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
                            placement.Segment.Bridge,
                            placement.Segment.Tunnel))
                .ToArray(),
            junctionWrite.Placements,
            graph.Junctions.Count,
            skippedOutside,
            imported.IgnoredWayCount,
            imported.MissingNodeReferenceCount,
            backupPaths,
            roadKit);
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

    private sealed record PlannedSpline(
        MapStudioRoadGraphSegment Segment,
        TileState Tile,
        int SplineId,
        double LocalX,
        double LocalZ,
        double StartHeight,
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
