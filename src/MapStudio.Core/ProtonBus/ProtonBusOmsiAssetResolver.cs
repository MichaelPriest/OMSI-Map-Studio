using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Models;
using MapStudio.Core.Omsi.Scenery;
using MapStudio.Core.Omsi.Splines;
using MapStudio.Core.Omsi.Textures;

namespace MapStudio.Core.ProtonBus;

public sealed record ProtonBusResolvedTextureSource(
    string DeclaredName,
    string SourcePath,
    string TargetFileName,
    bool RequiresConversion,
    string OwnerPath);

public sealed record ProtonBusOmsiAssetIssue(
    string Code,
    string DeclaredPath,
    string? ResolvedPath = null,
    string? Detail = null);

public sealed record ProtonBusOmsiAssetResolutionResult(
    IReadOnlyDictionary<
        string,
        OmsiSplineDefinition>
        SplineDefinitions,
    IReadOnlyDictionary<
        string,
        ProtonBusResolvedSceneryAsset>
        SceneryAssets,
    IReadOnlyList<
        ProtonBusResolvedTextureSource>
        Textures,
    IReadOnlyList<
        ProtonBusOmsiAssetIssue>
        Issues)
{
    public bool HasBlockingIssues =>
        Issues.Any(
            issue =>
                issue.Code is
                    "splinePathInvalid" or
                    "splineMissing" or
                    "scoPathInvalid" or
                    "scoMissing" or
                    "meshPathInvalid" or
                    "meshMissing" or
                    "meshGeometryInvalid" or
                    "splineTextureMissing" or
                    "sceneryTextureMissing" or
                    "textureTargetCollision");
}

public sealed class ProtonBusOmsiAssetResolver
{
    private readonly OmsiSplineDefinitionReader
        _splineReader =
            new();

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
        ProtonBusOmsiAssetResolutionResult>
        ResolveAsync(
            string omsiRoot,
            OmsiTileContent content,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            omsiRoot);

        ArgumentNullException.ThrowIfNull(
            content);

        var root =
            Path.GetFullPath(
                omsiRoot);

        var splineDefinitions =
            new Dictionary<
                string,
                OmsiSplineDefinition>(
                    StringComparer
                        .OrdinalIgnoreCase);

        var sceneryAssets =
            new Dictionary<
                string,
                ProtonBusResolvedSceneryAsset>(
                    StringComparer
                        .OrdinalIgnoreCase);

        var textures =
            new List<
                ProtonBusResolvedTextureSource>();

        var issues =
            new List<
                ProtonBusOmsiAssetIssue>();

        var textureTargets =
            new Dictionary<
                string,
                string>(
                    StringComparer
                        .OrdinalIgnoreCase);

        foreach (
            var declaredPath
            in content.Splines
                .Select(
                    spline =>
                        spline.SplinePath)
                .Where(
                    path =>
                        !string.IsNullOrWhiteSpace(
                            path))
                .Distinct(
                    StringComparer
                        .OrdinalIgnoreCase))
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            if (
                !TryResolveSplinePath(
                    root,
                    declaredPath,
                    out var splinePath))
            {
                issues.Add(
                    new(
                        "splinePathInvalid",
                        declaredPath));

                continue;
            }

            if (
                !File.Exists(
                    splinePath))
            {
                issues.Add(
                    new(
                        "splineMissing",
                        declaredPath,
                        splinePath));

                continue;
            }

            var definition =
                await _splineReader
                    .ReadAsync(
                        splinePath,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (
                !definition.Exists)
            {
                issues.Add(
                    new(
                        "splineMissing",
                        declaredPath,
                        splinePath));

                continue;
            }

            splineDefinitions[
                declaredPath] =
                definition;

            for (
                var textureIndex = 0;
                textureIndex <
                    definition
                        .Textures
                        .Count;
                textureIndex++)
            {
                var textureName =
                    definition
                        .Textures[
                            textureIndex];

                if (
                    !OmsiTextureAssetPathResolver
                        .TryResolveSplineTexture(
                            root,
                            splinePath,
                            textureName,
                            out var texturePath))
                {
                    issues.Add(
                        new(
                            "splineTextureMissing",
                            textureName,
                            null,
                            declaredPath));

                    continue;
                }

                AddTexture(
                    textureName,
                    texturePath,
                    ProtonBusTextureNamePlanner
                        .ToPortablePngName(
                            textureName,
                            $"spline_{textureIndex}"),
                    declaredPath,
                    textures,
                    textureTargets,
                    issues);
            }
        }

        foreach (
            var declaredPath
            in content.Objects
                .Select(
                    placed =>
                        placed
                            .SceneryObjectPath)
                .Where(
                    path =>
                        !string.IsNullOrWhiteSpace(
                            path))
                .Distinct(
                    StringComparer
                        .OrdinalIgnoreCase))
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            if (
                !OmsiSceneryObjectPathResolver
                    .TryResolve(
                        root,
                        declaredPath,
                        out var fullScoPath))
            {
                issues.Add(
                    new(
                        "scoPathInvalid",
                        declaredPath));

                continue;
            }

