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

            var materialNightTexturePaths =
                new string?[
                    geometry.Materials.Count];

            var materialLightTexturePaths =
                new string?[
                    geometry.Materials.Count];

            var materialTransMapTexturePaths =
                new string?[
                    geometry.Materials.Count];

            var materialBumpTexturePaths =
                new string?[
                    geometry.Materials.Count];

            var materialBumpStrengths =
                new double?[
                    geometry.Materials.Count];

            var materialEnvironmentTexturePaths =
                new string?[
                    geometry.Materials.Count];

            var materialEnvironmentStrengths =
                new double?[
                    geometry.Materials.Count];

            var materialAlphaModes =
                new int?[
                    geometry.Materials.Count];

            var materialNoZWriteFlags =
                new bool[
                    geometry.Materials.Count];

            var materialNoZCheckFlags =
                new bool[
                    geometry.Materials.Count];

            foreach (
                var materialOverride in
                    metadata.MaterialOverrides
                        .Where(
                            item =>
                                item.MeshOrdinal ==
                                    index))
            {
                var materialIndex =
                    ResolveMaterialIndex(
                        geometry,
                        materialOverride);

                if (materialIndex < 0)
                {
                    continue;
                }

                materialAlphaModes[
                    materialIndex] =
                    materialOverride
                        .AlphaMode;

                materialNoZWriteFlags[
                    materialIndex] =
                    materialOverride
                        .NoZWrite;

                materialNoZCheckFlags[
                    materialIndex] =
                    materialOverride
                        .NoZCheck;

                if (
                    !string.IsNullOrWhiteSpace(
                        materialOverride
                            .TransMapSource) &&
                    OmsiTextureAssetPathResolver
                        .TryResolveSceneryTexture(
                            omsiRoot,
                            fullScoPath,
                            meshPath,
                            materialOverride
                                .TransMapSource!,
                            out var transMapTexturePath))
                {
                    materialTransMapTexturePaths[
                        materialIndex] =
                        transMapTexturePath;
                }

                if (
                    !string.IsNullOrWhiteSpace(
                        materialOverride
                            .BumpMapTextureName) &&
                    OmsiTextureAssetPathResolver
                        .TryResolveSceneryTexture(
                            omsiRoot,
                            fullScoPath,
                            meshPath,
                            materialOverride
                                .BumpMapTextureName!,
                            out var bumpTexturePath))
                {
                    materialBumpTexturePaths[
                        materialIndex] =
                        bumpTexturePath;
                }

                materialBumpStrengths[
                    materialIndex] =
                    materialOverride
                        .BumpMapStrength;

                if (
                    !string.IsNullOrWhiteSpace(
                        materialOverride
                            .EnvironmentMapTextureName) &&
                    OmsiTextureAssetPathResolver
                        .TryResolveSceneryTexture(
                            omsiRoot,
                            fullScoPath,
                            meshPath,
                            materialOverride
                                .EnvironmentMapTextureName!,
                            out var environmentTexturePath))
                {
                    materialEnvironmentTexturePaths[
                        materialIndex] =
                        environmentTexturePath;
                }

                materialEnvironmentStrengths[
                    materialIndex] =
                    materialOverride
                        .EnvironmentMapStrength;

                if (
                    !string.IsNullOrWhiteSpace(
                        materialOverride
                            .NightMapTextureName) &&
                    OmsiTextureAssetPathResolver
                        .TryResolveSceneryTexture(
                            omsiRoot,
                            fullScoPath,
                            meshPath,
                            materialOverride
                                .NightMapTextureName!,
                            out var nightTexturePath))
                {
                    materialNightTexturePaths[
                        materialIndex] =
                        nightTexturePath;
                }

                if (
                    !string.IsNullOrWhiteSpace(
                        materialOverride
                            .LightMapTextureName) &&
                    OmsiTextureAssetPathResolver
                        .TryResolveSceneryTexture(
                            omsiRoot,
                            fullScoPath,
                            meshPath,
                            materialOverride
                                .LightMapTextureName!,
                            out var lightTexturePath))
                {
                    materialLightTexturePaths[
                        materialIndex] =
                        lightTexturePath;
                }
            }

            meshes.Add(
                new NativeSceneryMeshAsset(
                    declaredPath,
                    meshPath,
                    transform,
                    lodThreshold,
                    geometry,
                    materialTexturePaths,
                    materialNightTexturePaths,
                    materialLightTexturePaths,
                    materialAlphaModes,
                    materialNoZWriteFlags,
                    materialNoZCheckFlags,
                    materialTransMapTexturePaths,
                    materialBumpTexturePaths,
                    materialBumpStrengths,
                    materialEnvironmentTexturePaths,
                    materialEnvironmentStrengths));
        }

        string? treeTexturePath =
            null;

        if (
            metadata.Tree is
                { } tree &&
            OmsiTextureAssetPathResolver
                .TryResolveSceneryObjectTexture(
                    omsiRoot,
                    fullScoPath,
                    tree.TextureName,
                    out var resolvedTreeTexture))
        {
            treeTexturePath =
                resolvedTreeTexture;
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
                : null,
            treeTexturePath,
            metadata.RenderType);
    }

    private static int ResolveMaterialIndex(
        OmsiO3dGeometry geometry,
        OmsiSceneryMaterialOverride
            materialOverride)
    {
        if (materialOverride.MaterialIndex < 0)
        {
            return -1;
        }

        var matches =
            geometry.Materials
                .Select(
                    (material, index) =>
                        new
                        {
                            material,
                            index
                        })
                .Where(
                    item =>
                        MaterialTextureMatches(
                            item.material
                                .TextureName,
                            materialOverride
                                .TextureName))
                .Select(
                    item =>
                        item.index)
                .ToArray();

        if (
            materialOverride.MaterialIndex <
                matches.Length)
        {
            return matches[
                materialOverride
                    .MaterialIndex];
        }

        // Compatibility fallback for unusual legacy assets
        // whose material has no texture name in the mesh.
        if (
            matches.Length == 0 &&
            materialOverride.MaterialIndex <
                geometry.Materials.Count)
        {
            return materialOverride
                .MaterialIndex;
        }

        return -1;
    }

    private static bool MaterialTextureMatches(
        string? meshTexture,
        string overrideTexture)
    {
        if (
            string.IsNullOrWhiteSpace(
                meshTexture) ||
            string.IsNullOrWhiteSpace(
                overrideTexture))
        {
            return false;
        }

        static string Normalize(
            string value) =>
            value
                .Trim()
                .Replace(
                    '/',
                    '\\');

        var left =
            Normalize(meshTexture);

        var right =
            Normalize(overrideTexture);

        return
            string.Equals(
                left,
                right,
                StringComparison
                    .OrdinalIgnoreCase) ||
            string.Equals(
                Path.GetFileName(left),
                Path.GetFileName(right),
                StringComparison
                    .OrdinalIgnoreCase);
    }
}
