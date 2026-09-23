namespace MapStudio.Core.Omsi.Maps;

public static class OmsiSplineLinkPlanner
{
    public static IReadOnlyDictionary<
        int,
        OmsiSplineLinkTarget>
        Plan(
            IReadOnlyDictionary<
                int,
                OmsiSplineLinkState> states,
            int splineId,
            int originalPreviousSplineId,
            int originalNextSplineId,
            int desiredPreviousSplineId,
            int desiredNextSplineId)
    {
        ArgumentNullException.ThrowIfNull(states);

        if (
            desiredPreviousSplineId == splineId ||
            desiredNextSplineId == splineId)
        {
            throw new InvalidDataException(
                "splineLinkSelf");
        }

        if (
            desiredPreviousSplineId != -1 &&
            desiredPreviousSplineId ==
                desiredNextSplineId)
        {
            throw new InvalidDataException(
                "splineLinkDuplicateNeighbor");
        }

        if (
            !states.TryGetValue(
                splineId,
                out var source) ||
            source.PreviousSplineId !=
                originalPreviousSplineId ||
            source.NextSplineId !=
                originalNextSplineId)
        {
            throw new InvalidDataException(
                "splineLinkSourceChanged");
        }

        var desired =
            new Dictionary<
                int,
                OmsiSplineLinkTarget>();

        OmsiSplineLinkState Require(
            int id)
        {
            if (
                !states.TryGetValue(
                    id,
                    out var state))
            {
                throw new InvalidDataException(
                    "splineLinkNeighborMissing");
            }

            return state;
        }

        OmsiSplineLinkTarget CurrentTarget(
            int id)
        {
            if (desired.TryGetValue(
                    id,
                    out var target))
            {
                return target;
            }

            var state = Require(id);

            return new OmsiSplineLinkTarget(
                state.PreviousSplineId,
                state.NextSplineId);
        }

        void SetTarget(
            int id,
            int? previous = null,
            int? next = null)
        {
            var current =
                CurrentTarget(id);

            desired[id] =
                new OmsiSplineLinkTarget(
                    previous ??
                        current.PreviousSplineId,
                    next ??
                        current.NextSplineId);
        }

        if (
            originalPreviousSplineId !=
                -1 &&
            states.TryGetValue(
                originalPreviousSplineId,
                out var originalPrevious))
        {
            if (
                originalPrevious.NextSplineId !=
                    splineId)
            {
                throw new InvalidDataException(
                    "splineLinkConflict");
            }

            if (
                originalPreviousSplineId !=
                    desiredPreviousSplineId)
            {
                SetTarget(
                    originalPreviousSplineId,
                    next: -1);
            }
        }

        if (
            originalNextSplineId !=
                -1 &&
            states.TryGetValue(
                originalNextSplineId,
                out var originalNext))
        {
            if (
                originalNext.PreviousSplineId !=
                    splineId)
            {
                throw new InvalidDataException(
                    "splineLinkConflict");
            }

            if (
                originalNextSplineId !=
                    desiredNextSplineId)
            {
                SetTarget(
                    originalNextSplineId,
                    previous: -1);
            }
        }

        if (desiredPreviousSplineId != -1)
        {
            var previous =
                Require(
                    desiredPreviousSplineId);

            if (
                previous.NextSplineId !=
                    -1 &&
                previous.NextSplineId !=
                    splineId &&
                states.ContainsKey(
                    previous.NextSplineId))
            {
                throw new InvalidDataException(
                    "splineLinkTargetBusy");
            }

            SetTarget(
                desiredPreviousSplineId,
                next: splineId);
        }

        if (desiredNextSplineId != -1)
        {
            var next =
                Require(
                    desiredNextSplineId);

            if (
                next.PreviousSplineId !=
                    -1 &&
                next.PreviousSplineId !=
                    splineId &&
                states.ContainsKey(
                    next.PreviousSplineId))
            {
                throw new InvalidDataException(
                    "splineLinkTargetBusy");
            }

            SetTarget(
                desiredNextSplineId,
                previous: splineId);
        }

        SetTarget(
            splineId,
            desiredPreviousSplineId,
            desiredNextSplineId);

        return desired
            .Where(pair =>
            {
                var current =
                    Require(pair.Key);

                return
                    current.PreviousSplineId !=
                        pair.Value
                            .PreviousSplineId ||
                    current.NextSplineId !=
                        pair.Value
                            .NextSplineId;
            })
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value);
    }
}
