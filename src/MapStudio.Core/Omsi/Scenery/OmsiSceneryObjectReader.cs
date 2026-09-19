using System.Globalization;
using MapStudio.Core.Omsi.Config;

namespace MapStudio.Core.Omsi.Scenery;

public sealed class OmsiSceneryObjectReader
{
    public async Task<OmsiSceneryObjectMetadata> ReadMetadataAsync(
        string sceneryObjectPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sceneryObjectPath);

        if (!File.Exists(sceneryObjectPath))
        {
            return OmsiSceneryObjectMetadata.Missing;
        }

        var document = await OmsiConfigParser.ParseFileAsync(
            sceneryObjectPath,
            cancellationToken);

        return ReadMetadata(document);
    }

    public static OmsiSceneryObjectMetadata ReadMetadata(
        OmsiConfigDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var friendlyName = document
            .FindFirstSection("friendlyname")
            ?.DataLines
            .FirstOrDefault();

        var groups = ReadGroups(document);
        var meshes = ReadSingleValueSections(document, "mesh");
        var collisionMeshes = ReadSingleValueSections(
            document,
            "collision_mesh");

        var materialOverrides =
            ReadMaterialOverrides(
                document);

        return new OmsiSceneryObjectMetadata(
            Exists: true,
            FriendlyName: friendlyName,
            Groups: groups,
            MeshPaths: meshes,
            CollisionMeshPaths: collisionMeshes,
            MaterialOverrides:
                materialOverrides);
    }

    private static IReadOnlyList<
        OmsiSceneryMaterialOverride>
        ReadMaterialOverrides(
            OmsiConfigDocument document)
    {
        var result =
            new List<MaterialOverrideBuilder>();

        var meshOrdinal = -1;

        MaterialOverrideBuilder?
            current = null;

        foreach (var section in
            document.Sections)
        {
            if (string.Equals(
                    section.Keyword,
                    "mesh",
                    StringComparison.OrdinalIgnoreCase))
            {
                meshOrdinal++;
                current = null;
                continue;
            }

            if (string.Equals(
                    section.Keyword,
                    "matl_change",
                    StringComparison.OrdinalIgnoreCase))
            {
                current = null;
                continue;
            }

            if (string.Equals(
                    section.Keyword,
                    "matl",
                    StringComparison.OrdinalIgnoreCase))
            {
                current = null;

                if (meshOrdinal < 0)
                {
                    continue;
                }

                var values =
                    section.DataLines
                        .ToArray();

                if (
                    values.Length < 2 ||
                    string.IsNullOrWhiteSpace(
                        values[0]) ||
                    !int.TryParse(
                        values[1],
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var materialIndex) ||
                    materialIndex < 0)
                {
                    continue;
                }

                current =
                    new MaterialOverrideBuilder(
                        meshOrdinal,
                        values[0],
                        materialIndex);

                result.Add(current);
                continue;
            }

            if (current is null)
            {
                continue;
            }

            if (string.Equals(
                    section.Keyword,
                    "matl_alpha",
                    StringComparison.OrdinalIgnoreCase))
            {
                var value =
                    section.DataLines
                        .FirstOrDefault();

                if (
                    int.TryParse(
                        value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var alphaMode) &&
                    alphaMode is >= 0 and <= 2)
                {
                    current.AlphaMode =
                        alphaMode;
                }

                continue;
            }

            if (string.Equals(
                    section.Keyword,
                    "matl_bumpmap",
                    StringComparison.OrdinalIgnoreCase))
            {
                var values =
                    section.DataLines
                        .ToArray();

                if (
                    values.Length > 0 &&
                    !string.IsNullOrWhiteSpace(
                        values[0]))
                {
                    current.BumpMapTextureName =
                        values[0];
                }

                if (
                    values.Length > 1 &&
                    double.TryParse(
                        values[1],
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var strength) &&
                    double.IsFinite(strength))
                {
                    current.BumpMapStrength =
                        strength;
                }

                continue;
            }

            if (string.Equals(
                    section.Keyword,
                    "matl_noZwrite",
                    StringComparison.OrdinalIgnoreCase))
            {
                current.NoZWrite = true;
                continue;
            }

            if (string.Equals(
                    section.Keyword,
                    "matl_noZcheck",
                    StringComparison.OrdinalIgnoreCase))
            {
                current.NoZCheck = true;
            }
        }

        return result
            .Select(
                builder =>
                    new OmsiSceneryMaterialOverride(
                        builder.MeshOrdinal,
                        builder.TextureName,
                        builder.MaterialIndex,
                        builder.AlphaMode,
                        builder.NoZWrite,
                        builder.NoZCheck,
                        builder.BumpMapTextureName,
                        builder.BumpMapStrength))
            .ToArray();
    }

    private static IReadOnlyList<string> ReadGroups(
        OmsiConfigDocument document)
    {
        var values = document
            .FindFirstSection("groups")
            ?.DataLines
            .ToArray();

        if (values is null ||
            values.Length == 0 ||
            !int.TryParse(
                values[0],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var count) ||
            count <= 0)
        {
            return Array.Empty<string>();
        }

        return values
            .Skip(1)
            .Take(count)
            .ToArray();
    }

    private static IReadOnlyList<string> ReadSingleValueSections(
        OmsiConfigDocument document,
        string keyword) =>
        document
            .FindSections(keyword)
            .Select(section => section.DataLines.FirstOrDefault())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!)
            .ToArray();

    private sealed class MaterialOverrideBuilder(
        int meshOrdinal,
        string textureName,
        int materialIndex)
    {
        public int MeshOrdinal { get; } =
            meshOrdinal;

        public string TextureName { get; } =
            textureName;

        public int MaterialIndex { get; } =
            materialIndex;

        public int? AlphaMode { get; set; }

        public bool NoZWrite { get; set; }

        public bool NoZCheck { get; set; }

        public string?
            BumpMapTextureName
        { get; set; }

        public double?
            BumpMapStrength
        { get; set; }
    }
}
