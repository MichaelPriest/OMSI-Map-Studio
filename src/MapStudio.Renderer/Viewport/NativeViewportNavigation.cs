using System.Numerics;
using MapStudio.Renderer.Scene;

namespace MapStudio.Renderer.Viewport;

public sealed record NativeViewportNavigationState(
    Vector3 Target,
    float Distance,
    float Yaw,
    float Pitch);

public sealed class NativeViewportNavigation
{
    private const float FieldOfViewRadians =
        MathF.PI *
        55.0f /
        180.0f;

    private const float DefaultYaw =
        -0.72f;

    private const float DefaultPitch =
        0.95f;

    private const float TopViewPitch =
        MathF.PI /
        2.0f -
        0.017453292f;

    private Vector3 _homeTarget =
        Vector3.Zero;

    private float _homeDistance =
        900.0f;

    private float _minimumDistance =
        12.0f;

    private float _maximumDistance =
        20_000.0f;

    public Vector3 Target { get; private set; }

    public float Distance { get; private set; } =
        900.0f;

    public float Yaw { get; private set; } =
        DefaultYaw;

    public float Pitch { get; private set; } =
        DefaultPitch;

    public float Zoom =>
        _homeDistance /
        Math.Max(
            0.001f,
            Distance);

    public Vector3 CameraPosition
    {
        get
        {
            var horizontal =
                MathF.Cos(Pitch);

            var direction =
                new Vector3(
                    MathF.Sin(Yaw) *
                    horizontal,
                    MathF.Sin(Pitch),
                    MathF.Cos(Yaw) *
                    horizontal);

            return
                Target +
                direction *
                Distance;
        }
    }

    public void FitToScene(
        NativeSceneSnapshot scene)
    {
        ArgumentNullException.ThrowIfNull(
            scene);

        var bounds =
            NativeSceneProjection
                .FromScene(scene);

        var minHeight =
            float.PositiveInfinity;

        var maxHeight =
            float.NegativeInfinity;

        foreach (var terrain in scene.Terrain)
        {
            foreach (
                var height in
                    terrain.Terrain.Heights)
            {
                if (!float.IsFinite(height))
                {
                    continue;
                }

                minHeight =
                    MathF.Min(
                        minHeight,
                        height);

                maxHeight =
                    MathF.Max(
                        maxHeight,
                        height);
            }
        }

        if (
            !float.IsFinite(minHeight) ||
            !float.IsFinite(maxHeight))
        {
            minHeight = 0;
            maxHeight = 0;
        }

        _homeTarget =
            new Vector3(
                (float)bounds.CenterX,
                (minHeight + maxHeight) *
                0.5f,
                (float)bounds.CenterZ);

        var horizontalSpan =
            (float)Math.Max(
                bounds.HalfWidth * 2.0,
                bounds.HalfHeight * 2.0);

        var verticalSpan =
            MathF.Max(
                0,
                maxHeight - minHeight);

        var framingSpan =
            MathF.Max(
                60.0f,
                MathF.Max(
                    horizontalSpan,
                    verticalSpan *
                    4.0f));

        _homeDistance =
            Math.Clamp(
                framingSpan *
                1.45f,
                120.0f,
                12_000.0f);

        _minimumDistance =
            MathF.Max(
                8.0f,
                framingSpan *
                0.0125f);

        _maximumDistance =
            MathF.Max(
                20_000.0f,
                _homeDistance *
                12.0f);

        Reset();
    }

    public void FitToBounds(
        Vector3 minimum,
        Vector3 maximum)
    {
        var center =
            (minimum + maximum) *
            0.5f;

        var extent =
            Vector3.Max(
                maximum - minimum,
                Vector3.Zero);

        var span =
            MathF.Max(
                1.0f,
                MathF.Max(
                    extent.X,
                    MathF.Max(
                        extent.Y,
                        extent.Z)));

        _homeTarget =
            center;

        _homeDistance =
            Math.Clamp(
                span *
                1.85f,
                4.0f,
                4_000.0f);

        _minimumDistance =
            MathF.Max(
                0.75f,
                span *
                0.04f);

        _maximumDistance =
            MathF.Max(
                4_000.0f,
                _homeDistance *
                16.0f);

        Reset();
    }

    public void Reset()
    {
        Target =
            _homeTarget;

        Distance =
            _homeDistance;

        Yaw =
            DefaultYaw;

        Pitch =
            DefaultPitch;
    }

    public NativeViewportNavigationState
        CaptureState() =>
        new(
            Target,
            Distance,
            Yaw,
            Pitch);

    public void RestoreState(
        NativeViewportNavigationState state)
    {
        ArgumentNullException.ThrowIfNull(
            state);

        if (
            !float.IsFinite(
                state.Target.X) ||
            !float.IsFinite(
                state.Target.Y) ||
            !float.IsFinite(
                state.Target.Z) ||
            !float.IsFinite(
                state.Distance) ||
            !float.IsFinite(
                state.Yaw) ||
            !float.IsFinite(
                state.Pitch))
        {
            Reset();
            return;
        }

        Target =
            state.Target;

        Distance =
            Math.Clamp(
                state.Distance,
                _minimumDistance,
                _maximumDistance);

        Yaw =
            state.Yaw;

        Pitch =
            Math.Clamp(
                state.Pitch,
                0.18f,
                1.553343f);
    }

