using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Omsi.Junctions;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Renderer.Scene;

namespace MapStudio.Renderer.Viewport;

public sealed record NativeProceduralJunctionPlanItem(
    string AssetName,
    MapStudioJunctionSpec Spec,
    OmsiTileReference Tile,
    double X,
    double Y,
    double Rotation,
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
                                $"{arm.AngleDegrees:0.0}:{arm.WidthMeters:0.0}:{arm.LaneCount}:{arm.OneWay}"));

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
                    new Vector3(
                        (float)junction.Position.X,
                        (float)height,
                        (float)junction.Position.Z)));
        }

        return new NativeProceduralJunctionPlan(
            items,
            skipped);
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

        var lanes =
            Math.Clamp(
                segment.LaneCount ??
                2,
                1,
                8);

        var width =
            segment.WidthMeters ??
            lanes *
            3.5;

        return new MapStudioJunctionArm(
            angle,
            width,
            lanes,
            segment.OneWay ??
                false);
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
                                    $"{arm.AngleDegrees:000.0}:{arm.WidthMeters:00.0}:{arm.LaneCount}:{(arm.OneWay ? 1 : 0)}")));

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
