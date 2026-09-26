using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Omsi.Junctions;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Splines;
using MapStudio.Renderer.Scene;

namespace MapStudio.Renderer.Viewport;

public sealed record NativeProceduralJunctionPlanItem(
    string AssetName,
    MapStudioJunctionSpec Spec,
    OmsiTileReference Tile,
    double X,
    double Y,
    double Rotation,
    double Pitch,
    double Bank,
    Vector3 WorldPoint);

public sealed record NativeProceduralJunctionPlan(
    IReadOnlyList<NativeProceduralJunctionPlanItem> Items,
    int SkippedJunctions)
{
    public int UniqueAssetCount =>
        Items
            .Select(
                item =>
                    item.AssetName)
            .Distinct(
                StringComparer
                    .OrdinalIgnoreCase)
            .Count();
}

public sealed class NativeProceduralJunctionPlanBuilder
{
    private const double
        MaximumTerrainTiltDegrees =
            12.0;

    private const double
        StructuralLayerSeparationMeters =
            4.8;

    public NativeProceduralJunctionPlan Build(
        NativeSceneSnapshot scene,
        MapStudioRoadGraph graph)
    {
        ArgumentNullException.ThrowIfNull(
            scene);

        ArgumentNullException.ThrowIfNull(
            graph);

        var items =
            new List<
                NativeProceduralJunctionPlanItem>();

        var skipped =
            0;

        var segmentsByNode =
            graph.Segments
                .SelectMany(
                    segment =>
                        new[]
                        {
                            (
                                NodeId:
                                    segment
                                        .FromNodeId,
                                Segment:
                                    segment,
                                AtStart:
                                    true
                            ),
                            (
                                NodeId:
                                    segment
                                        .ToNodeId,
                                Segment:
                                    segment,
                                AtStart:
                                    false
                            )
                        })
                .GroupBy(
                    item =>
                        item.NodeId)
                .ToDictionary(
                    group =>
                        group.Key,
                    group =>
                        group.ToArray());

        foreach (
            var junction in
                graph.Junctions)
        {
            if (
                !segmentsByNode
                    .TryGetValue(
                        junction.NodeId,
                        out var incident) ||
                incident.Length <
                    3 ||
                !NativeTerrainSampler
                    .TryGetHeightAtWorldPoint(
                        scene,
                        junction.Position.X,
                        junction.Position.Z,
                        out var height))
            {
                skipped++;
                continue;
            }

            var incidentSegments =
                incident
                    .Select(
                        item =>
                            item.Segment)
                    .ToArray();

            height +=
                ResolveStructuralJunctionHeightOffset(
                    incidentSegments);

            var actualArms =
                incident
                    .Select(
                        item =>
                        BuildArm(
                            item.Segment,
                            item.AtStart))
                    .ToArray();

            var canonical =
                Canonicalize(
                    actualArms);

            var allowTerrainTilt =
                incident.All(
                    item =>
                        !item.Segment.Bridge &&
                        !item.Segment.Tunnel &&
                        (item.Segment.Layer ?? 0) ==
                            0);

            var (
                pitch,
                bank
            ) =
                allowTerrainTilt
                    ? ResolveTerrainTilt(
                        scene,
                        junction.Position.X,
                        junction.Position.Z,
                        canonical.RotationDegrees,
                        actualArms)
                    : (0.0, 0.0);

            var tileX =
                (int)Math.Floor(
                    junction.Position.X /
                    300.0);

            var tileY =
                (int)Math.Floor(
                    junction.Position.Z /
                    300.0);

            var tile =
                scene.Tiles
                    .FirstOrDefault(
                        current =>
                            current.Reference.X ==
                                tileX &&
                            current.Reference.Y ==
                                tileY);

            if (tile is null)
            {
                skipped++;
                continue;
            }

            var signature =
                string.Join(
                    ";",
                    canonical.Arms
                        .Select(
                            arm =>
                                $"{arm.AngleDegrees:0.0}:{arm.WidthMeters:0.0}:{arm.LaneCount}:{arm.LaneWidthMeters:0.00}:{arm.InboundLaneCount}:{arm.OutboundLaneCount}:{arm.OneWay}"));

            var hash =
                Convert.ToHexString(
                    SHA256.HashData(
                        Encoding.UTF8
                            .GetBytes(
                                signature)))
                    [..12]
                    .ToLowerInvariant();

            var assetName =
                "MSJ_" +
                canonical.Arms.Count +
                "_" +
                hash;

            var spec =
                new MapStudioJunctionSpec(
                    assetName,
                    canonical.Arms);

            items.Add(
                new NativeProceduralJunctionPlanItem(
                    assetName,
                    spec,
                    tile.Reference,
                    junction.Position.X -
                        tileX *
                        300.0,
                    junction.Position.Z -
                        tileY *
                        300.0,
                    canonical.RotationDegrees,
                    pitch,
                    bank,
                    new Vector3(
                        (float)junction.Position.X,
                        (float)height,
                        (float)junction.Position.Z)));
        }

        return new NativeProceduralJunctionPlan(
            items,
            skipped);
    }