    public void SetTopView()
    {
        Yaw =
            0.0f;

        Pitch =
            TopViewPitch;
    }

    public void SetPerspectiveView()
    {
        Yaw =
            DefaultYaw;

        Pitch =
            DefaultPitch;
    }

    public Matrix4x4 GetViewProjection(
        uint viewportWidth,
        uint viewportHeight)
    {
        var safeWidth =
            Math.Max(
                1u,
                viewportWidth);

        var safeHeight =
            Math.Max(
                1u,
                viewportHeight);

        var aspectRatio =
            safeWidth /
            (float)safeHeight;

        var view =
            Matrix4x4.CreateLookAt(
                CameraPosition,
                Target,
                Vector3.UnitY);

        var farPlane =
            MathF.Max(
                20_000.0f,
                Distance *
                24.0f);

        var projection =
            Matrix4x4
                .CreatePerspectiveFieldOfView(
                    FieldOfViewRadians,
                    aspectRatio,
                    0.5f,
                    farPlane);

        return
            view *
            projection;
    }

    public bool TryGetWorldRay(
        uint pixelX,
        uint pixelY,
        uint viewportWidth,
        uint viewportHeight,
        out Vector3 origin,
        out Vector3 direction)
    {
        origin =
            Vector3.Zero;

        direction =
            Vector3.Zero;

        if (
            viewportWidth == 0 ||
            viewportHeight == 0)
        {
            return false;
        }

        var viewProjection =
            GetViewProjection(
                viewportWidth,
                viewportHeight);

        if (
            !Matrix4x4.Invert(
                viewProjection,
                out var inverse))
        {
            return false;
        }

        var ndcX =
            pixelX /
            (float)viewportWidth *
            2.0f -
            1.0f;

        var ndcY =
            1.0f -
            pixelY /
            (float)viewportHeight *
            2.0f;

        Vector3 Unproject(
            float depth)
        {
            var point =
                Vector4.Transform(
                    new Vector4(
                        ndcX,
                        ndcY,
                        depth,
                        1.0f),
                    inverse);

            if (
                Math.Abs(point.W) >
                0.000001f)
            {
                point /=
                    point.W;
            }

            return new Vector3(
                point.X,
                point.Y,
                point.Z);
        }

        var near =
            Unproject(
                0.0f);

        var far =
            Unproject(
                1.0f);

        var delta =
            far -
            near;

        if (
            delta.LengthSquared() <
            0.000001f)
        {
            return false;
        }

        origin =
            near;

        direction =
            Vector3.Normalize(
                delta);

        return true;
    }

    public bool TryProjectWorldPoint(
        Vector3 worldPoint,
        uint viewportWidth,
        uint viewportHeight,
        out Vector2 pixelPoint)
    {
        pixelPoint =
            Vector2.Zero;

        if (
            viewportWidth == 0 ||
            viewportHeight == 0 ||
            !float.IsFinite(worldPoint.X) ||
            !float.IsFinite(worldPoint.Y) ||
            !float.IsFinite(worldPoint.Z))
        {
            return false;
        }

        var projected =
            Vector4.Transform(
                new Vector4(
                    worldPoint,
                    1.0f),
                GetViewProjection(
                    viewportWidth,
                    viewportHeight));

        if (
            !float.IsFinite(projected.W) ||
            projected.W <=
                0.000001f)
        {
            return false;
        }

        projected /=
            projected.W;

        if (
            !float.IsFinite(projected.X) ||
            !float.IsFinite(projected.Y))
        {
            return false;
        }

        pixelPoint =
            new Vector2(
                (
                    projected.X +
                    1.0f
                ) *
                0.5f *
                viewportWidth,
                (
                    1.0f -
                    projected.Y
                ) *
                0.5f *
                viewportHeight);

        return true;
    }

    public void FocusOn(
        Vector3 target,
        float preferredDistance =
            100.0f)
    {
        Target =
            target;

        Distance =
            Math.Clamp(
                preferredDistance,
                _minimumDistance,
                _maximumDistance);
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

        Distance =
            Math.Clamp(
                Distance /
                factor,
                _minimumDistance,
                _maximumDistance);
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

        var forward =
            Vector3.Normalize(
                Target -
                CameraPosition);

        var horizontalForward =
            new Vector3(
                forward.X,
                0,
                forward.Z);

        if (
            horizontalForward.LengthSquared() <
            0.000001f)
        {
            return;
        }

        horizontalForward =
            Vector3.Normalize(
                horizontalForward);

        var right =
            Vector3.Normalize(
                Vector3.Cross(
                    Vector3.UnitY,
                    horizontalForward));

        var worldPerPixel =
            2.0f *
            Distance *
            MathF.Tan(
                FieldOfViewRadians *
                0.5f) /
            viewportHeight;

        Target -=
            right *
            (float)deltaX *
            worldPerPixel;

        Target +=
            horizontalForward *
            (float)deltaY *
            worldPerPixel;
    }

    public void OrbitPixels(
        double deltaX,
        double deltaY)
    {
        Yaw -=
            (float)deltaX *
            0.0050f;

        Pitch =
            Math.Clamp(
                Pitch -
                (float)deltaY *
                0.0040f,
                0.18f,
                1.42f);
    }
}
