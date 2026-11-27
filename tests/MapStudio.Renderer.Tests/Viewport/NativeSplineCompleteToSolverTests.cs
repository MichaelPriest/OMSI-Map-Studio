using MapStudio.Core.Omsi.Maps;
using MapStudio.Renderer.Picking;
using MapStudio.Renderer.Scene;
using MapStudio.Renderer.Viewport;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeSplineCompleteToSolverTests
{
    [Fact]
    public void CreatesStraightConnectorBetweenAlignedEnds()
    {
        var source =
            CreateSpline(
                10,
                0,
                -10,
                0,
                10,
                0);

        var target =
            CreateSpline(
                20,
                0,
                30,
                0,
                10,
                0);

        var scene =
            CreateScene(
                source,
                target);

        var ok =
            NativeSplineCompleteToSolver
                .TryCreateRequest(
                    scene,
                    10,
                    20,
                    200,
                    out var request,
                    out _);

        Assert.True(ok);
        Assert.NotNull(request);
        Assert.False(request!.IsCurved);
        Assert.Equal(30, request.Length, 5);
        Assert.Equal(0, request.Radius, 5);
        Assert.Equal(10, request.PreviousSplineId);
        Assert.Equal(20, request.NextSplineId);
    }

    [Fact]
    public void CreatesQuarterCircleMatchingBothTangents()
    {
        var source =
            CreateSpline(
                10,
                0,
                -10,
                0,
                10,
                0);

        var target =
            CreateSpline(
                20,
                10,
                10,
                90,
                10,
                0);

        var scene =
            CreateScene(
                source,
                target);

        var ok =
            NativeSplineCompleteToSolver
                .TryCreateRequest(
                    scene,
                    10,
                    20,
                    200,
                    out var request,
                    out _);

        Assert.True(ok);
        Assert.NotNull(request);
        Assert.True(request!.IsCurved);
        Assert.Equal(10, request.Radius, 4);
        Assert.Equal(
            Math.PI *
                5,
            request.Length,
            4);
    }

    [Fact]
    public void RejectsCurveBeyondMaximumRadius()
    {
        var source =
            CreateSpline(
                10,
                0,
                -10,
                0,
                10,
                0);

        var target =
            CreateSpline(
                20,
                10,
                10,
                90,
                10,
                0);

        var ok =
            NativeSplineCompleteToSolver
                .TryCreateRequest(
                    CreateScene(
                        source,
                        target),
                    10,
                    20,
                    5,
                    out _,
                    out var status);

        Assert.False(ok);
        Assert.Contains(
            "excede",
            status,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsDifferentSplineProfiles()
    {
        var source =
            CreateSpline(
                10,
                0,
                -10,
                0,
                10,
                0);

        var target =
            CreateSpline(
                20,
                10,
                10,
                90,
                10,
                0,
                @"Splines\Other\other.sli");

        var ok =
            NativeSplineCompleteToSolver
                .TryCreateRequest(
                    CreateScene(
                        source,
                        target),
                    10,
                    20,
                    200,
                    out _,
                    out var status);

        Assert.False(ok);
        Assert.Contains(
            "mesma SLI",
            status,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsOccupiedEndpoints()
    {
        var source =
            CreateSpline(
                10,
                0,
                -10,
                0,
                10,
                0,
                next:
                    99);

        var target =
            CreateSpline(
                20,
                10,
                10,
                90,
                10,
                0);

        Assert.False(
            NativeSplineCompleteToSolver
                .TryCreateRequest(
                    CreateScene(
                        source,
                        target),
                    10,
                    20,
                    200,
                    out _,
                    out _));
    }

    [Theory]
    [InlineData(12, 40)]
    [InlineData(-12, 40)]
    public void CreatesSmoothTwoArcConnectorBetweenOffsetParallelRoads(double x, double z)
    {
        var source = CreateSpline(10, 0, -10, 0, 10, 0);
        var target = CreateSpline(20, x, z, 0, 10, 0);
        Assert.True(NativeSplineCompleteToSolver.TryCreateRequest(
            CreateScene(source, target), 10, 20, 200, out var request, out var status), status);
        Assert.NotNull(request);
        var second = Assert.IsType<NativeSplinePlacementRequest>(request!.Continuation);
        Assert.Equal(10, request.PreviousSplineId);
        Assert.Equal(20, request.NextSplineId);
        Assert.Equal(request.EndWorld, second.StartWorld);
        Assert.Equal(0, request.Rotation, 5);
        Assert.True(request.Radius * second.Radius < 0);
        var firstEndHeading = request.Rotation + request.Length / request.Radius * 180 / Math.PI;
        var secondEndHeading = second.Rotation + second.Length / second.Radius * 180 / Math.PI;
        Assert.InRange(Math.Abs(Math.IEEERemainder(firstEndHeading - second.Rotation, 360)), 0, 0.01);
        Assert.InRange(Math.Abs(Math.IEEERemainder(secondEndHeading, 360)), 0, 0.01);
        Assert.Equal((float)x, second.EndWorld.X, 4);
        Assert.Equal((float)z, second.EndWorld.Z, 4);
    }

    private static NativeSceneSnapshot
        CreateScene(
            params NativeSplineEntity[] splines) =>
        new(
            [
                new NativeSceneTile(
                    new OmsiTileReference(
                        0,
                        0,
                        "tile_0_0.map"),
                    OmsiTileContent.Missing),
                new NativeSceneTile(new OmsiTileReference(-1, 0, "tile_-1_0.map"),
                    OmsiTileContent.Missing)
            ],
            [],
            splines,
            []);

    private static NativeSplineEntity
        CreateSpline(
            int id,
            double worldX,
            double worldZ,
            double rotation,
            double length,
            double radius,
            string path =
                @"Splines\Roads\road.sli",
            int previous =
                -1,
            int next =
                -1) =>
        new(
            new PickingId(
                PickingKind.Spline,
                id),
            new OmsiTileReference(
                0,
                0,
                "tile_0_0.map"),
            new OmsiPlacedSpline(
                "0",
                path,
                id,
                previous,
                next,
                worldX,
                worldZ,
                0,
                rotation,
                length,
                radius,
                0,
                0,
                false,
                []),
            (float)worldX,
            0,
            (float)worldZ);
}
