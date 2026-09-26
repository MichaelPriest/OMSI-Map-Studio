using System.Globalization;
using System.Numerics;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Splines;
using MapStudio.Core.Omsi.Structures;

namespace MapStudio.Renderer.Viewport;

public sealed record NativeTunnelPortalPlanItem(
    string AssetName,
    MapStudioTunnelPortalSpec Spec,
    OmsiTileReference Tile,
    double X,
    double Y,
    double Rotation,
    Vector3 WorldPoint,
    bool IsRunStart);

public sealed record NativeTunnelPortalPlan(
    IReadOnlyList<NativeTunnelPortalPlanItem> Items,
    int SkippedPortalCount)
{
    public int UniqueAssetCount =>
        Items.Select(item => item.AssetName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
}

public sealed class NativeTunnelPortalPlanBuilder
{
    private const double DefaultRoadWidthMeters = 7.0;

    public NativeTunnelPortalPlan Build(
        NativeSceneSnapshot scene,
        NativeProceduralRoadPlacementBuildResult placement)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(placement);

        var tunnelIndices = placement.Requests
            .Select((request, index) => (Request: request, Index: index))
            .Where(item => IsTunnelRequest(item.Request))
            .Select(item => item.Index)
            .ToHashSet();

        if (tunnelIndices.Count == 0)
        {
            return new NativeTunnelPortalPlan([], 0);
        }

        var next = new Dictionary<int, int>();
        var previous = new Dictionary<int, int>();

        foreach (var link in placement.Links)
        {
            if (!tunnelIndices.Contains(link.PreviousRequestIndex) ||
                !tunnelIndices.Contains(link.NextRequestIndex))
            {
                continue;
            }

            var left = placement.Requests[link.PreviousRequestIndex];
            var right = placement.Requests[link.NextRequestIndex];

            if (!CanShareStructuralRun(left, right))
            {
                continue;
            }

            next[link.PreviousRequestIndex] = link.NextRequestIndex;
            previous[link.NextRequestIndex] = link.PreviousRequestIndex;
        }

        var items = new List<NativeTunnelPortalPlanItem>();
        var skipped = 0;
        var visited = new HashSet<int>();

        foreach (var start in tunnelIndices
            .Where(index => !previous.ContainsKey(index))
            .OrderBy(index => index))
        {
            BuildRun(start);
        }

        foreach (var remaining in tunnelIndices
            .Where(index => !visited.Contains(index))
            .OrderBy(index => index))
        {
            BuildRun(remaining);
        }

        return new NativeTunnelPortalPlan(items, skipped);

        void BuildRun(int startIndex)
        {
            var run = new List<int>();
            var current = startIndex;

            while (tunnelIndices.Contains(current) && visited.Add(current))
            {
                run.Add(current);

                if (!next.TryGetValue(current, out var nextIndex))
                {
                    break;
                }

                current = nextIndex;
            }

            if (run.Count == 0)
            {
                return;
            }

            var first = placement.Requests[run[0]];
            var last = placement.Requests[run[^1]];

            AddPortal(first, first.StartWorld, first.Rotation + 180.0, true);
            AddPortal(
                last,
                last.EndWorld,
                ResolveEndRotation(last),
                false);
        }

        void AddPortal(
            NativeSplinePlacementRequest request,
            Vector3 worldPoint,
            double rotation,
            bool isRunStart)
        {
            var tileX = (int)Math.Floor(worldPoint.X / 300.0f);
            var tileY = (int)Math.Floor(worldPoint.Z / 300.0f);

            var tile = scene.Tiles.FirstOrDefault(candidate =>
                candidate.Reference.X == tileX &&
                candidate.Reference.Y == tileY);

            if (tile is null)
            {
                skipped++;
                return;
            }

            var roadWidth = ResolveRoadWidth(request);
            var quantizedWidth = Math.Ceiling(roadWidth * 2.0) / 2.0;
            var assetName = string.Create(
                CultureInfo.InvariantCulture,
                $"MS_TunnelPortal_W{(int)Math.Round(quantizedWidth * 10):000}");

            items.Add(
                new NativeTunnelPortalPlanItem(
                    assetName,
                    new MapStudioTunnelPortalSpec(
                        assetName,
                        quantizedWidth),
                    tile.Reference,
                    worldPoint.X - tileX * 300.0,
                    worldPoint.Z - tileY * 300.0,
                    NormalizeDegrees(rotation),
                    worldPoint,
                    isRunStart));
        }
    }

    private static bool IsTunnelRequest(
        NativeSplinePlacementRequest request)
    {
        if (request.SourceBridge != request.SourceTunnel)
        {
            return request.SourceTunnel;
        }

        return (request.SourceLayer ?? 0) < 0;
    }

    private static bool CanShareStructuralRun(
        NativeSplinePlacementRequest left,
        NativeSplinePlacementRequest right) =>
        IsTunnelRequest(left) &&
        IsTunnelRequest(right) &&
        left.SourceBridge == right.SourceBridge &&
        left.SourceTunnel == right.SourceTunnel &&
        (left.SourceLayer ?? 0) == (right.SourceLayer ?? 0);

    private static double ResolveRoadWidth(
        NativeSplinePlacementRequest request)
    {
        var fileName = Path.GetFileNameWithoutExtension(request.SplinePath);

        if (fileName.EndsWith("_tunnel", StringComparison.OrdinalIgnoreCase))
        {
            fileName = fileName[..^"_tunnel".Length];
        }
        else if (fileName.EndsWith("_bridge", StringComparison.OrdinalIgnoreCase))
        {
            fileName = fileName[..^"_bridge".Length];
        }

        var profile = MapStudioStandardRoadCatalog.Profiles
            .FirstOrDefault(candidate =>
                string.Equals(
                    Path.GetFileNameWithoutExtension(candidate.RelativePath),
                    fileName,
                    StringComparison.OrdinalIgnoreCase));

        return Math.Clamp(
            profile?.TotalWidthMeters ?? DefaultRoadWidthMeters,
            3.0,
            30.0);
    }

    private static double ResolveEndRotation(
        NativeSplinePlacementRequest request)
    {
        var delta = Math.Abs(request.Radius) > 0.001
            ? request.Length / request.Radius * 180.0 / Math.PI
            : 0.0;

        return request.Rotation + delta;
    }

    private static double NormalizeDegrees(double value)
    {
        value %= 360.0;

        if (value < 0)
        {
            value += 360.0;
        }

        return value;
    }
}
