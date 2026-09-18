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

        return new OmsiSceneryObjectMetadata(
            Exists: true,
            FriendlyName: friendlyName,
            Groups: groups,
            MeshPaths: meshes,
            CollisionMeshPaths: collisionMeshes);
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
}
