using MapStudio.Core.Omsi.Maps;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiSplineLinkPlannerTests
{
    [Fact]
    public void RepairsMissingOriginalNeighborAndReciprocalBrokenTarget()
    {
        var states =
            new Dictionary<int, OmsiSplineLinkState>
            {
                [10] =
                    new OmsiSplineLinkState(
                        10,
                        -1,
                        999),
                [20] =
                    new OmsiSplineLinkState(
                        20,
                        888,
                        -1)
            };

        var plan =
            OmsiSplineLinkPlanner.Plan(
                states,
                10,
                -1,
                999,
                -1,
                20);

        Assert.Equal(
            20,
            plan[10].NextSplineId);

        Assert.Equal(
            10,
            plan[20].PreviousSplineId);
    }

    [Fact]
    public void StillRejectsTargetWithValidExistingNeighbor()
    {
        var states =
            new Dictionary<int, OmsiSplineLinkState>
            {
                [10] =
                    new OmsiSplineLinkState(
                        10,
                        -1,
                        -1),
                [20] =
                    new OmsiSplineLinkState(
                        20,
                        30,
                        -1),
                [30] =
                    new OmsiSplineLinkState(
                        30,
                        -1,
                        20)
            };

        Assert.Throws<
            InvalidDataException>(
            () =>
                OmsiSplineLinkPlanner.Plan(
                    states,
                    10,
                    -1,
                    -1,
                    -1,
                    20));
    }
}
