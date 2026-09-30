using System.Globalization;
using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Terrain;
using MapStudio.Core.Omsi.Splines;

namespace MapStudio.Core.Omsi.Structures;

public sealed record MapStudioRoadStructureSpline(
    MapStudioRoadGraphSegment Segment,
    double StartHeightMeters,
    double EndHeightMeters);

public sealed record MapStudioBridgePierSceneryPlan(
    string Id,
    string AssetName,
    MapStudioBridgePierSpec Spec,
    MapStudioRoadPoint WorldCenter,
    double HeightMeters,
    double Rotation);

public sealed record MapStudioTunnelPortalSceneryPlan(
    string Id,
    string AssetName,
    MapStudioTunnelPortalSpec Spec,
    MapStudioRoadPoint WorldCenter,
    double HeightMeters,
    double Rotation,
    bool IsRunStart);

public sealed record MapStudioRoadStructureSceneryPlan(
    IReadOnlyList<MapStudioBridgePierSceneryPlan> BridgePiers,
    IReadOnlyList<MapStudioTunnelPortalSceneryPlan> TunnelPortals)
{
    public int PlacementCount =>
        BridgePiers.Count +
        TunnelPortals.Count;

    public int UniqueAssetCount =>
        BridgePiers
            .Select(item => item.AssetName)
            .Concat(
                TunnelPortals.Select(
                    item => item.AssetName))
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .Count();
}

public sealed class MapStudioRoadStructureSceneryPlanner
{
    private const double TargetSpanMeters =
        30.0;

    private const double MinimumRunLengthMeters =
        18.0;

    private const double MinimumEndInsetMeters =
        8.0;

    private const double MinimumPierHeightMeters =
        1.25;

    private const double DeckUndersideOffsetMeters =
        0.20;

    private const double DefaultRoadWidthMeters =
        7.0;

    private const double PortalClearSideMarginMeters =
        0.50;

    private const double TerrainProbeOutsideMeters =
        1.50;

    private const double MinimumWingHeightMeters =
        2.50;

    private const double MaximumWingHeightMeters =
        7.00;

    private const double TerrainWingFreeboardMeters =
        0.60;

    private const double WingHeightQuantumMeters =
        0.50;

    private const double TerrainLongitudinalProbeMeters =
        4.00;

    private const double MinimumWingDepthMeters =
        2.50;

    private const double MaximumWingDepthMeters =
        8.00;

    private const double WingDepthQuantumMeters =
        0.50;

    private const double TerrainDepthGain =
        0.80;

    public MapStudioRoadStructureSceneryPlan Build(
        IReadOnlyList<MapStudioRoadStructureSpline> splines,
        MapStudioGeographicAnchor anchor,
        MapStudioGeoreferencedElevationSurface? elevation)
    {
        ArgumentNullException.ThrowIfNull(splines);

        if (splines.Count == 0)
        {
            return new MapStudioRoadStructureSceneryPlan(
                Array.Empty<MapStudioBridgePierSceneryPlan>(),
                Array.Empty<MapStudioTunnelPortalSceneryPlan>());
        }

        var bridgePiers =
            new List<MapStudioBridgePierSceneryPlan>();

        var tunnelPortals =
            new List<MapStudioTunnelPortalSceneryPlan>();

        var runOrdinal =
            0;

        foreach (
            var traceGroup in
                splines
                    .GroupBy(
                        item =>
                            item.Segment.TraceId,
                        StringComparer.OrdinalIgnoreCase))
        {
            var ordered =
                traceGroup
                    .OrderBy(
                        item =>
                            item.Segment.Id)
                    .ToArray();

            for (
                var start = 0;
                start < ordered.Length;)
            {
                var structure =
                    ResolvePlacementStructure(
                        ordered[start].Segment);

                if (
                    !structure.Bridge &&
                    !structure.Tunnel)
                {
                    start++;

                    continue;
                }

                var end =
                    start;

                while (
                    end + 1 <
                        ordered.Length &&
                    ordered[end]
                        .Segment
                        .ToNodeId ==
                    ordered[end + 1]
                        .Segment
                        .FromNodeId &&
                    HasCompatibleGradeSeparation(
                        ordered[end].Segment,
                        ordered[end + 1].Segment))
                {
                    end++;
                }

                var run =
                    ordered[
                        start..(end + 1)];

                runOrdinal++;

                if (structure.Bridge)
                {
                    BuildBridgeRun(
                        run,
                        runOrdinal,
                        anchor,
                        elevation,
                        bridgePiers);
                }
                else
                {
                    BuildTunnelRun(
                        run,
                        runOrdinal,
                        anchor,
                        elevation,
                        tunnelPortals);
                }

                start =
                    end +
                    1;
            }
        }

        return new MapStudioRoadStructureSceneryPlan(
            bridgePiers,
            tunnelPortals);
    }

