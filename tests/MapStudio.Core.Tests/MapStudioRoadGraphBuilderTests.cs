using MapStudio.Core.Generation.Roads;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioRoadGraphBuilderTests
{
    [Fact]
    public void CrossingTracesCreateFourSegmentsAndOneJunction()
    {
        var graph =
            new MapStudioRoadGraphBuilder()
                .Build(
                    [
                        new MapStudioRoadTrace(
                            "east-west",
                            [
                                new(-10, 0),
                                new(10, 0)
                            ],
                            "road-2lane"),
                        new MapStudioRoadTrace(
                            "north-south",
                            [
                                new(0, -10),
                                new(0, 10)
                            ],
                            "road-2lane")
                    ]);

        Assert.Equal(
            5,
            graph.Nodes.Count);

        Assert.Equal(
            4,
            graph.Segments.Count);

        var junction =
            Assert.Single(
                graph.Junctions);

        Assert.Equal(
            4,
            junction.Degree);

        Assert.Equal(
            0,
            junction.Position.X,
            6);

        Assert.Equal(
            0,
            junction.Position.Z,
            6);

        Assert.Equal(
            2,
            junction.TraceIds.Count);
    }

    [Fact]
    public void TIntersectionSplitsMainRoadAtBranchEndpoint()
    {
        var graph =
            new MapStudioRoadGraphBuilder()
                .Build(
                    [
                        new MapStudioRoadTrace(
                            "main",
                            [
                                new(-20, 0),
                                new(20, 0)
                            ],
                            "avenue"),
                        new MapStudioRoadTrace(
                            "branch",
                            [
                                new(0, 0.08),
                                new(0, 12)
                            ],
                            "local")
                    ],
                    snapToleranceMeters:
                        0.20);

        var junction =
            Assert.Single(
                graph.Junctions);

        Assert.Equal(
            3,
            junction.Degree);

        Assert.Equal(
            3,
            graph.Segments.Count);

        Assert.Contains(
            graph.Segments,
            segment =>
                segment.TraceId ==
                "branch");

        Assert.Equal(
            2,
            junction.TraceIds.Count);
    }

    [Fact]
    public void BendInsideSingleTraceIsNotReportedAsJunction()
    {
        var graph =
            new MapStudioRoadGraphBuilder()
                .Build(
                    [
                        new MapStudioRoadTrace(
                            "curved-road",
                            [
                                new(0, 0),
                                new(10, 0),
                                new(15, 5)
                            ],
                            "road")
                    ]);

        Assert.Empty(
            graph.Junctions);

        Assert.Equal(
            2,
            graph.Segments.Count);

        Assert.Contains(
            graph.Nodes,
            node =>
                node.Degree ==
                    2 &&
                !node.IsJunction);
    }

    [Fact]
    public void SegmentMetadataSurvivesGraphSplitting()
    {
        var graph =
            new MapStudioRoadGraphBuilder()
                .Build(
                    [
                        new MapStudioRoadTrace(
                            "avenue",
                            [
                                new(-10, 0),
                                new(10, 0)
                            ],
                            "ms_avenue_divided_4lane",
                            LaneCount:
                                4,
                            OneWay:
                                false,
                            WidthMeters:
                                16),
                        new MapStudioRoadTrace(
                            "cross",
                            [
                                new(0, -5),
                                new(0, 5)
                            ],
                            "ms_road_2lane_7m")
                    ]);

        var avenueSegments =
            graph.Segments
                .Where(
                    segment =>
                        segment.TraceId ==
                        "avenue")
                .ToArray();

        Assert.Equal(
            2,
            avenueSegments.Length);

        Assert.All(
            avenueSegments,
            segment =>
            {
                Assert.Equal(
                    4,
                    segment.LaneCount);

                Assert.False(
                    segment.OneWay);

                Assert.Equal(
                    16,
                    segment.WidthMeters);
            });
    }

    [Fact]
    public void DuplicateTraceIdsAreRejected()
    {
        Assert.Throws<
            InvalidDataException>(
                () =>
                    new MapStudioRoadGraphBuilder()
                        .Build(
                            [
                                new MapStudioRoadTrace(
                                    "same",
                                    [
                                        new(0, 0),
                                        new(1, 0)
                                    ],
                                    "road"),
                                new MapStudioRoadTrace(
                                    "same",
                                    [
                                        new(0, 1),
                                        new(1, 1)
                                    ],
                                    "road")
                            ]));
    }
}
