using System.Globalization;
using System.Numerics;
using MapStudio.Core.Omsi.Splines;
using MapStudio.Core.Omsi.Structures;
using MapStudio.Renderer.Scene;

namespace MapStudio.Renderer.Viewport;

public sealed record NativeBridgePierPlanItem(
    string AssetName,
    MapStudioBridgePierSpec Spec,
    MapStudio.Core.Omsi.Maps.OmsiTileReference Tile,
    double X,
    double Y,
    double Rotation,
    Vector3 WorldPoint);

public sealed record NativeBridgePierPlan(
    IReadOnlyList<NativeBridgePierPlanItem> Items,
    int SkippedSupportCount)
{
    public int UniqueAssetCount =>
        Items.Select(
                item =>
                    item.AssetName)
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .Count();
}

public sealed class NativeBridgePierPlanBuilder
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

    public NativeBridgePierPlan Build(
        NativeSceneSnapshot scene,
        NativeProceduralRoadPlacementBuildResult
            placement)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(
            placement);

        var bridgeIndices =
            placement.Requests
                .Select(
                    (request, index) =>
                        (
                            Request: request,
                            Index: index
                        ))
                .Where(
                    item =>
                        IsBridgeRequest(
                            item.Request))
                .Select(
                    item =>
                        item.Index)
                .ToHashSet();

        if (bridgeIndices.Count == 0)
        {
            return new NativeBridgePierPlan(
                [],
                0);
        }

        var next =
            new Dictionary<int, int>();
        var previous =
            new Dictionary<int, int>();

        foreach (
            var link in
                placement.Links)
        {
            if (
                !bridgeIndices.Contains(
                    link.PreviousRequestIndex) ||
                !bridgeIndices.Contains(
                    link.NextRequestIndex))
            {
                continue;
            }

            var left =
                placement.Requests[
                    link.PreviousRequestIndex];
            var right =
                placement.Requests[
                    link.NextRequestIndex];

            if (
                !CanShareStructuralRun(
                    left,
                    right))
            {
                continue;
            }

            next[
                link.PreviousRequestIndex] =
                link.NextRequestIndex;
            previous[
                link.NextRequestIndex] =
                link.PreviousRequestIndex;
        }

        var items =
            new List<
                NativeBridgePierPlanItem>();
        var skipped =
            0;
        var visited =
            new HashSet<int>();

        foreach (
            var start in
                bridgeIndices
                    .Where(
                        index =>
                            !previous
                                .ContainsKey(
                                    index))
                    .OrderBy(
                        index =>
                            index))
        {
            BuildRun(start);
        }

        foreach (
            var remaining in
                bridgeIndices
                    .Where(
                        index =>
                            !visited.Contains(
                                index))
                    .OrderBy(
                        index =>
                            index))
        {
            BuildRun(remaining);
        }

        return new NativeBridgePierPlan(
            items,
            skipped);

        void BuildRun(
            int startIndex)
        {
            var run =
                new List<int>();
            var current =
                startIndex;

            while (
                bridgeIndices.Contains(
                    current) &&
                visited.Add(
                    current))
            {
                run.Add(current);

                if (
                    !next.TryGetValue(
                        current,
                        out var nextIndex))
                {
                    break;
                }

                current =
                    nextIndex;
            }

            if (run.Count == 0)
            {
                return;
            }

            var totalLength =
                run.Sum(
                    index =>
                        Math.Max(
                            0,
                            placement.Requests[
                                index]
                                .Length));

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
                        out var request,
                        out var center,
                        out var rotation))
                {
                    skipped++;
                    continue;
                }

                if (
                    !NativeTerrainSampler
                        .TryGetHeightAtWorldPoint(
                            scene,
                            center.X,
                            center.Z,
                            out var terrainHeight))
                {
                    skipped++;
                    continue;
                }

                var pierHeight =
                    center.Y -
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

                var tileX =
                    (int)Math.Floor(
                        center.X / 300.0f);
                var tileY =
                    (int)Math.Floor(
                        center.Z / 300.0f);

                var tile =
                    scene.Tiles
                        .FirstOrDefault(
                            candidate =>
                                candidate.Reference.X ==
                                    tileX &&
                                candidate.Reference.Y ==
                                    tileY);

                if (tile is null)
                {
                    skipped++;
                    continue;
                }

                var quantizedHeight =
                    Math.Ceiling(
                        pierHeight * 4.0) /
                    4.0;
                var capWidth =
                    ResolveCapWidth(
                        request);
                var assetName =
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"MS_BridgePier_H{(int)Math.Round(quantizedHeight * 100):0000}_W{(int)Math.Round(capWidth * 10):000}");

                items.Add(
                    new NativeBridgePierPlanItem(
                        assetName,
                        new MapStudioBridgePierSpec(
                            assetName,
                            quantizedHeight,
                            1.8,
                            1.4,
                            capWidth),
                        tile.Reference,
                        center.X -
                            tileX * 300.0,
                        center.Z -
                            tileY * 300.0,
                        rotation,
                        new Vector3(
                            center.X,
                            (float)terrainHeight,
                            center.Z)));
            }
        }

        bool TryResolveRunPoint(
            IReadOnlyList<int> run,
            double runDistance,
            out NativeSplinePlacementRequest
                request,
            out Vector3 center,
            out double rotation)
        {
            request =
                placement.Requests[
                    run[0]];
            center =
                default;
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
                    placement.Requests[
                        run[index]];
                var length =
                    Math.Max(
                        0,
                        candidate.Length);

                if (
                    runDistance <=
                        traveled + length ||
                    index ==
                        run.Count - 1)
                {
                    request =
                        candidate;
                    (
                        center,
                        rotation
                    ) =
                        ResolveFrame(
                            candidate,
                            Math.Clamp(
                                runDistance -
                                    traveled,
                                0,
                                length));

                    return true;
                }

                traveled +=
                    length;
            }

            return false;
        }
    }

    private static bool IsBridgeRequest(
        NativeSplinePlacementRequest request)
    {
        if (
            request.SourceBridge !=
            request.SourceTunnel)
        {
            return request.SourceBridge;
        }

        return (
            request.SourceLayer ??
            0) > 0;
    }

    private static bool CanShareStructuralRun(
        NativeSplinePlacementRequest left,
        NativeSplinePlacementRequest right) =>
        IsBridgeRequest(left) &&
        IsBridgeRequest(right) &&
        left.SourceBridge ==
            right.SourceBridge &&
        left.SourceTunnel ==
            right.SourceTunnel &&
        (left.SourceLayer ?? 0) ==
            (right.SourceLayer ?? 0);

    private static (
        Vector3 Center,
        double Rotation
    ) ResolveFrame(
        NativeSplinePlacementRequest request,
        double distance)
    {
        var clamped =
            Math.Clamp(
                distance,
                0.0,
                Math.Max(
                    0,
                    request.Length));
        var yaw =
            request.Rotation *
            Math.PI / 180.0;
        var hasCurve =
            Math.Abs(
                request.Radius) > 0.001;
        var curveAngle =
            hasCurve
                ? clamped /
                  request.Radius
                : 0.0;
        var localX =
            hasCurve
                ? request.Radius *
                  (
                      1.0 -
                      Math.Cos(
                          curveAngle)
                  )
                : 0.0;
        var localZ =
            hasCurve
                ? request.Radius *
                  Math.Sin(
                      curveAngle)
                : clamped;
        var cosYaw =
            Math.Cos(yaw);
        var sinYaw =
            Math.Sin(yaw);
        var worldX =
            request.StartWorld.X +
            localX * cosYaw +
            localZ * sinYaw;
        var worldZ =
            request.StartWorld.Z -
            localX * sinYaw +
            localZ * cosYaw;
        var amount =
            request.Length > 0.0001
                ? clamped /
                  request.Length
                : 0.0;
        var worldY =
            request.StartWorld.Y +
            (
                request.EndWorld.Y -
                request.StartWorld.Y
            ) *
            (float)Math.Clamp(
                amount,
                0,
                1);

        return
            (
                new Vector3(
                    (float)worldX,
                    worldY,
                    (float)worldZ),
                NormalizeDegrees(
                    (
                        yaw +
                        curveAngle
                    ) *
                    180.0 /
                    Math.PI)
            );
    }

    private static double ResolveCapWidth(
        NativeSplinePlacementRequest request)
    {
        var fileName =
            Path.GetFileNameWithoutExtension(
                request.SplinePath);

        if (
            fileName.EndsWith(
                "_bridge",
                StringComparison.OrdinalIgnoreCase))
        {
            fileName =
                fileName[
                    ..^"_bridge".Length];
        }
        else if (
            fileName.EndsWith(
                "_tunnel",
                StringComparison.OrdinalIgnoreCase))
        {
            fileName =
                fileName[
                    ..^"_tunnel".Length];
        }

        var profile =
            MapStudioStandardRoadCatalog
                .Profiles
                .FirstOrDefault(
                    candidate =>
                        string.Equals(
                            Path.GetFileNameWithoutExtension(
                                candidate.RelativePath),
                            fileName,
                            StringComparison.OrdinalIgnoreCase));

        var target =
            Math.Clamp(
                (
                    profile?.TotalWidthMeters ??
                    7.0
                ) *
                0.90,
                4.0,
                24.0);

        return Math.Ceiling(
            target * 2.0) /
            2.0;
    }

    private static double NormalizeDegrees(
        double value)
    {
        value %= 360.0;

        if (value < 0)
        {
            value += 360.0;
        }

        return value;
    }
}
