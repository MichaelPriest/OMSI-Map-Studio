using System.Numerics;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Renderer.Picking;
using MapStudio.Renderer.Scene;
using MapStudio.Renderer.Viewport;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class
    NativeSplinePlacementMathTests
{
    [Fact]
    public void StraightSplineRejectsNonFiniteCoordinates()
    {
        Assert.False(
            NativeSplinePlacementMath.TryCreateStraight(
                new Vector3(float.NaN, 0, 0),
                new Vector3(10, 0, 0),
                out var invalidStart));

        Assert.Null(invalidStart);

        Assert.False(
            NativeSplinePlacementMath.TryCreateStraight(
                Vector3.Zero,
                new Vector3(float.PositiveInfinity, 0, 0),
                out var invalidEnd));

        Assert.Null(invalidEnd);
    }

    [Fact]
    public void ArcAndControlPointRejectNonFiniteCoordinates()
    {
        Assert.False(
            NativeSplinePlacementMath.TryCreateArc(
                Vector3.Zero,
                new Vector3(10, 0, 10),
                new Vector3(5, float.NaN, 2),
                out var invalidArc));

        Assert.Null(invalidArc);

        Assert.False(
            NativeSplinePlacementMath.TryGetCurveOffsetFromControlPoint(
                Vector3.Zero,
                new Vector3(10, 0, 0),
                new Vector3(5, 0, float.PositiveInfinity),
                out var offset));

        Assert.Equal(0, offset);

        Assert.False(
            NativeSplinePlacementMath.TryCreateArcFromOffset(
                new Vector3(float.NaN, 0, 0),
                new Vector3(10, 0, 0),
                2,
                out var invalidOffsetArc));

        Assert.Null(invalidOffsetArc);
    }

    [Fact]
    public void ArcFromOffsetUsesDoublePrecisionForExtremeFiniteHeightDifference()
    {
        var ok = NativeSplinePlacementMath.TryCreateArcFromOffset(
            new Vector3(0, -float.MaxValue, 0),
            new Vector3(40, float.MaxValue, 0),
            8,
            out var shape);

        Assert.True(ok);
        Assert.NotNull(shape);
        Assert.True(double.IsFinite(shape!.GradientStart));
        Assert.True(double.IsFinite(shape.GradientEnd));
    }

    [Fact]
    public void ExtremeFiniteArcInputsNeverProduceNonFiniteShapeFields()
    {
        var maximum = float.MaxValue;
        var start = new Vector3(-maximum, -maximum, -maximum);
        var end = new Vector3(maximum, maximum, maximum);

        var straightOk = NativeSplinePlacementMath.TryCreateStraight(
            start, end, out var straight);
        Assert.True(straightOk);
        Assert.NotNull(straight);
        AssertFiniteShape(straight!);

        var arcOk = NativeSplinePlacementMath.TryCreateArc(
            start,
            end,
            new Vector3(0, 0, maximum),
            out var arc);
        Assert.True(arcOk);
        Assert.NotNull(arc);
        AssertFiniteShape(arc!);

        var offsetArcOk = NativeSplinePlacementMath.TryCreateArcFromOffset(
            start, end, double.MaxValue, out var offsetArc);
        Assert.True(offsetArcOk);
        Assert.NotNull(offsetArc);
        AssertFiniteShape(offsetArc!);
    }

    private static void AssertFiniteShape(NativeSplinePlacementShape shape)
    {
        Assert.True(float.IsFinite(shape.Start.X));
        Assert.True(float.IsFinite(shape.Start.Y));
        Assert.True(float.IsFinite(shape.Start.Z));
        Assert.True(float.IsFinite(shape.End.X));
        Assert.True(float.IsFinite(shape.End.Y));
        Assert.True(float.IsFinite(shape.End.Z));
        Assert.True(double.IsFinite(shape.Rotation));
        Assert.True(double.IsFinite(shape.Length));
        Assert.True(double.IsFinite(shape.Radius));
        Assert.True(double.IsFinite(shape.GradientStart));
        Assert.True(double.IsFinite(shape.GradientEnd));
    }

    [Fact]
    public void StraightSplineUsesOmsiYawAndGradient()
    {
        var ok =
            NativeSplinePlacementMath
                .TryCreateStraight(
                    new Vector3(
                        0,
                        10,
                        0),
                    new Vector3(
                        10,
                        12,
                        0),
                    out var shape);

        Assert.True(
            ok);

        Assert.NotNull(
            shape);

        Assert.Equal(
            90,
            shape!.Rotation,
            6);

        Assert.Equal(
            10,
            shape.Length,
            6);

        Assert.Equal(
            20,
            shape.GradientStart,
            6);

        Assert.Equal(
            0,
            shape.Radius,
            6);
    }

    [Fact]
    public void RoadElevationModesUseStartHeightForRampsAndLeveling()
    {
        const float terrain =
            103.0f;

        const float start =
            100.0f;

        Assert.Equal(
            106.0f,
            NativeSplinePlacementMath
                .ResolveRoadEndpointHeight(
                    terrain,
                    start,
                    NativeRoadElevationMode.Elevate,
                    6.0));

        Assert.Equal(
            100.0f,
            NativeSplinePlacementMath
                .ResolveRoadEndpointHeight(
                    terrain,
                    start,
                    NativeRoadElevationMode.Level,
                    99.0));

        Assert.Equal(
            95.0f,
            NativeSplinePlacementMath
                .ResolveRoadEndpointHeight(
                    terrain,
                    start,
                    NativeRoadElevationMode.Lower,
                    -5.0));

        Assert.Equal(
            105.0f,
            NativeSplinePlacementMath
                .ResolveRoadEndpointHeight(
                    terrain,
                    start,
                    NativeRoadElevationMode.FollowTerrain,
                    2.0));
    }

    [Fact]
    public void ArcThroughControlPointProducesFiniteRadius()
    {
        var ok =
            NativeSplinePlacementMath
                .TryCreateArc(
                    new Vector3(
                        0,
                        0,
                        0),
                    new Vector3(
                        10,
                        0,
                        10),
                    new Vector3(
                        8,
                        0,
                        2),
                    out var shape);

        Assert.True(
            ok);

        Assert.NotNull(
            shape);

        Assert.True(
            shape!.IsCurved);

        Assert.True(
            Math.Abs(
                shape.Radius) >
            0.1);

        Assert.True(
            shape.Length >
            10);
    }

    [Fact]
    public void UnknownRoadElevationModeFallsBackToTerrainHeight()
    {
        var height = NativeSplinePlacementMath.ResolveRoadEndpointHeight(
            42,
            100,
            unchecked((NativeRoadElevationMode)int.MaxValue),
            25);

        Assert.Equal(42, height);
        Assert.True(float.IsFinite(height));
    }

    [Fact]
    public void RoadElevationAlwaysReturnsFiniteHeightForMalformedInputs()
    {
        var invalidTerrain = NativeSplinePlacementMath.ResolveRoadEndpointHeight(
            float.NaN,
            12,
            NativeRoadElevationMode.FollowTerrain,
            5);
        var invalidStart = NativeSplinePlacementMath.ResolveRoadEndpointHeight(
            10,
            float.PositiveInfinity,
            NativeRoadElevationMode.Elevate,
            5);
        var overflow = NativeSplinePlacementMath.ResolveRoadEndpointHeight(
            float.MaxValue,
            float.MaxValue,
            NativeRoadElevationMode.Elevate,
            300);
        var invalidOffset = NativeSplinePlacementMath.ResolveRoadEndpointHeight(
            10,
            12,
            NativeRoadElevationMode.FollowTerrain,
            double.NaN);

        Assert.True(float.IsFinite(invalidTerrain));
        Assert.True(float.IsFinite(invalidStart));
        Assert.Equal(float.MaxValue, overflow);
        Assert.Equal(10, invalidOffset);
    }

    [Fact]
    public void ArcShapeReachesRequestedEndpointInRendererMath()
    {
        var start =
            new Vector3(
                10,
                2,
                20);

        var end =
            new Vector3(
                50,
                2,
                60);

        var control =
            new Vector3(
                38,
                2,
                31);

        Assert.True(
            NativeSplinePlacementMath
                .TryCreateArc(
                    start,
                    end,
                    control,
                    out var shape));

        Assert.NotNull(
            shape);

        Assert.True(
            shape!.IsCurved);

        var spline =
            new OmsiPlacedSpline(
                "0",
                "road.sli",
                1,
                -1,
                -1,
                start.X,
                start.Z,
                start.Y,
                shape.Rotation,
                shape.Length,
                shape.Radius,
                shape.GradientStart,
                shape.GradientEnd,
                false,
                []);

        var entity =
            new NativeSplineEntity(
                new PickingId(
                    PickingKind.Spline,
                    1),
                new OmsiTileReference(
                    0,
                    0,
                    "tile_0_0.map"),
                spline,
                start.X,
                start.Y,
                start.Z);

        var frame =
            NativeSplinePathMath
                .GetFrame(
                    entity,
                    shape.Length);

        Assert.InRange(
            frame.Center.X,
            end.X -
                0.01f,
            end.X +
                0.01f);

        Assert.InRange(
            frame.Center.Z,
            end.Z -
                0.01f,
            end.Z +
                0.01f);
    }

    [Fact]
    public void VisualCurveControlUsesOnlyLateralDistance()
    {
        var start =
            new Vector3(
                0,
                0,
                0);

        var end =
            new Vector3(
                40,
                0,
                0);

        Assert.True(
            NativeSplinePlacementMath
                .TryGetCurveOffsetFromControlPoint(
                    start,
                    end,
                    new Vector3(
                        20,
                        0,
                        -8),
                    out var first));

        Assert.True(
            NativeSplinePlacementMath
                .TryGetCurveOffsetFromControlPoint(
                    start,
                    end,
                    new Vector3(
                        35,
                        0,
                        -8),
                    out var second));

        Assert.Equal(
            8,
            first,
            6);

        Assert.Equal(
            first,
            second,
            6);

        Assert.True(
            NativeSplinePlacementMath
                .TryGetCurveOffsetFromControlPoint(
                    start,
                    end,
                    new Vector3(
                        20,
                        0,
                        8),
                    out var opposite));

        Assert.Equal(
            -8,
            opposite,
            6);
    }

    [Fact]
    public void CurveOffsetCreatesTwoPointEasyRoadArc()
    {
        var ok =
            NativeSplinePlacementMath
                .TryCreateArcFromOffset(
                    new Vector3(
                        0,
                        10,
                        0),
                    new Vector3(
                        40,
                        14,
                        0),
                    8,
                    out var shape);

        Assert.True(
            ok);

        Assert.NotNull(
            shape);

        Assert.True(
            shape!.IsCurved);

        Assert.True(
            shape.Radius >
            0);

        Assert.True(
            shape.Length >
            40);

        Assert.InRange(
            shape.GradientStart,
            0,
            10);
    }

    [Fact]
    public void NearZeroCurveOffsetFallsBackToStraight()
    {
        var ok =
            NativeSplinePlacementMath
                .TryCreateArcFromOffset(
                    Vector3.Zero,
                    new Vector3(
                        0,
                        0,
                        30),
                    0.01,
                    out var shape);

        Assert.True(
            ok);

        Assert.NotNull(
            shape);

        Assert.False(
            shape!.IsCurved);

        Assert.Equal(
            30,
            shape.Length,
            6);
    }

    [Fact]
    public void CollinearCurveFallsBackToStraight()
    {
        var ok =
            NativeSplinePlacementMath
                .TryCreateArc(
                    Vector3.Zero,
                    new Vector3(
                        10,
                        0,
                        0),
                    new Vector3(
                        5,
                        0,
                        0),
                    out var shape);

        Assert.True(
            ok);

        Assert.False(
            shape!.IsCurved);

        Assert.Equal(
            0,
            shape.Radius,
            6);
    }
}
