namespace MapStudio.Renderer.Viewport;

public static class NativeTerrainBrushVisualMath
{
    public static double ResolveIntensityOpacity(
        double intensityMeters)
    {
        var safeIntensity =
            double.IsFinite(intensityMeters) &&
            intensityMeters > 0
                ? Math.Clamp(intensityMeters, 0.05, 100.0)
                : 0.05;

        var normalized =
            Math.Log10(1.0 + safeIntensity) /
            Math.Log10(101.0);

        return Math.Clamp(
            0.08 + normalized * 0.30,
            0.08,
            0.38);
    }
}
