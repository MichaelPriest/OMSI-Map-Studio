using MapStudio.Core.Omsi.Maps;

namespace MapStudio.Core.ProtonBus;

public sealed record ProtonBusOmsiTilePackageExportResult(
    bool IsExported,
    ProtonBusMapPackageResult?
        Package,
    ProtonBusOmsiAssetResolutionResult
        Assets,
    ProtonBusOmsiTileExportResult?
        Tile,
    IReadOnlyList<
        ProtonBusOmsiAssetIssue>
        Issues);

public sealed class ProtonBusOmsiTilePackageExporter
{
    private readonly ProtonBusOmsiAssetResolver
        _assetResolver =
            new();

    public async Task<
        ProtonBusOmsiTilePackageExportResult>
        ExportAsync(
            string omsiRoot,
            string outputRoot,
            ProtonBusMapDefinition definition,
            OmsiTileReference tile,
            OmsiTileContent content,
            ProtonBusOmsiTileExportOptions?
                options = null,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            omsiRoot);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            outputRoot);

        ArgumentNullException.ThrowIfNull(
            definition);

        ArgumentNullException.ThrowIfNull(
            tile);

        ArgumentNullException.ThrowIfNull(
            content);

        var assets =
            await _assetResolver
                .ResolveAsync(
                    omsiRoot,
                    content,
                    cancellationToken)
                .ConfigureAwait(false);

        var issues =
            new List<
                ProtonBusOmsiAssetIssue>(
                    assets.Issues);

        foreach (
            var texture
            in assets.Textures)
        {
            if (
                !ProtonBusTextureTranscoder
                    .CanTranscode(
                        texture.SourcePath))
            {
                issues.Add(
                    new(
                        "textureTranscodeUnsupported",
                        texture.DeclaredName,
                        texture.SourcePath,
                        texture.TargetFileName));
            }
        }

        if (
            HasBlockingIssue(
                issues))
        {
            return new(
                false,
                null,
                assets,
                null,
                issues.ToArray());
        }

        var tileResult =
            ProtonBusOmsiTileExportBuilder
                .Build(
                    tile,
                    content,
                    assets
                        .SplineDefinitions,
                    assets
                        .SceneryAssets,
                    options);

        foreach (
            var missing
            in tileResult
                .MissingSplineDefinitions)
        {
            issues.Add(
                new(
                    "splineDefinitionMissingAfterResolution",
                    missing));
        }

        foreach (
            var missing
            in tileResult
                .MissingSceneryAssets)
        {
            issues.Add(
                new(
                    "sceneryAssetMissingAfterResolution",
                    missing));
        }

        if (
            HasBlockingIssue(
                issues))
        {
            return new(
                false,
                null,
                assets,
                tileResult,
                issues.ToArray());
        }

        cancellationToken
            .ThrowIfCancellationRequested();

        var package =
            ProtonBusMapPackageWriter
                .Write(
                    outputRoot,
                    new(
                        definition,
                        [
                            new(
                                $"tile_{tile.X}_{tile.Y}.3ds",
                                tileResult.Scene)
                        ],
                        assets.Textures
                            .Select(
                                texture =>
                                    new ProtonBusTextureExport(
                                        texture
                                            .SourcePath,
                                        texture
                                            .TargetFileName))
                            .ToArray()));

        return new(
            true,
            package,
            assets,
            tileResult,
            issues.ToArray());
    }

    private static bool HasBlockingIssue(
        IEnumerable<
            ProtonBusOmsiAssetIssue>
            issues) =>
        issues.Any(
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
                    "textureTargetCollision" or
                    "textureTranscodeUnsupported" or
                    "splineDefinitionMissingAfterResolution" or
                    "sceneryAssetMissingAfterResolution");
}
