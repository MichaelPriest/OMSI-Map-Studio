using System.Numerics;
using MapStudio.Renderer.Scene;

namespace MapStudio.Renderer.Viewport;

public static class NativeGizmoManipulationMath
{
    private const float FieldOfViewRadians =
        MathF.PI *
        55.0f /
        180.0f;

    public static Vector3 GetMoveDelta(
        NativeGizmoHandle handle,
        Vector3 cameraPosition,
        Vector3 target,
        float cameraDistance,
        uint viewportHeight,
        double deltaPixelX,
        double deltaPixelY)
    {
        var safeHeight =
            Math.Max(
                1u,
                viewportHeight);

        var worldPerPixel =
            2.0f *
            Math.Max(
                0.01f,
                cameraDistance) *
            MathF.Tan(
                FieldOfViewRadians *
                0.5f) /
            safeHeight;

        if (
            handle ==
            NativeGizmoHandle.MoveY)
        {
            return
                Vector3.UnitY *
                (
                    -(float)deltaPixelY *
                    worldPerPixel
                );
        }

        var forward =
            target -
            cameraPosition;

        forward.Y = 0;

        if (
            forward.LengthSquared() <
            0.000001f)
        {
            forward =
                Vector3.UnitZ;
        }
        else
        {
            forward =
                Vector3.Normalize(
                    forward);
        }

        var right =
            Vector3.Normalize(
                Vector3.Cross(
                    Vector3.UnitY,
                    forward));

        var cameraPlaneDelta =
            right *
            (
                (float)deltaPixelX *
                worldPerPixel
            ) -
            forward *
            (
                (float)deltaPixelY *
                worldPerPixel
            );

        if (
            handle ==
                NativeGizmoHandle.MoveXZ)
        {
            return cameraPlaneDelta;
        }

        var axis =
            handle switch
            {
                NativeGizmoHandle.MoveX =>
                    Vector3.UnitX,
                NativeGizmoHandle.MoveZ =>
                    Vector3.UnitZ,
                _ =>
                    Vector3.Zero
            };

        return
            axis *
            Vector3.Dot(
                cameraPlaneDelta,
                axis);
    }

    public static float GetRotationDeltaDegrees(
        double deltaPixelX,
        double deltaPixelY) =>
        (float)(
            (
                deltaPixelX -
                deltaPixelY
            ) *
            0.35);

    public static Vector3 SnapTranslation(
        Vector3 translation,
        float increment)
    {
        if (
            increment <=
            0)
        {
            return translation;
        }

        float Snap(
            float value) =>
            MathF.Round(
                value /
                increment) *
            increment;

        return new Vector3(
            Snap(
                translation.X),
            Snap(
                translation.Y),
            Snap(
                translation.Z));
    }

    public static float SnapRotation(
        float degrees,
        float increment)
    {
        if (
            increment <=
            0)
        {
            return degrees;
        }

        return
            MathF.Round(
                degrees /
                increment) *
            increment;
    }

    public static Matrix4x4
        CreateRotationPreview(
            NativeGizmoHandle handle,
            Vector3 anchor,
            float degrees)
    {
        var radians =
            degrees *
            MathF.PI /
            180.0f;

        var rotation =
            handle switch
            {
                NativeGizmoHandle.RotateX =>
                    Matrix4x4
                        .CreateRotationX(
                            radians),
                NativeGizmoHandle.RotateY =>
                    Matrix4x4
                        .CreateRotationY(
                            radians),
                NativeGizmoHandle.RotateZ =>
                    Matrix4x4
                        .CreateRotationZ(
                            radians),
                _ =>
                    Matrix4x4.Identity
            };

        return
            Matrix4x4
                .CreateTranslation(
                    -anchor) *
            rotation *
            Matrix4x4
                .CreateTranslation(
                    anchor);
    }
}
