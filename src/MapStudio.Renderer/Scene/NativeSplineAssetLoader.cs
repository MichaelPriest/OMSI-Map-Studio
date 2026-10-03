using MapStudio.Core.Omsi.Splines;
using MapStudio.Core.Omsi.Textures;

namespace MapStudio.Renderer.Scene;

public sealed class NativeSplineAssetLoader
{
    private readonly OmsiSplineDefinitionReader
        _reader =
            new();

    private readonly NativeDerivedFileCache<
        OmsiSplineDefinition>
        _definitionCache =
            new(
                512);

    public async Task<
        IReadOnlyDictionary<
            string,
            NativeSplineAsset>>
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
                NativeSplineAsset>(
                StringComparer
                    .OrdinalIgnoreCase);

        var paths =
            scene.Splines
                .Select(
                    item =>
                        item.Spline
                            .SplinePath)
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

    public Task<NativeSplineAsset>
        LoadAssetAsync(
            string omsiRoot,
            string declaredPath,
            CancellationToken cancellationToken =
                default) =>
            LoadOneAsync(
                omsiRoot,
                declaredPath,
                cancellationToken);

    private async Task<NativeSplineAsset>
        LoadOneAsync(
            string omsiRoot,
            string declaredPath,
            CancellationToken cancellationToken)
    {
        if (
            !OmsiSplinePathResolver
                .TryResolve(
                    omsiRoot,
                    declaredPath,
                    out var fullPath))
        {
            return new NativeSplineAsset(
                declaredPath,
                null,
                OmsiSplineDefinition.Missing,
                Array.Empty<string?>(),
                "splinePathInvalid");
        }

        var definition =
            await LoadDefinitionAsync(
                    fullPath,
                    cancellationToken)
                .ConfigureAwait(false);

        if (!definition.Exists)
        {
            return new NativeSplineAsset(
                declaredPath,
                fullPath,
                definition,
                Array.Empty<string?>(),
                "splineMissing");
        }

        var texturePaths =
            definition.Textures
                .Select(
                    textureName =>
                        OmsiTextureAssetPathResolver
                            .TryResolveSplineTexture(
                                omsiRoot,
                                fullPath,
                                textureName,
                                out var texturePath)
                            ? texturePath
                            : null)
                .ToArray();

        return new NativeSplineAsset(
            declaredPath,
            fullPath,
            definition,
            texturePaths,
            definition.Surfaces.Count == 0
                ? "noRenderableProfile"
                : null);
    }

    private async Task<
        OmsiSplineDefinition>
        LoadDefinitionAsync(
            string fullPath,
            CancellationToken cancellationToken)
    {
        if (
            _definitionCache
                .TryGet(
                    fullPath,
                    out var cached))
        {
            return cached;
        }

        var definition =
            await _reader
                .ReadAsync(
                    fullPath,
                    cancellationToken)
                .ConfigureAwait(false);

        _definitionCache
            .Set(
                fullPath,
                definition);

        return definition;
    }
}