    private static double
        ResolveStructuralJunctionHeightOffset(
            IReadOnlyList<
                MapStudioRoadGraphSegment>
                segments)
    {
        if (
            segments.Count <
            3)
        {
            return 0;
        }

        var first =
            segments[0];

        if (
            !IsStructuralJunctionSegment(
                first) ||
            segments.Any(
                segment =>
                    !IsStructuralJunctionSegment(
                        segment) ||
                    !HasCompatibleStructuralGrade(
                        first,
                        segment)))
        {
            return 0;
        }

        var layer =
            first.Layer ??
            0;

        if (layer == 0)
        {
            return 0;
        }

        var direction =
            first.Bridge &&
            !first.Tunnel
                ? 1
                : first.Tunnel &&
                    !first.Bridge
                    ? -1
                    : Math.Sign(
                        layer);

        return direction *
            Math.Abs(
                layer) *
            StructuralLayerSeparationMeters;
    }

    private static bool
        IsStructuralJunctionSegment(
            MapStudioRoadGraphSegment segment) =>
        segment.Bridge ||
        segment.Tunnel ||
        (segment.Layer ?? 0) !=
            0;

    private static bool
        HasCompatibleStructuralGrade(
            MapStudioRoadGraphSegment left,
            MapStudioRoadGraphSegment right) =>
        left.Bridge ==
            right.Bridge &&
        left.Tunnel ==
            right.Tunnel &&
        (left.Layer ?? 0) ==
            (right.Layer ?? 0);

    private static (
        double Pitch,
        double Bank
    ) ResolveTerrainTilt(
        NativeSceneSnapshot scene,
        double centerX,
        double centerZ,
        double rotationDegrees,
        IReadOnlyList<
            MapStudioJunctionArm>
            arms)
    {
        if (arms.Count < 3)
        {
            return (0, 0);
        }

        var extent =
            MapStudioJunctionGeometrySizing
                .ResolveSurfaceExtentMeters(
                    arms.Select(
                        arm =>
                            arm.WidthMeters));

        var sampleDistance =
            Math.Clamp(
                extent *
                    0.70,
                2.0,
                8.0);

        var radians =
            rotationDegrees *
            Math.PI /
            180.0;

        var forwardX =
            Math.Sin(
                radians);

        var forwardZ =
            Math.Cos(
                radians);

        var rightX =
            Math.Cos(
                radians);

        var rightZ =
            -Math.Sin(
                radians);

        if (
            !NativeTerrainSampler
                .TryGetHeightAtWorldPoint(
                    scene,
                    centerX +
                        forwardX *
                        sampleDistance,
                    centerZ +
                        forwardZ *
                        sampleDistance,
                    out var forwardHeight) ||
            !NativeTerrainSampler
                .TryGetHeightAtWorldPoint(
                    scene,
                    centerX -
                        forwardX *
                        sampleDistance,
                    centerZ -
                        forwardZ *
                        sampleDistance,
                    out var backHeight) ||
            !NativeTerrainSampler
                .TryGetHeightAtWorldPoint(
                    scene,
                    centerX +
                        rightX *
                        sampleDistance,
                    centerZ +
                        rightZ *
                        sampleDistance,
                    out var rightHeight) ||
            !NativeTerrainSampler
                .TryGetHeightAtWorldPoint(
                    scene,
                    centerX -
                        rightX *
                        sampleDistance,
                    centerZ -
                        rightZ *
                        sampleDistance,
                    out var leftHeight))
        {
            return (0, 0);
        }

        var forwardSlope =
            (
                forwardHeight -
                backHeight
            ) /
            (
                sampleDistance *
                2.0
            );

        var rightSlope =
            (
                rightHeight -
                leftHeight
            ) /
            (
                sampleDistance *
                2.0
            );

        var pitch =
            -Math.Atan(
                forwardSlope) *
            180.0 /
            Math.PI;

        var bank =
            Math.Atan(
                rightSlope) *
            180.0 /
            Math.PI;

        return
            (
                Math.Clamp(
                    pitch,
                    -MaximumTerrainTiltDegrees,
                    MaximumTerrainTiltDegrees),
                Math.Clamp(
                    bank,
                    -MaximumTerrainTiltDegrees,
                    MaximumTerrainTiltDegrees)
            );
    }

