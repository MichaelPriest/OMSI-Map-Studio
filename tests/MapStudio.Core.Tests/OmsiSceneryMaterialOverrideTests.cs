using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Scenery;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiSceneryMaterialOverrideTests
{
    [Fact]
    public void ReadMetadata_AssociatesStaticMaterialOverridesWithMeshOrdinal()
    {
        const string source =
            "[mesh]\n" +
            "model\\glass.o3d\n" +
            "[matl]\n" +
            "glass.dds\n" +
            "1\n" +
            "[matl_alpha]\n" +
            "2\n" +
            "[matl_transmap]\n" +
            "\\S:1\n" +
            "[matl_lightmap]\n" +
            "glass_light.dds\n" +
            "[matl_envmap_mask]\n" +
            "envmask.bmp\n" +
            "[alphascale]\n" +
            "alpha_var\n" +
            "[matl_allcolor]\n" +
            "1\n0.5\n0.25\n1\n" +
            "0\n0\n0\n0\n0\n0\n0\n0\n0\n1\n" +
            "[matl_envmap]\n" +
            "envmap.bmp\n" +
            "0.4\n" +
            "[matl_nightmap]\n" +
            "glass_night.dds\n" +
            "[matl_bumpmap]\n" +
            "glass_bump.bmp\n" +
            "0.05\n" +
            "[matl_noZwrite]\n" +
            "[matl_noZcheck]\n" +
            "[mesh]\n" +
            "model\\tree.o3d\n" +
            "[matl]\n" +
            "leaf.tga\n" +
            "0\n" +
            "[matl_alpha]\n" +
            "1\n";

        var document =
            OmsiConfigParser.Parse(
                source);

        var metadata =
            OmsiSceneryObjectReader
                .ReadMetadata(
                    document);

        Assert.Equal(
            2,
            metadata.MaterialOverrides.Count);

        var glass =
            metadata.MaterialOverrides[0];

        Assert.Equal(0, glass.MeshOrdinal);
        Assert.Equal("glass.dds", glass.TextureName);
        Assert.Equal(1, glass.MaterialIndex);
        Assert.Equal(2, glass.AlphaMode);
        Assert.True(glass.NoZWrite);
        Assert.True(glass.NoZCheck);
        Assert.Equal(
            "glass_bump.bmp",
            glass.BumpMapTextureName);
        Assert.Equal(
            0.05,
            glass.BumpMapStrength);
        Assert.Equal(
            "glass_night.dds",
            glass.NightMapTextureName);
        Assert.Equal(
            "envmap.bmp",
            glass.EnvironmentMapTextureName);
        Assert.Equal(
            0.4,
            glass.EnvironmentMapStrength);
        Assert.Equal(
            @"\S:1",
            glass.TransMapSource);
        Assert.Equal(
            "glass_light.dds",
            glass.LightMapTextureName);
        Assert.Equal(
            [
                "[matl_envmap_mask]",
                "[alphascale]",
                "[matl_allcolor]"
            ],
            glass.UnsupportedCommands);

        var leaf =
            metadata.MaterialOverrides[1];

        Assert.Equal(1, leaf.MeshOrdinal);
        Assert.Equal("leaf.tga", leaf.TextureName);
        Assert.Equal(0, leaf.MaterialIndex);
        Assert.Equal(1, leaf.AlphaMode);
        Assert.False(leaf.NoZWrite);
        Assert.False(leaf.NoZCheck);
        Assert.Null(
            leaf.BumpMapTextureName);
        Assert.Null(
            leaf.BumpMapStrength);
        Assert.Null(
            leaf.NightMapTextureName);
        Assert.Null(
            leaf.EnvironmentMapTextureName);
        Assert.Null(
            leaf.EnvironmentMapStrength);
        Assert.Null(
            leaf.TransMapSource);
        Assert.Null(
            leaf.LightMapTextureName);
        Assert.Empty(
            leaf.UnsupportedCommands);
    }

    [Fact]
    public void ReadMetadata_PreservesLodThresholdsByMeshOrder()
    {
        const string source =
            "[mesh]\n" +
            "model\\always.o3d\n" +
            "[LOD]\n" +
            "0.6\n" +
            "[mesh]\n" +
            "model\\near.o3d\n" +
            "[LOD]\n" +
            "0.2\n" +
            "[mesh]\n" +
            "model\\medium.o3d\n" +
            "[LOD]\n" +
            "0\n" +
            "[mesh]\n" +
            "model\\far.o3d\n";

        var metadata =
            OmsiSceneryObjectReader
                .ReadMetadata(
                    OmsiConfigParser.Parse(
                        source));

        Assert.Equal(
            [
                @"model\always.o3d",
                @"model\near.o3d",
                @"model\medium.o3d",
                @"model\far.o3d"
            ],
            metadata.MeshPaths);

        Assert.Equal(
            new double?[]
            {
                null,
                0.6,
                0.2,
                0.0
            },
            metadata.MeshLodThresholds);
    }

    [Fact]
    public void ReadMetadata_InvalidLodDoesNotReusePreviousThreshold()
    {
        const string source =
            "[LOD]\n" +
            "0.4\n" +
            "[mesh]\n" +
            "model\\near.o3d\n" +
            "[LOD]\n" +
            "invalid\n" +
            "[mesh]\n" +
            "model\\global.o3d\n";

        var metadata =
            OmsiSceneryObjectReader
                .ReadMetadata(
                    OmsiConfigParser.Parse(
                        source));

        Assert.Equal(
            new double?[]
            {
                0.4,
                null
            },
            metadata.MeshLodThresholds);
    }

    [Fact]
    public void ReadMetadata_DoesNotTreatDynamicMaterialChangeAsStaticOverride()
    {
        const string source =
            "[mesh]\n" +
            "model\\display.o3d\n" +
            "[matl]\n" +
            "base.dds\n" +
            "0\n" +
            "[matl_change]\n" +
            "dynamic.dds\n" +
            "0\n" +
            "display_var\n" +
            "[matl_alpha]\n" +
            "2\n";

        var document =
            OmsiConfigParser.Parse(
                source);

        var metadata =
            OmsiSceneryObjectReader
                .ReadMetadata(
                    document);

        var material =
            Assert.Single(
                metadata.MaterialOverrides);

        Assert.Equal(
            "base.dds",
            material.TextureName);

        Assert.Null(
            material.AlphaMode);
    }

    [Fact]
    public void ReadMetadata_IgnoresInvalidAlphaMode()
    {
        const string source =
            "[mesh]\n" +
            "model\\sign.o3d\n" +
            "[matl]\n" +
            "sign.dds\n" +
            "0\n" +
            "[matl_alpha]\n" +
            "7\n";

        var metadata =
            OmsiSceneryObjectReader
                .ReadMetadata(
                    OmsiConfigParser.Parse(
                        source));

        var material =
            Assert.Single(
                metadata.MaterialOverrides);

        Assert.Null(
            material.AlphaMode);
    }
}
