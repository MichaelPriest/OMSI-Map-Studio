using MapStudio.Core.Omsi.Maps;
using MapStudio.Renderer.Picking;

namespace MapStudio.Renderer.Viewport;

/// <summary>
/// Validates user-entered and staged object/spline transforms before they can
/// mutate the live scene. Persistence performs its own validation as a second
/// boundary.
/// </summary>
public static class NativeTransformEditValidator
{
    public static bool IsFinite(NativeSelectionInfo values)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (!AllFinite(values.X, values.Y, values.Z, values.Rotation) ||
            !IsFiniteIfPresent(values.Pitch) ||
            !IsFiniteIfPresent(values.Bank) ||
            !IsFiniteIfPresent(values.Length) ||
            !IsFiniteIfPresent(values.Radius) ||
            !IsFiniteIfPresent(values.GradientStart) ||
            !IsFiniteIfPresent(values.GradientEnd))
        {
            return false;
        }

        return values.Kind switch
        {
            PickingKind.Object =>
                IsFiniteIfPresent(values.Pitch) &&
                IsFiniteIfPresent(values.Bank),
            PickingKind.Spline =>
                IsFiniteIfPresent(values.Length) &&
                IsFiniteIfPresent(values.Radius) &&
                IsFiniteIfPresent(values.GradientStart) &&
                IsFiniteIfPresent(values.GradientEnd),
            _ => true
        };
    }

    public static bool AreFinite(
        IReadOnlyList<NativePendingTransformEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(edits);

        return edits.All(edit =>
            edit is not null &&
            (edit.ObjectEdit is null) != (edit.SplineEdit is null) &&
            (edit.ObjectEdit is { } objectEdit
                ? IsFinite(objectEdit)
                : IsFinite(edit.SplineEdit!)));
    }

    public static bool IsFinite(OmsiObjectTransformEdit edit)
    {
        ArgumentNullException.ThrowIfNull(edit);

        return edit.SourceSectionOrdinal >= 0 &&
            edit.ObjectId >= 0 &&
            !string.IsNullOrWhiteSpace(edit.SceneryObjectPath) &&
            AllFinite(edit.X, edit.Y, edit.Z, edit.Rotation, edit.Pitch, edit.Bank);
    }

    public static bool IsFinite(OmsiSplineTransformEdit edit)
    {
        ArgumentNullException.ThrowIfNull(edit);

        return edit.SourceSectionOrdinal >= 0 &&
            edit.SplineId >= 0 &&
            !string.IsNullOrWhiteSpace(edit.SplinePath) &&
            AllFinite(
                edit.X,
                edit.Y,
                edit.Z,
                edit.Rotation,
                edit.Length,
                edit.Radius,
                edit.GradientStart,
                edit.GradientEnd);
    }

    private static bool AllFinite(params double[] values) =>
        values.All(double.IsFinite);

    private static bool IsFiniteIfPresent(double? value) =>
        value is null || double.IsFinite(value.Value);
}
