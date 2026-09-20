using System.Numerics;

namespace MapStudio.Renderer.Viewport;

public sealed class NativeViewportNavigation
{
    private const float MinimumZoom =
        0.25f;

    private const float MaximumZoom =
        12.0f;

    public float OffsetX { get; private set; }

    public float OffsetY { get; private set; }

    public float Zoom { get; private set; } =
        1.0f;

    public Vector4 ShaderTransform =>
        new(
            OffsetX,
            OffsetY,
            Zoom,
            0.0f);

    public void Reset()
    {
        OffsetX = 0;
        OffsetY = 0;
        Zoom = 1.0f;
    }

    public void ZoomByWheel(
        int wheelDelta)
    {
        if (wheelDelta == 0)
        {
            return;
        }

        var steps =
            wheelDelta /
            120.0f;

        var factor =
            MathF.Pow(
                1.18f,
                steps);

        Zoom =
            Math.Clamp(
                Zoom * factor,
                MinimumZoom,
                MaximumZoom);
    }

    public void PanPixels(
        double deltaX,
        double deltaY,
        uint viewportWidth,
        uint viewportHeight)
    {
        if (
            viewportWidth == 0 ||
            viewportHeight == 0)
        {
            return;
        }

        OffsetX +=
            (float)(
                2.0 *
                deltaX /
                viewportWidth);

        OffsetY -=
            (float)(
                2.0 *
                deltaY /
                viewportHeight);

        OffsetX =
            Math.Clamp(
                OffsetX,
                -8.0f,
                8.0f);

        OffsetY =
            Math.Clamp(
                OffsetY,
                -8.0f,
                8.0f);
    }
}
