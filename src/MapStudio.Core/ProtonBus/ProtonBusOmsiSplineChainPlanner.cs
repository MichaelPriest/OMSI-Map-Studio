using MapStudio.Core.Omsi.Maps;

namespace MapStudio.Core.ProtonBus;

public sealed record ProtonBusOmsiSplineChainIssue(
    string Code,
    int SplineId,
    int? RelatedSplineId = null,
    string? Detail = null);

public sealed record ProtonBusOmsiSplineChain(
    int HeadSplineId,
    IReadOnlyList<OmsiPlacedSpline> Splines,
    bool IsClosedLoop = false);

public sealed record ProtonBusOmsiSplineChainPlan(
    IReadOnlyList<ProtonBusOmsiSplineChain> Chains,
    IReadOnlyList<ProtonBusOmsiSplineChainIssue> Issues);

public static class ProtonBusOmsiSplineChainPlanner
{
    public static ProtonBusOmsiSplineChainPlan Plan(
        IReadOnlyList<OmsiPlacedSpline> splines)
    {
        ArgumentNullException.ThrowIfNull(
            splines);

        var byId =
            new Dictionary<int, OmsiPlacedSpline>();

        var issues =
            new List<ProtonBusOmsiSplineChainIssue>();

        foreach (var spline in splines)
        {
            ArgumentNullException.ThrowIfNull(
                spline);

            if (!byId.TryAdd(
                    spline.SplineId,
                    spline))
            {
                issues.Add(
                    new(
                        "duplicateSplineId",
                        spline.SplineId,
                        Detail:
                            "Linked spline chains require unique spline IDs inside the tile."));
            }
        }

        if (issues.Any(
                issue =>
                    issue.Code ==
                    "duplicateSplineId"))
        {
            return new(
                Array.Empty<ProtonBusOmsiSplineChain>(),
                issues);
        }

        foreach (var spline in splines)
        {
            if (spline.NextSplineId != -1)
            {
                if (!byId.TryGetValue(
                        spline.NextSplineId,
                        out var next))
                {
                    issues.Add(
                        new(
                            "nextSplineMissing",
                            spline.SplineId,
                            spline.NextSplineId));
                }
                else if (next.PreviousSplineId !=
                         spline.SplineId)
                {
                    issues.Add(
                        new(
                            "nextSplineNotReciprocal",
                            spline.SplineId,
                            spline.NextSplineId,
                            $"Spline {next.SplineId} points back to {next.PreviousSplineId}."));
                }
            }

            if (spline.PreviousSplineId != -1)
            {
                if (!byId.TryGetValue(
                        spline.PreviousSplineId,
                        out var previous))
                {
                    issues.Add(
                        new(
                            "previousSplineMissing",
                            spline.SplineId,
                            spline.PreviousSplineId));
                }
                else if (previous.NextSplineId !=
                         spline.SplineId)
                {
                    issues.Add(
                        new(
                            "previousSplineNotReciprocal",
                            spline.SplineId,
                            spline.PreviousSplineId,
                            $"Spline {previous.SplineId} points forward to {previous.NextSplineId}."));
                }
            }
        }

        var visited =
            new HashSet<int>();

        var chains =
            new List<ProtonBusOmsiSplineChain>();

        foreach (
            var head
            in splines
                .Where(
                    spline =>
                        spline.PreviousSplineId ==
                            -1 ||
                        !byId.ContainsKey(
                            spline.PreviousSplineId))
                .OrderBy(
                    spline =>
                        spline.SplineId))
        {
            if (visited.Contains(
                    head.SplineId))
            {
                continue;
            }

            chains.Add(
                WalkChain(
                    head,
                    byId,
                    visited,
                    issues));
        }

        foreach (
            var remaining
            in splines
                .OrderBy(
                    spline =>
                        spline.SplineId))
        {
            if (
                visited.Contains(
                    remaining.SplineId))
            {
                continue;
            }

            chains.Add(
                WalkChain(
                    remaining,
                    byId,
                    visited,
                    issues));
        }

        return new(
            chains,
            issues);
    }

    private static ProtonBusOmsiSplineChain
        WalkChain(
            OmsiPlacedSpline start,
            IReadOnlyDictionary<int, OmsiPlacedSpline>
                byId,
            ISet<int> visited,
            ICollection<ProtonBusOmsiSplineChainIssue>
                issues)
    {
        var chain =
            new List<OmsiPlacedSpline>();

        var local =
            new HashSet<int>();

        var current =
            start;

        var closedLoop =
            false;

        while (true)
        {
            if (!local.Add(
                    current.SplineId))
            {
                closedLoop =
                    current.SplineId ==
                    start.SplineId;

                if (!closedLoop)
                {
                    issues.Add(
                        new(
                            "splineChainCycle",
                            current.SplineId,
                            start.SplineId,
                            "The chain enters a cycle that does not close at its head."));
                }

                break;
            }

            if (!visited.Add(
                    current.SplineId))
            {
                issues.Add(
                    new(
                        "splineChainOverlap",
                        current.SplineId,
                        start.SplineId,
                        "The same spline is reachable from more than one chain head."));

                break;
            }

            chain.Add(
                current);

            if (current.NextSplineId ==
                -1)
            {
                break;
            }

            if (!byId.TryGetValue(
                    current.NextSplineId,
                    out var next))
            {
                break;
            }

            current =
                next;
        }

        return new(
            start.SplineId,
            chain,
            closedLoop);
    }
}
