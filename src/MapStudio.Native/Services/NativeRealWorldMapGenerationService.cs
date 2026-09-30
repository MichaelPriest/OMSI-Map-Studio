using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Scene;
using MapStudio.Core.Generation.Terrain;
using MapStudio.Core.Omsi.Indexing;
using MapStudio.Core.Omsi.Structures;

namespace MapStudio.Native.Services;

public enum NativeRealWorldElevationMode
{
    None,
    Google,
    OpenMeteo
}

public sealed record NativeRealWorldMapGenerationResult(
    MapStudioRealWorldMapPipelineResult Pipeline,
    NativeRealWorldElevationMode ElevationMode);

public sealed class NativeRealWorldMapGenerationService
{
    private readonly MapStudioRealWorldMapPipeline
        _pipeline;

    public NativeRealWorldMapGenerationService()
        : this(
            new MapStudioRealWorldMapPipeline())
    {
    }

    public NativeRealWorldMapGenerationService(
        MapStudioRealWorldMapPipeline pipeline)
    {
        ArgumentNullException.ThrowIfNull(
            pipeline);

        _pipeline =
            pipeline;
    }

    public async Task<NativeRealWorldMapGenerationResult>
        RunBestAvailableAsync(
            string omsiRoot,
            string mapDirectory,
            double south,
            double west,
            double north,
            double east,
            MapStudioGeographicAnchor anchor,
            int elevationRows = 17,
            int elevationColumns = 17,
            IReadOnlyDictionary<
                string,
                IReadOnlyList<MapStudioSceneEvidence>>?
                streetFurnitureEvidence = null,
            IProgress<MapStudioRealWorldMapPipelineProgress>?
                progress = null,
            IProgress<OmsiAssetIndexProgress>?
                assetIndexProgress = null,
            IProgress<MapStudioElevationDownloadProgress>?
                elevationProgress = null,
            CancellationToken cancellationToken = default,
            bool useSavedGoogleElevation = true,
            bool useSavedOpenMeteoElevation = false,
            bool useBuildingVisualEvidence = false,
            bool useOpenAerialMapEvidence = false)
    {
        var buildingVisualEvidenceProvider =
            CreateBuildingVisualEvidenceProvider(
                mapDirectory,
                useBuildingVisualEvidence,
                useOpenAerialMapEvidence);

        var googleKey =
            NativeMapCredentialStore
                .TryGetGoogleMapsApiKey();

        if (
            useSavedGoogleElevation &&
            !string.IsNullOrWhiteSpace(
                googleKey))
        {
            var pipeline =
                await _pipeline
                    .RunAutoIndexedWithGoogleElevationAsync(
                        omsiRoot,
                        mapDirectory,
                        south,
                        west,
                        north,
                        east,
                        anchor,
                        googleKey,
                        elevationRows,
                        elevationColumns,
                        streetFurnitureEvidence,
                        progress,
                        assetIndexProgress,
                        elevationProgress,
                        cancellationToken,
                        buildingVisualEvidenceProvider)
                    .ConfigureAwait(false);

            return new NativeRealWorldMapGenerationResult(
                pipeline,
                NativeRealWorldElevationMode.Google);
        }

        var openMeteoKey =
            NativeMapCredentialStore
                .TryGetOpenMeteoApiKey();

        if (
            useSavedOpenMeteoElevation)
        {
            var pipeline =
                await _pipeline
                    .RunAutoIndexedWithOpenMeteoElevationAsync(
                        omsiRoot,
                        mapDirectory,
                        south,
                        west,
                        north,
                        east,
                        anchor,
                        openMeteoKey,
                        elevationRows,
                        elevationColumns,
                        streetFurnitureEvidence,
                        progress,
                        assetIndexProgress,
                        elevationProgress,
                        cancellationToken,
                        buildingVisualEvidenceProvider)
                    .ConfigureAwait(false);

            return new NativeRealWorldMapGenerationResult(
                pipeline,
                NativeRealWorldElevationMode.OpenMeteo);
        }

        var withoutElevation =
            await _pipeline
                .RunAutoIndexedAsync(
                    omsiRoot,
                    mapDirectory,
                    south,
                    west,
                    north,
                    east,
                    anchor,
                    streetFurnitureEvidence,
                    progress,
                    assetIndexProgress,
                    cancellationToken,
                    buildingVisualEvidenceProvider:
                        buildingVisualEvidenceProvider)
                .ConfigureAwait(false);

        return new NativeRealWorldMapGenerationResult(
            withoutElevation,
            NativeRealWorldElevationMode.None);
    }