    private static MapStudioJunctionArm BuildArm(
        MapStudioRoadGraphSegment segment,
        bool atStart)
    {
        var origin =
            atStart
                ? segment.Start
                : segment.End;

        var target =
            atStart
                ? segment.End
                : segment.Start;

        var dx =
            target.X -
            origin.X;

        var dz =
            target.Z -
            origin.Z;

        var angle =
            Math.Atan2(
                dx,
                dz) *
            180.0 /
            Math.PI;

        if (angle < 0)
        {
            angle +=
                360.0;
        }

        var profile =
            MapStudioStandardRoadCatalog
                .Profiles
                .FirstOrDefault(
                    candidate =>
                        string.Equals(
                            candidate.RelativePath,
                            segment.ProfileId,
                            StringComparison
                                .OrdinalIgnoreCase));

        var lanes =
            Math.Clamp(
                profile?.LaneCount ??
                segment.LaneCount ??
                2,
                1,
                8);

        var physicalWidth =
            profile?.TotalWidthMeters ??
            segment.WidthMeters ??
            lanes *
            3.5;

        var laneWidth =
            profile?.LaneWidthMeters ??
            Math.Clamp(
                (
                    segment.WidthMeters ??
                    lanes *
                    3.5
                ) /
                lanes,
                2.0,
                4.5);

        var oneWay =
            profile?.OneWay ??
            segment.OneWay ??
                false;

        var (
            forwardLanes,
            backwardLanes
        ) =
            ResolveDirectionalLanes(
                lanes,
                oneWay,
                segment.ForwardLaneCount,
                segment.BackwardLaneCount);

        var inboundLanes =
            atStart
                ? backwardLanes
                : forwardLanes;

        var outboundLanes =
            atStart
                ? forwardLanes
                : backwardLanes;

        return new MapStudioJunctionArm(
            angle,
            physicalWidth,
            lanes,
            oneWay,
            laneWidth,
            inboundLanes,
            outboundLanes);
    }

    private static (
        int Forward,
        int Backward
    ) ResolveDirectionalLanes(
        int visualLaneCount,
        bool oneWay,
        int? sourceForward,
        int? sourceBackward)
    {
        if (oneWay)
        {
            return
                (
                    visualLaneCount,
                    0
                );
        }

        if (
            visualLaneCount <=
                1)
        {
            return
                (
                    1,
                    1
                );
        }

        var sourceTotal =
            (
                sourceForward ??
                0
            ) +
            (
                sourceBackward ??
                0
            );

        if (sourceTotal > 0)
        {
            var forward =
                (int)Math.Round(
                    visualLaneCount *
                    (
                        sourceForward ??
                        0
                    ) /
                    (double)sourceTotal,
                    MidpointRounding
                        .AwayFromZero);

            forward =
                Math.Clamp(
                    forward,
                    1,
                    visualLaneCount -
                        1);

            return
                (
                    forward,
                    visualLaneCount -
                        forward
                );
        }

        var backward =
            visualLaneCount /
            2;

        return
            (
                visualLaneCount -
                    backward,
                backward
            );
    }

    private static CanonicalJunction
        Canonicalize(
            IReadOnlyList<
                MapStudioJunctionArm>
                actualArms)
    {
        CanonicalJunction?
            best =
                null;

        string? bestSignature =
            null;

        foreach (
            var baseArm in
                actualArms)
        {
            var normalized =
                actualArms
                    .Select(
                        arm =>
                            arm with
                            {
                                AngleDegrees =
                                    NormalizeAngle(
                                        arm.AngleDegrees -
                                        baseArm.AngleDegrees)
                            })
                    .OrderBy(
                        arm =>
                            arm.AngleDegrees)
                    .ToArray();

            var signature =
                string.Join(
                    ";",
                    normalized
                        .Select(
                            arm =>
                                string.Create(
                                    CultureInfo
                                        .InvariantCulture,
                                    $"{arm.AngleDegrees:000.0}:{arm.WidthMeters:00.0}:{arm.LaneCount}:{arm.LaneWidthMeters:0.00}:{arm.InboundLaneCount}:{arm.OutboundLaneCount}:{(arm.OneWay ? 1 : 0)}")));

            if (
                bestSignature is null ||
                string.CompareOrdinal(
                    signature,
                    bestSignature) <
                0)
            {
                bestSignature =
                    signature;

                best =
                    new CanonicalJunction(
                        normalized,
                        baseArm.AngleDegrees);
            }
        }

        return best ??
            throw new InvalidDataException(
                "junctionCanonicalizationFailed");
    }

    private static double NormalizeAngle(
        double angle)
    {
        angle %=
            360.0;

        if (angle < 0)
        {
            angle +=
                360.0;
        }

        return angle;
    }

    private sealed record CanonicalJunction(
        IReadOnlyList<MapStudioJunctionArm> Arms,
        double RotationDegrees);
}