            if (
                !File.Exists(
                    fullScoPath))
            {
                issues.Add(
                    new(
                        "scoMissing",
                        declaredPath,
                        fullScoPath));

                continue;
            }

            var metadata =
                await _sceneryReader
                    .ReadMetadataAsync(
                        fullScoPath,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (
                !metadata.Exists)
            {
                issues.Add(
                    new(
                        "scoMissing",
                        declaredPath,
                        fullScoPath));

                continue;
            }

            var resolvedMeshes =
                new List<
                    ProtonBusResolvedSceneryMesh>();

            var lodThresholds =
                metadata
                    .MeshLodThresholds
                    .Where(
                        value =>
                            value.HasValue)
                    .Select(
                        value =>
                            value!.Value)
                    .Distinct()
                    .OrderByDescending(
                        value =>
                            value)
                    .ToArray();

            double? selectedLod =
                lodThresholds.Length >
                    0
                    ? lodThresholds[0]
                    : null;

            for (
                var meshIndex = 0;
                meshIndex <
                    metadata.MeshPaths.Count;
                meshIndex++)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                var lod =
                    meshIndex <
                        metadata
                            .MeshLodThresholds
                            .Count
                        ? metadata
                            .MeshLodThresholds[
                                meshIndex]
                        : null;

                if (
                    lod.HasValue &&
                    selectedLod.HasValue &&
                    Math.Abs(
                        lod.Value -
                        selectedLod.Value) >
                    0.000001)
                {
                    continue;
                }

                var declaredMesh =
                    metadata.MeshPaths[
                        meshIndex];

                if (
                    !OmsiSceneryMeshPathResolver
                        .TryResolve(
                            root,
                            fullScoPath,
                            declaredMesh,
                            out var meshPath))
                {
                    issues.Add(
                        new(
                            "meshPathInvalid",
                            declaredMesh,
                            null,
                            declaredPath));

                    continue;
                }

                if (
                    !File.Exists(
                        meshPath))
                {
                    issues.Add(
                        new(
                            "meshMissing",
                            declaredMesh,
                            meshPath,
                            declaredPath));

                    continue;
                }

                var geometry =
                    ReadGeometry(
                        meshPath);

                if (
                    !geometry.IsLoaded)
                {
                    issues.Add(
                        new(
                            "meshGeometryInvalid",
                            declaredMesh,
                            meshPath,
                            geometry
                                .ErrorCode));

                    continue;
                }

                var transform =
                    meshIndex <
                        metadata
                            .MeshTransforms
                            .Count
                        ? metadata
                            .MeshTransforms[
                                meshIndex]
                        : OmsiSceneryMeshTransform
                            .Identity;

                resolvedMeshes.Add(
                    new(
                        geometry,
                        transform,
                        meshIndex));

                AddSceneryTextures(
                    root,
                    fullScoPath,
                    meshPath,
                    declaredPath,
                    geometry,
                    textures,
                    textureTargets,
                    issues);
            }

            for (
                var collisionIndex = 0;
                collisionIndex <
                    metadata
                        .CollisionMeshPaths
                        .Count;
                collisionIndex++)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                var declaredMesh =
                    metadata
                        .CollisionMeshPaths[
                            collisionIndex];

                if (
                    !OmsiSceneryMeshPathResolver
                        .TryResolve(
                            root,
                            fullScoPath,
                            declaredMesh,
                            out var meshPath))
                {
                    issues.Add(
                        new(
                            "meshPathInvalid",
                            declaredMesh,
                            null,
                            declaredPath));

                    continue;
                }

                if (
                    !File.Exists(
                        meshPath))
                {
                    issues.Add(
                        new(
                            "meshMissing",
                            declaredMesh,
                            meshPath,
                            declaredPath));

                    continue;
                }

                var geometry =
                    ReadGeometry(
                        meshPath);

                if (
                    !geometry.IsLoaded)
                {
                    issues.Add(
                        new(
                            "meshGeometryInvalid",
                            declaredMesh,
                            meshPath,
                            geometry
                                .ErrorCode));

                    continue;
                }

