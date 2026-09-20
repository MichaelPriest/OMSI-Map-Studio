using MapStudio.Core.Omsi.Splines;

namespace MapStudio.Renderer.Scene;

public sealed class NativeSplineAssetLoader
{
    private readonly OmsiSplineDefinitionReader
        _reader =
            new();

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
                "splinePathInvalid");
        }

        var definition =
            await _reader
                .ReadAsync(
                    fullPath,
                    cancellationToken)
                .ConfigureAwait(false);

        if (!definition.Exists)
        {
            return new NativeSplineAsset(
                declaredPath,
                fullPath,
                definition,
                "splineMissing");
        }

        return new NativeSplineAsset(
            declaredPath,
            fullPath,
            definition,
            definition.Surfaces.Count == 0
                ? "noRenderableProfile"
                : null);
    }
}
