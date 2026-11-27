using System.Numerics;

namespace MapStudio.Renderer.Scene;

public sealed record NativeRoadProfilePoint(double Distance, Vector3 WorldPoint, double? TerrainHeight, double Gradient)
{
    public double? Clearance => TerrainHeight is { } height ? WorldPoint.Y - height : null;
}

public sealed record NativeRoadProfile(int SplineId, string AssetPath, double Length, IReadOnlyList<NativeRoadProfilePoint> Points)
{
    public double MaxAbsoluteGradient => Points.Max(p => Math.Abs(p.Gradient));
    public double Rise => Points[^1].WorldPoint.Y - Points[0].WorldPoint.Y;
}

public static class NativeRoadProfileSampler
{
    public static NativeRoadProfile? Build(NativeSceneSnapshot scene, NativeSplineEntity entity)
    {
        var spline = entity.Spline;
        if (!double.IsFinite(spline.Length) || spline.Length <= 0 || !double.IsFinite(spline.Radius) ||
            !double.IsFinite(spline.Rotation) || !double.IsFinite(spline.GradientStart) || !double.IsFinite(spline.GradientEnd) ||
            !float.IsFinite(entity.WorldX) || !float.IsFinite(entity.WorldY) || !float.IsFinite(entity.WorldZ)) return null;
        var steps = (int)Math.Clamp(Math.Ceiling(spline.Length / 2), 2, 1000);
        var points = new List<NativeRoadProfilePoint>(steps + 1);
        for (var i = 0; i <= steps; i++)
        {
            var fraction = (double)i / steps;
            var distance = spline.Length * fraction;
            var frame = NativeSplinePathMath.GetFrame(entity, distance);
            var terrain = NativeTerrainSampler.TryGetHeightAtWorldPoint(scene, frame.Center.X, frame.Center.Z, out var height)
                ? (double?)height : null;
            var grade = spline.GradientStart * (1 - fraction) + spline.GradientEnd * fraction;
            points.Add(new NativeRoadProfilePoint(distance, frame.Center, terrain, grade));
        }
        return new NativeRoadProfile(spline.SplineId, spline.SplinePath, spline.Length, points);
    }
}
