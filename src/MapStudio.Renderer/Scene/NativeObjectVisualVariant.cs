using System.Numerics;

namespace MapStudio.Renderer.Scene;

/// <summary>
/// Identifies one placed scenery object, not the shared source asset.
/// Tile coordinates keep equal object IDs in different tiles independent.
/// </summary>
public readonly record struct NativeObjectVisualVariantKey(
    int TileX,
    int TileY,
    int ObjectId)
{
    public static NativeObjectVisualVariantKey From(
        NativeObjectEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new NativeObjectVisualVariantKey(
            entity.Tile.X,
            entity.Tile.Y,
            entity.Object.ObjectId);
    }
}

/// <summary>
/// Non-destructive visual overrides for a single placed object.
/// Texture paths must point to existing image files; original package files
/// are read-only and are never rewritten by this type.
/// </summary>
public sealed record NativeObjectVisualVariant(
    string Name,
    IReadOnlyDictionary<int, string> MaterialTextureOverrides,
    Vector4? ColorTint = null)
{
    private static readonly HashSet<string> SupportedTextureExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".bmp", ".dds", ".png", ".jpg", ".jpeg", ".tga"
        };

    public static NativeObjectVisualVariant Original { get; } =
        new(
            "Original",
            new Dictionary<int, string>());

    public bool TryGetTextureOverride(
        int? materialIndex,
        out string texturePath)
    {
        texturePath = string.Empty;

        if (
            materialIndex is not int index ||
            index < 0 ||
            !MaterialTextureOverrides.TryGetValue(index, out var candidate) ||
            string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        var fullPath = Path.GetFullPath(candidate);
        var extension = Path.GetExtension(fullPath);
        if (
            !SupportedTextureExtensions.Contains(extension) ||
            !File.Exists(fullPath))
        {
            return false;
        }

        texturePath = fullPath;
        return true;
    }

    public static Vector4 ApplyTint(
        Vector4 materialColor,
        Vector4? tint)
    {
        if (tint is not Vector4 value || !IsValidTint(value))
        {
            return materialColor;
        }

        return new Vector4(
            Math.Clamp(materialColor.X * value.X, 0f, 1f),
            Math.Clamp(materialColor.Y * value.Y, 0f, 1f),
            Math.Clamp(materialColor.Z * value.Z, 0f, 1f),
            Math.Clamp(materialColor.W * value.W, 0f, 1f));
    }

    private static bool IsValidTint(Vector4 tint) =>
        float.IsFinite(tint.X) &&
        float.IsFinite(tint.Y) &&
        float.IsFinite(tint.Z) &&
        float.IsFinite(tint.W) &&
        tint.X is >= 0f and <= 1f &&
        tint.Y is >= 0f and <= 1f &&
        tint.Z is >= 0f and <= 1f &&
        tint.W is >= 0f and <= 1f;
}