                resolvedMeshes.Add(
                    new(
                        geometry,
                        OmsiSceneryMeshTransform
                            .Identity,
                        metadata.MeshPaths.Count +
                            collisionIndex,
                        GenerateCollider:
                            true,
                        Invisible:
                            true));
            }

            if (
                metadata.Tree is
                    not null)
            {
                issues.Add(
                    new(
                        "treeConversionPending",
                        declaredPath,
                        fullScoPath,
                        "Tree billboards are not exported yet."));
            }

            sceneryAssets[
                declaredPath] =
                new(
                    resolvedMeshes
                        .ToArray(),
                    metadata
                        .UsesAbsoluteHeight);
        }

        return new(
            splineDefinitions,
            sceneryAssets,
            textures
                .GroupBy(
                    texture =>
                        (
                            Source:
                                Path.GetFullPath(
                                    texture
                                        .SourcePath),
                            texture
                                .TargetFileName
                        ))
                .Select(
                    group =>
                        group.First())
                .OrderBy(
                    texture =>
                        texture.TargetFileName,
                    StringComparer
                        .OrdinalIgnoreCase)
                .ToArray(),
            issues.ToArray());
    }

    private OmsiO3dGeometry ReadGeometry(
        string path) =>
        string.Equals(
            Path.GetExtension(
                path),
            ".x",
            StringComparison
                .OrdinalIgnoreCase)
            ? _directXReader.Read(
                path)
            : _o3dReader.Read(
                path);

    private static void AddSceneryTextures(
        string omsiRoot,
        string fullScoPath,
        string meshPath,
        string ownerPath,
        OmsiO3dGeometry geometry,
        ICollection<
            ProtonBusResolvedTextureSource>
            textures,
        IDictionary<string, string>
            textureTargets,
        ICollection<
            ProtonBusOmsiAssetIssue>
            issues)
    {
        for (
            var materialIndex = 0;
            materialIndex <
                geometry.Materials.Count;
            materialIndex++)
        {
            var textureName =
                geometry.Materials[
                    materialIndex]
                    .TextureName;

            if (
                string.IsNullOrWhiteSpace(
                    textureName))
            {
                continue;
            }

            if (
                !OmsiTextureAssetPathResolver
                    .TryResolveSceneryTexture(
                        omsiRoot,
                        fullScoPath,
                        meshPath,
                        textureName,
                        out var texturePath))
            {
                issues.Add(
                    new(
                        "sceneryTextureMissing",
                        textureName,
                        null,
                        ownerPath));

                continue;
            }

            AddTexture(
                textureName,
                texturePath,
                ProtonBusTextureNamePlanner
                    .ToPortablePngName(
                        textureName,
                        $"object_{materialIndex}"),
                ownerPath,
                textures,
                textureTargets,
                issues);
        }
    }

    private static void AddTexture(
        string declaredName,
        string sourcePath,
        string targetFileName,
        string ownerPath,
        ICollection<
            ProtonBusResolvedTextureSource>
            textures,
        IDictionary<string, string>
            textureTargets,
        ICollection<
            ProtonBusOmsiAssetIssue>
            issues)
    {
        var canonicalSource =
            Path.GetFullPath(
                sourcePath);

        if (
            textureTargets.TryGetValue(
                targetFileName,
                out var existingSource) &&
            !string.Equals(
                existingSource,
                canonicalSource,
                StringComparison
                    .OrdinalIgnoreCase))
        {
            issues.Add(
                new(
                    "textureTargetCollision",
                    declaredName,
                    canonicalSource,
                    $"Target '{targetFileName}' is already assigned to '{existingSource}'."));

            return;
        }

        textureTargets[
            targetFileName] =
            canonicalSource;

        textures.Add(
            new(
                declaredName,
                canonicalSource,
                targetFileName,
                RequiresConversion:
                    !string.Equals(
                        Path.GetExtension(
                            canonicalSource),
                        ".png",
                        StringComparison
                            .OrdinalIgnoreCase),
                ownerPath));
    }

    private static bool TryResolveSplinePath(
        string omsiRoot,
        string declaredPath,
        out string fullPath)
    {
        fullPath =
            string.Empty;

        if (
            string.IsNullOrWhiteSpace(
                declaredPath) ||
            Path.IsPathRooted(
                declaredPath) ||
            !string.Equals(
                Path.GetExtension(
                    declaredPath),
                ".sli",
                StringComparison
                    .OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var splinesRoot =
                Path.GetFullPath(
                    Path.Combine(
                        omsiRoot,
                        "Splines"))
                    .TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar);

            var normalized =
                declaredPath
                    .Trim()
                    .Replace(
                        '\\',
                        Path.DirectorySeparatorChar)
                    .Replace(
                        '/',
                        Path.DirectorySeparatorChar)
                    .TrimStart(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar);

            var prefix =
                "Splines" +
                Path.DirectorySeparatorChar;

            if (
                normalized.StartsWith(
                    prefix,
                    StringComparison
                        .OrdinalIgnoreCase))
            {
                normalized =
                    normalized[
                        prefix.Length..];
            }

            var candidate =
                Path.GetFullPath(
                    Path.Combine(
                        splinesRoot,
                        normalized));

            var requiredPrefix =
                splinesRoot +
                Path.DirectorySeparatorChar;

            if (
                !candidate.StartsWith(
                    requiredPrefix,
                    StringComparison
                        .OrdinalIgnoreCase))
            {
                return false;
            }

            fullPath =
                candidate;

            return true;
        }
        catch (Exception exception) when (
            exception is
                ArgumentException or
                NotSupportedException or
                PathTooLongException)
        {
            return false;
        }
    }
}
