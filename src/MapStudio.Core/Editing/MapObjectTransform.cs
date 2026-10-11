using System.Numerics;

namespace MapStudio.Core.Editing;

/// <summary>
/// Position, Euler rotation (degrees), and scale for a placed map object.
/// Coordinates are stored in OMSI world units; conversion to renderer coordinates
/// belongs at the renderer boundary, not in edit history.
/// </summary>
public readonly record struct MapObjectTransform(
    Vector3 Position,
    Vector3 RotationDegrees,
    Vector3 Scale)
{
    public static MapObjectTransform Identity { get; } =
        new(Vector3.Zero, Vector3.Zero, Vector3.One);

    public bool IsFinite =>
        IsFiniteVector(Position) &&
        IsFiniteVector(RotationDegrees) &&
        IsFiniteVector(Scale);

    private static bool IsFiniteVector(Vector3 value) =>
        float.IsFinite(value.X) &&
        float.IsFinite(value.Y) &&
        float.IsFinite(value.Z);
}

/// <summary>
/// Minimal mutable contract implemented by a real map-object document/viewport
/// adapter. The target must apply the complete transform as one atomic update.
/// </summary>
public interface IMapObjectTransformTarget
{
    string ObjectId { get; }

    MapObjectTransform Transform { get; set; }
}

/// <summary>
/// Reversible transform of one existing map object. Captures both states so
/// undo/redo never depend on the current selection or current gizmo state.
/// </summary>
public sealed class MapObjectTransformOperation : IEditOperation
{
    private readonly IMapObjectTransformTarget _target;
    private readonly MapObjectTransform _before;
    private readonly MapObjectTransform _after;

    public MapObjectTransformOperation(
        IMapObjectTransformTarget target,
        MapObjectTransform before,
        MapObjectTransform after)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (string.IsNullOrWhiteSpace(target.ObjectId))
        {
            throw new ArgumentException("The map object must have a stable identifier.", nameof(target));
        }

        if (!before.IsFinite)
        {
            throw new ArgumentOutOfRangeException(nameof(before), "The original transform must contain finite values.");
        }

        if (!after.IsFinite)
        {
            throw new ArgumentOutOfRangeException(nameof(after), "The new transform must contain finite values.");
        }

        _target = target;
        _before = before;
        _after = after;
        Description = $"Transform object {_target.ObjectId}";
    }

    public string Description { get; }

    public void Undo() => _target.Transform = _before;

    public void Redo() => _target.Transform = _after;
}