    private static void BuildBridgeRun(
        IReadOnlyList<MapStudioRoadStructureSpline> run,
        int runOrdinal,
        MapStudioGeographicAnchor anchor,
        MapStudioGeoreferencedElevationSurface? elevation,
        ICollection<MapStudioBridgePierSceneryPlan> output)
    {
        var totalLength =
            run.Sum(
                item =>
                    Math.Max(
                        0,
                        item.Segment.LengthMeters));

        if (
            totalLength <
            MinimumRunLengthMeters)
        {
            return;
        }

        var spanCount =
            Math.Max(
                1,
                (int)Math.Ceiling(
                    totalLength /
                    TargetSpanMeters));

        var supportOrdinal =
            0;

        for (
            var spanIndex = 1;
            spanIndex < spanCount;
            spanIndex++)
        {
            var distance =
                totalLength *
                spanIndex /
                spanCount;

            if (
                distance <
                    MinimumEndInsetMeters ||
                totalLength -
                    distance <
                    MinimumEndInsetMeters)
            {
                continue;
            }

            if (
                !TryResolveRunPoint(
                    run,
                    distance,
                    out var spline,
                    out var point,
                    out var deckHeight,
                    out var rotation))
            {
                continue;
            }

            var terrainHeight =
                SampleTerrainHeight(
                    elevation,
                    anchor,
                    point,
                    0);

            var pierHeight =
                deckHeight -
                DeckUndersideOffsetMeters -
                terrainHeight;

            if (
                !double.IsFinite(
                    pierHeight) ||
                pierHeight <
                    MinimumPierHeightMeters)
            {
                continue;
            }

            supportOrdinal++;

            var quantizedHeight =
                Math.Ceiling(
                    pierHeight *
                    4.0) /
                4.0;

            var capWidth =
                ResolveCapWidth(
                    spline.Segment);

            var assetName =
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"MS_BridgePier_H{(int)Math.Round(quantizedHeight * 100):0000}_W{(int)Math.Round(capWidth * 10):000}");

            output.Add(
                new MapStudioBridgePierSceneryPlan(
                    $"osm-bridge-pier-{runOrdinal}-{supportOrdinal}",
                    assetName,
                    new MapStudioBridgePierSpec(
                        assetName,
                        quantizedHeight,
                        1.8,
                        1.4,
                        capWidth),
                    point,
                    terrainHeight,
                    rotation));
        }
    }

    private static void BuildTunnelRun(
        IReadOnlyList<MapStudioRoadStructureSpline> run,
        int runOrdinal,
        MapStudioGeographicAnchor anchor,
        MapStudioGeoreferencedElevationSurface? elevation,
        ICollection<MapStudioTunnelPortalSceneryPlan> output)
    {
        if (run.Count == 0)
        {
            return;
        }

        var first =
            run[0];

        var last =
            run[^1];

        AddTunnelPortal(
            first,
            first.Segment.Start,
            ResolveRotationDegrees(
                    first.Segment.Start,
                    first.Segment.End) +
                180.0,
            true,
            runOrdinal,
            1,
            anchor,
            elevation,
            output);

        AddTunnelPortal(
            last,
            last.Segment.End,
            ResolveRotationDegrees(
                last.Segment.Start,
                last.Segment.End),
            false,
            runOrdinal,
            2,
            anchor,
            elevation,
            output);
    }

    private static void AddTunnelPortal(
        MapStudioRoadStructureSpline spline,
        MapStudioRoadPoint point,
        double rotation,
        bool isRunStart,
        int runOrdinal,
        int portalOrdinal,
        MapStudioGeographicAnchor anchor,
        MapStudioGeoreferencedElevationSurface? elevation,
        ICollection<MapStudioTunnelPortalSceneryPlan> output)
    {
        var roadWidth =
            ResolveRoadWidth(
                spline.Segment);

        var quantizedWidth =
            Math.Ceiling(
                roadWidth *
                2.0) /
            2.0;

        var terrainCenter =
            SampleTerrainHeight(
                elevation,
                anchor,
                point,
                0);

        var probeDistance =
            quantizedWidth /
                2.0 +
            PortalClearSideMarginMeters +
            TerrainProbeOutsideMeters;

        var yaw =
            NormalizeDegrees(
                rotation) *
            Math.PI /
            180.0;

        var lateralX =
            Math.Cos(yaw);

        var lateralZ =
            -Math.Sin(yaw);

        var leftPoint =
            new MapStudioRoadPoint(
                point.X -
                    lateralX *
                    probeDistance,
                point.Z -
                    lateralZ *
                    probeDistance);

        var rightPoint =
            new MapStudioRoadPoint(
                point.X +
                    lateralX *
                    probeDistance,
                point.Z +
                    lateralZ *
                    probeDistance);

        var leftTerrain =
            SampleTerrainHeight(
                elevation,
                anchor,
                leftPoint,
                terrainCenter);

        var rightTerrain =
            SampleTerrainHeight(
                elevation,
                anchor,
                rightPoint,
                terrainCenter);

        var leftWingHeight =
            ResolveWingHeight(
                terrainCenter,
                leftTerrain);

        var rightWingHeight =
            ResolveWingHeight(
                terrainCenter,
                rightTerrain);

        var outwardX =
            Math.Sin(yaw);

        var outwardZ =
            Math.Cos(yaw);

        var leftFarPoint =
            new MapStudioRoadPoint(
                leftPoint.X +
                    outwardX *
                    TerrainLongitudinalProbeMeters,
                leftPoint.Z +
                    outwardZ *
                    TerrainLongitudinalProbeMeters);

        var rightFarPoint =
            new MapStudioRoadPoint(
                rightPoint.X +
                    outwardX *
                    TerrainLongitudinalProbeMeters,
                rightPoint.Z +
                    outwardZ *
                    TerrainLongitudinalProbeMeters);

        var leftTerrainFar =
            SampleTerrainHeight(
                elevation,
                anchor,
                leftFarPoint,
                leftTerrain);

        var rightTerrainFar =
            SampleTerrainHeight(
                elevation,
                anchor,
                rightFarPoint,
                rightTerrain);

        var leftWingDepth =
            ResolveWingDepth(
                leftTerrain,
                leftTerrainFar);

        var rightWingDepth =
            ResolveWingDepth(
                rightTerrain,
                rightTerrainFar);

        var assetName =
            string.Create(
                CultureInfo.InvariantCulture,
                $"MS_TunnelPortal_W{(int)Math.Round(quantizedWidth * 10):000}_L{(int)Math.Round(leftWingHeight * 10):000}_R{(int)Math.Round(rightWingHeight * 10):000}_DL{(int)Math.Round(leftWingDepth * 10):000}_DR{(int)Math.Round(rightWingDepth * 10):000}");

        output.Add(
            new MapStudioTunnelPortalSceneryPlan(
                $"osm-tunnel-portal-{runOrdinal}-{portalOrdinal}",
                assetName,
                new MapStudioTunnelPortalSpec(
                    assetName,
                    quantizedWidth,
                    LeftWingHeightMeters:
                        leftWingHeight,
                    RightWingHeightMeters:
                        rightWingHeight,
                    LeftWingDepthMeters:
                        leftWingDepth,
                    RightWingDepthMeters:
                        rightWingDepth),
                point,
                terrainCenter,
                NormalizeDegrees(
                    rotation),
                isRunStart));
    }

    private static bool TryResolveRunPoint(
        IReadOnlyList<MapStudioRoadStructureSpline> run,
        double runDistance,
        out MapStudioRoadStructureSpline spline,
        out MapStudioRoadPoint point,
        out double height,
        out double rotation)
    {
        spline =
            run[0];

        point =
            run[0].Segment.Start;

        height =
            run[0].StartHeightMeters;

        rotation =
            0;

        var traveled =
            0.0;

        for (
            var index = 0;
            index < run.Count;
            index++)
        {
            var candidate =
                run[index];

            var length =
                Math.Max(
                    0,
                    candidate.Segment.LengthMeters);

            if (
                runDistance <=
                    traveled +
                    length ||
                index ==
                    run.Count -
                    1)
            {
                var localDistance =
                    Math.Clamp(
                        runDistance -
                            traveled,
                        0,
                        length);

                var amount =
                    length >
                        0.0001
                        ? localDistance /
                            length
                        : 0;

                spline =
                    candidate;

                point =
                    new MapStudioRoadPoint(
                        Lerp(
                            candidate.Segment.Start.X,
                            candidate.Segment.End.X,
                            amount),
                        Lerp(
                            candidate.Segment.Start.Z,
                            candidate.Segment.End.Z,
                            amount));

                height =
                    Lerp(
                        candidate.StartHeightMeters,
                        candidate.EndHeightMeters,
                        amount);

                rotation =
                    ResolveRotationDegrees(
                        candidate.Segment.Start,
                        candidate.Segment.End);

                return true;
            }

            traveled +=
                length;
        }

        return false;
    }

    private static bool HasCompatibleGradeSeparation(
        MapStudioRoadGraphSegment left,
        MapStudioRoadGraphSegment right) =>
        ResolvePlacementStructure(
            left) ==
        ResolvePlacementStructure(
            right) &&
        ResolveEffectiveStructuralLayer(
            left) ==
        ResolveEffectiveStructuralLayer(
            right);

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

        return structure.Bridge
            ? 1
            : structure.Tunnel
                ? -1
                : 0;
    }

    private static double ResolveRoadWidth(
        MapStudioRoadGraphSegment segment)
    {
        var profile =
            ResolveProfile(
                segment.ProfileId);

        return Math.Clamp(
            segment.WidthMeters is
                > 0
                ? segment.WidthMeters.Value
                : profile?.TotalWidthMeters ??
                    DefaultRoadWidthMeters,
            3.0,
            30.0);
    }

    private static double ResolveCapWidth(
        MapStudioRoadGraphSegment segment)
    {
        var target =
            Math.Clamp(
                ResolveRoadWidth(
                    segment) *
                0.90,
                4.0,
                24.0);

        return Math.Ceiling(
            target *
            2.0) /
            2.0;
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

    private static double ResolveWingHeight(
        double centerTerrain,
        double sideTerrain)
    {
        var required =
            MinimumWingHeightMeters +
            Math.Max(
                0.0,
                sideTerrain -
                    centerTerrain) +
            TerrainWingFreeboardMeters;

        var clamped =
            Math.Clamp(
                required,
                MinimumWingHeightMeters,
                MaximumWingHeightMeters);

        return Math.Ceiling(
                clamped /
                WingHeightQuantumMeters) *
            WingHeightQuantumMeters;
    }

    private static double ResolveWingDepth(
        double nearTerrain,
        double farTerrain)
    {
        var required =
            MinimumWingDepthMeters +
            Math.Max(
                0.0,
                farTerrain -
                    nearTerrain) *
            TerrainDepthGain;

        var clamped =
            Math.Clamp(
                required,
                MinimumWingDepthMeters,
                MaximumWingDepthMeters);

        return Math.Ceiling(
                clamped /
                WingDepthQuantumMeters) *
            WingDepthQuantumMeters;
    }

    private static double SampleTerrainHeight(
        MapStudioGeoreferencedElevationSurface?
            elevation,
        MapStudioGeographicAnchor anchor,
        MapStudioRoadPoint point,
        double fallback) =>
        elevation?.SampleRelativeHeightOrDefault(
            anchor,
            point,
            fallback) ??
        fallback;

    private static double ResolveRotationDegrees(
        MapStudioRoadPoint start,
        MapStudioRoadPoint end)
    {
        var degrees =
            Math.Atan2(
                end.X -
                    start.X,
                end.Z -
                    start.Z) *
            180.0 /
            Math.PI;

        return NormalizeDegrees(
            degrees);
    }

    private static double NormalizeDegrees(
        double value)
    {
        value %=
            360.0;

        if (value < 0)
        {
            value +=
                360.0;
        }

        return value;
    }

    private static double Lerp(
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
}
