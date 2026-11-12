using System.Numerics;
using MapStudio.Renderer.Scene;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeObjectVisualVariantTests
{
    [Fact]
    public void OriginalVariantDoesNotOverrideTexturesOrTint()
    {
        Assert.Empty(
            NativeObjectVisualVariant.Original.MaterialTextureOverrides);

        var source = new Vector4(0.4f, 0.6f, 0.8f, 1f);
        Assert.Equal(
            source,
            NativeObjectVisualVariant.ApplyTint(
                source,
                NativeObjectVisualVariant.Original.ColorTint));
    }

    [Fact]
    public void TintMultipliesMaterialColorWithoutChangingAlphaByDefault()
    {
        var source = new Vector4(0.5f, 0.75f, 0.25f, 1f);
        var tint = new Vector4(0.5f, 1f, 0.4f, 1f);

        var actual = NativeObjectVisualVariant.ApplyTint(source, tint);

        Assert.Equal(new Vector4(0.25f, 0.75f, 0.1f, 1f), actual);
    }

    [Fact]
    public void InvalidTintIsIgnoredRatherThanProducingInvalidVertices()
    {
        var source = new Vector4(0.4f, 0.6f, 0.8f, 1f);
        var invalid = new Vector4(float.NaN, 1f, 1f, 1f);

        Assert.Equal(source, NativeObjectVisualVariant.ApplyTint(source, invalid));
    }

    [Fact]
    public void ExistingTextureCanBeOverriddenForOneMaterial()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"map-studio-variant-{Guid.NewGuid():N}.bmp");

        try
        {
            File.WriteAllBytes(path, [0x42, 0x4D]);
            var variant = new NativeObjectVisualVariant(
                "Blue theme",
                new Dictionary<int, string> { [2] = path });

            Assert.True(variant.TryGetTextureOverride(2, out var actual));
            Assert.Equal(Path.GetFullPath(path), actual);
            Assert.False(variant.TryGetTextureOverride(1, out _));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void MissingTextureFallsBackToTheOriginalMaterialTexture()
    {
        var variant = new NativeObjectVisualVariant(
            "Missing texture",
            new Dictionary<int, string>
            {
                [0] = Path.Combine(
                    Path.GetTempPath(),
                    $"missing-{Guid.NewGuid():N}.bmp")
            });

        Assert.False(variant.TryGetTextureOverride(0, out _));
    }
}
