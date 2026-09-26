using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.ProtonBus;
using Xunit;

namespace MapStudio.ProtonBus.Tests;

public sealed class ProtonBusOmsiSplineChainPlannerTests
{
    [Fact]
    public void PlannerCreatesContinuousChainFromReciprocalLinks()
    {
        var plan =
            ProtonBusOmsiSplineChainPlanner
                .Plan(
                    [
                        CreateSpline(
                            20,
                            previous:
                                10,
                            next:
                                30),
                        CreateSpline(
                            10,
                            previous:
                                -1,
                            next:
                                20),
                        CreateSpline(
                            30,
                            previous:
                                20,
                            next:
                                -1)
                    ]);

        var chain =
            Assert.Single(
                plan.Chains);

        Assert.Equal(
            10,
            chain.HeadSplineId);

        Assert.Equal(
            [
                10,
                20,
                30
            ],
            chain.Splines
                .Select(
                    spline =>
                        spline.SplineId)
                .ToArray());

        Assert.False(
            chain.IsClosedLoop);

        Assert.Empty(
            plan.Issues);
    }

    [Fact]
    public void PlannerRecognizesClosedLoop()
    {
        var plan =
            ProtonBusOmsiSplineChainPlanner
                .Plan(
                    [
                        CreateSpline(
                            1,
                            previous:
                                3,
                            next:
                                2),
                        CreateSpline(
                            2,
                            previous:
                                1,
                            next:
                                3),
                        CreateSpline(
                            3,
                            previous:
                                2,
                            next:
                                1)
                    ]);

        var chain =
            Assert.Single(
                plan.Chains);

        Assert.True(
            chain.IsClosedLoop);

        Assert.Equal(
            3,
            chain.Splines.Count);

        Assert.Equal(
            [
                1,
                2,
                3
            ],
            chain.Splines
                .Select(
                    spline =>
                        spline.SplineId)
                .ToArray());
    }

    [Fact]
    public void PlannerReportsBrokenReciprocalLink()
    {
        var plan =
            ProtonBusOmsiSplineChainPlanner
                .Plan(
                    [
                        CreateSpline(
                            1,
                            previous:
                                -1,
                            next:
                                2),
                        CreateSpline(
                            2,
                            previous:
                                -1,
                            next:
                                -1)
                    ]);

        Assert.Contains(
            plan.Issues,
            issue =>
                issue.Code ==
                "nextSplineNotReciprocal" &&
                issue.SplineId ==
                1 &&
                issue.RelatedSplineId ==
                2);
    }

    [Fact]
    public void PlannerReportsMissingNeighborButKeepsUsableChain()
    {
        var plan =
            ProtonBusOmsiSplineChainPlanner
                .Plan(
                    [
                        CreateSpline(
                            1,
                            previous:
                                -1,
                            next:
                                999)
                    ]);

        var chain =
            Assert.Single(
                plan.Chains);

        Assert.Single(
            chain.Splines);

        Assert.Contains(
            plan.Issues,
            issue =>
                issue.Code ==
                "nextSplineMissing" &&
                issue.RelatedSplineId ==
                999);
    }

    [Fact]
    public void PlannerRejectsDuplicateSplineIds()
    {
        var plan =
            ProtonBusOmsiSplineChainPlanner
                .Plan(
                    [
                        CreateSpline(
                            1,
                            -1,
                            -1),
                        CreateSpline(
                            1,
                            -1,
                            -1)
                    ]);

        Assert.Empty(
            plan.Chains);

        Assert.Contains(
            plan.Issues,
            issue =>
                issue.Code ==
                "duplicateSplineId");
    }

    private static OmsiPlacedSpline CreateSpline(
        int id,
        int previous,
        int next) =>
        new(
            HeaderValue:
                "spline",
            SplinePath:
                @"Splines\Test\road.sli",
            SplineId:
                id,
            PreviousSplineId:
                previous,
            NextSplineId:
                next,
            X:
                0,
            Z:
                0,
            Y:
                0,
            Rotation:
                0,
            Length:
                10,
            Radius:
                0,
            GradientStart:
                0,
            GradientEnd:
                0,
            IsHeightSpline:
                false,
            ExtraValues:
                []);
}
