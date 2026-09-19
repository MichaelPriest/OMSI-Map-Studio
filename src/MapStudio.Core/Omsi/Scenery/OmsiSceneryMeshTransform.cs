namespace MapStudio.Core.Omsi.Scenery;

public sealed record OmsiSceneryMeshTransform(
    double PositionX,
    double PositionY,
    double PositionZ,
    double RotationX,
    double RotationY,
    double RotationZ,
    double ScaleX,
    double ScaleY,
    double ScaleZ)
{
    public static OmsiSceneryMeshTransform
        Identity { get; } =
            new(
                PositionX: 0,
                PositionY: 0,
                PositionZ: 0,
                RotationX: 0,
                RotationY: 0,
                RotationZ: 0,
                ScaleX: 1,
                ScaleY: 1,
                ScaleZ: 1);
}
