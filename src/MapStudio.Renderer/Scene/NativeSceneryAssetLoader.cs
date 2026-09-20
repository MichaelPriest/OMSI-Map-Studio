using MapStudio.Core.Omsi.Models;
using MapStudio.Core.Omsi.Scenery;
using MapStudio.Core.Omsi.Textures;

namespace MapStudio.Renderer.Scene;

public sealed class NativeSceneryAssetLoader
{
    private readonly OmsiSceneryObjectReader
        _sceneryReader =
            new();

    private readonly OmsiO3dGeometryReader
        _o3dReader =
            new();

    private readonly OmsiDirectXTextGeometryReader
        _directXReader =
            new();

    public async Task<
        IReadOnlyDictionary<
            string,
            NativeSceneryAsset>>
        LoadAsync(
            string omsiRoot,
            NativeSceneSnapshot scene,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            omsiRoot);

        ArgumentNullException.ThrowIfNull(
            scene);

        var result =
            new Dictionary<
                string,
                NativeSceneryAsset>(
                StringComparer
                    .OrdinalIgnoreCase);

        var paths =
            scene.Objects
                .Select(
                    item =>
                        item.Object
                            .SceneryObjectPath)
                .Distinct(
                    StringComparer
                        .OrdinalIgnoreCase)
                .ToArray();

        foreach (var path in paths)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            result[path] =
                await LoadOneAsync(
                    omsiRoot,
                    path,
                    cancellationToken)
                    .ConfigureAwait(false);
        }

        return result;
    }

    public Task<NativeSceneryAsset>
        LoadAssetAsync(
            string omsiRoot,
            string sceneryObjectPath,
            CancellationToken cancellationToken =
                default) =>
            LoadOneAsync(
                omsiRoot,
                sceneryObjectPath,
                cancellationToken);

    private async Task<NativeSceneryAsset>
        LoadOneAsync(
            string omsiRoot,
            string sceneryObjectPath,
            CancellationToken cancellationToken)
    {
        if (
            !OmsiSceneryObjectPathResolver
                .TryResolve(
                    omsiRoot,
                    sceneryObjectPath,
                    out var fullScoPath))
        {
            return new NativeSceneryAsset(
                sceneryObjectPath,
                null,
                Array.Empty<
                    NativeSceneryMeshAsset>(),
                null,
                false,
                "scoPathInvalid");
        }

        var metadata =
            await _sceneryReader
                .ReadMetadataAsync(
                    fullScoPath,
                    cancellationToken)
                .ConfigureAwait(false);

        if (!metadata.Exists)
        {
            return new NativeSceneryAsset(
                sceneryObjectPath,
                fullScoPath,
                Array.Empty<
                    NativeSceneryMeshAsset>(),
                null,
                false,
                "scoMissing");
        }

        var meshes =
            new List<
                NativeSceneryMeshAsset>();

        for (
            var index = 0;
            index <
                metadata.MeshPaths.Count;
            index++)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            var declaredPath =
                metadata.MeshPaths[index];

            if (
                !OmsiSceneryMeshPathResolver
                    .TryResolve(
                        omsiRoot,
                        fullScoPath,
                        declaredPath,
                        out var meshPath))
            {
                continue;
            }

            var geometry =
                string.Equals(
                    Path.GetExtension(
                        meshPath),
                    ".x",
                    StringComparison
                        .OrdinalIgnoreCase)
                    ? _directXReader.Read(
                        meshPath)
                    : _o3dReader.Read(
                        meshPath);

            if (!geometry.IsLoaded)
            {
                continue;
            }

            var transform =
                index <
                    metadata
                        .MeshTransforms
                        .Count
                    ? metadata
                        .MeshTransforms[
                            index]
                    : OmsiSceneryMeshTransform
                        .Identity;

            var lodThreshold =
                index <
                    metadata
                        .MeshLodThresholds
                        .Count
                    ? metadata
                        .MeshLodThresholds[
                            index]
                    : null;

            var materialTexturePaths =
                geometry.Materials
                    .Select(
                        material =>
                        {
                            if (
                                string.IsNullOrWhiteSpace(
                                    material.TextureName))
                            {
                                return null;
                            }

                            return OmsiTextureAssetPathResolver
                                .TryResolveSceneryTexture(
                                    omsiRoot,
                                    fullScoPath,
                                    meshPath,
                                    material.TextureName,
                                    out var texturePath)
                                ? texturePath
                                : null;
                        })
                    .ToArray();

            meshes.Add(
                new NativeSceneryMeshAsset(
                    declaredPath,
                    meshPath,
                    transform,
                    lodThreshold,
                    geometry,
                    materialTexturePaths));
        }

        return new NativeSceneryAsset(
            sceneryObjectPath,
            fullScoPath,
            meshes,
            metadata.Tree,
            metadata.UsesAbsoluteHeight,
            meshes.Count == 0 &&
            metadata.Tree is null
                ? "noRenderableMeshes"
                : null);
    }
}