    public async Task<NativeRealWorldMapGenerationResult>
        RunWithSavedGoogleElevationAsync(
            string omsiRoot,
            string mapDirectory,
            double south,
            double west,
            double north,
            double east,
            MapStudioGeographicAnchor anchor,
            int elevationRows = 17,
            int elevationColumns = 17,
            IReadOnlyDictionary<
                string,
                IReadOnlyList<MapStudioSceneEvidence>>?
                streetFurnitureEvidence = null,
            IProgress<MapStudioRealWorldMapPipelineProgress>?
                progress = null,
            IProgress<OmsiAssetIndexProgress>?
                assetIndexProgress = null,
            IProgress<MapStudioElevationDownloadProgress>?
                elevationProgress = null,
            CancellationToken cancellationToken = default)
    {
        var googleKey =
            NativeMapCredentialStore
                .TryGetGoogleMapsApiKey();

        if (
            string.IsNullOrWhiteSpace(
                googleKey))
        {
            throw new InvalidOperationException(
                "googleMapsApiKeyRequired");
        }

        var pipeline =
            await _pipeline
                .RunAutoIndexedWithGoogleElevationAsync(
                    omsiRoot,
                    mapDirectory,
                    south,
                    west,
                    north,
                    east,
                    anchor,
                    googleKey,
                    elevationRows,
                    elevationColumns,
                    streetFurnitureEvidence,
                    progress,
                    assetIndexProgress,
                    elevationProgress,
                    cancellationToken)
                .ConfigureAwait(false);

        return new NativeRealWorldMapGenerationResult(
            pipeline,
            NativeRealWorldElevationMode.Google);
    }

    private static IMapStudioBuildingVisualEvidenceProvider?
        CreateBuildingVisualEvidenceProvider(
            string mapDirectory,
            bool useLocalReferences,
            bool useOpenAerialMap)
    {
        var referenceRoot =
            Path.Combine(
                Path.GetFullPath(
                    mapDirectory),
                ".mapstudio",
                "references",
                "buildings");

        var hasLocalReferences =
            useLocalReferences &&
            Directory.Exists(
                referenceRoot) &&
            Directory
                .EnumerateFiles(
                    referenceRoot,
                    "*.*",
                    SearchOption.AllDirectories)
                .Any(
                    path =>
                        Path.GetExtension(
                            path)
                            .ToLowerInvariant() is
                                ".png" or
                                ".jpg" or
                                ".jpeg" or
                                ".webp");

        if (
            !hasLocalReferences &&
            !useOpenAerialMap)
        {
            return null;
        }

        var settings =
            NativeAiConnectionSettingsStore
                .Load();

        var profile =
            settings.GetActiveProfile()
            ?? throw new InvalidOperationException(
                "buildingVisualEvidenceAiProfileRequired");

        var provider =
            NativeAiProviderFactory
                .Create(
                    profile);

        if (
            (
                provider.Descriptor.Capabilities &
                MapStudio.Core.AI.MapStudioAiCapability
                    .BuildingReferenceAnalysis
            ) ==
            0)
        {
            throw new InvalidOperationException(
                "buildingVisualEvidenceAiCapabilityRequired");
        }

        var providers =
            new List<
                IMapStudioBuildingVisualEvidenceProvider>();

        if (hasLocalReferences)
        {
            providers.Add(
                new NativeBuildingVisualEvidenceProvider(
                    mapDirectory,
                    provider));
        }

        if (useOpenAerialMap)
        {
            providers.Add(
                new NativeOpenAerialMapBuildingVisualEvidenceProvider(
                    provider));
        }

        return providers.Count switch
        {
            0 =>
                null,
            1 =>
                providers[0],
            _ =>
                new NativeCompositeBuildingVisualEvidenceProvider(
                    providers)
        };
    }
}
