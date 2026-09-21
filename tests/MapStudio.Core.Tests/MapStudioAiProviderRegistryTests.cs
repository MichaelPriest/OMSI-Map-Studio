using MapStudio.Core.AI;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioAiProviderRegistryTests
{
    [Fact]
    public void RegistryAcceptsProviderAdaptersWithoutBindingToVendor()
    {
        var registry =
            new MapStudioAiProviderRegistry();

        registry.Register(
            new TestProvider(
                "local-vision",
                "Local Vision",
                isLocal:
                    true));

        registry.Register(
            new TestProvider(
                "cloud-any",
                "Cloud Any",
                isLocal:
                    false));

        Assert.Equal(
            2,
            registry.Providers.Count);

        Assert.True(
            registry.TryGet(
                "LOCAL-VISION",
                out var local));

        Assert.NotNull(
            local);

        Assert.True(
            local!
                .Descriptor
                .IsLocal);

        Assert.Equal(
            "cloud-any",
            registry
                .GetRequired(
                    "cloud-any")
                .Descriptor
                .Id);
    }

    [Fact]
    public void BuildingAnalysisNormalizationRejectsImpossibleMeasurements()
    {
        var analysis =
            new MapStudioBuildingReferenceAnalysis(
                WidthMeters:
                    -5,
                HeightMeters:
                    18,
                DepthMeters:
                    double.NaN,
                FloorCount:
                    999,
                RoofType:
                    MapStudioBuildingRoofType
                        .Flat,
                RoofHeightMeters:
                    0,
                Openings:
                    null,
                FacadeMaterial:
                    "brick",
                RoofMaterial:
                    null,
                ArchitecturalStyle:
                    null,
                Notes:
                    null,
                Confidence:
                    1.7);

        var normalized =
            analysis.Normalize();

        Assert.Null(
            normalized.WidthMeters);

        Assert.Equal(
            18,
            normalized.HeightMeters);

        Assert.Null(
            normalized.DepthMeters);

        Assert.Null(
            normalized.FloorCount);

        Assert.Null(
            normalized.RoofHeightMeters);

        Assert.Equal(
            1,
            normalized.Confidence);
    }

    private sealed class TestProvider(
        string id,
        string displayName,
        bool isLocal)
        : IMapStudioAiProvider
    {
        public MapStudioAiProviderDescriptor
            Descriptor { get; } =
            new(
                id,
                displayName,
                MapStudioAiCapability
                    .ImageUnderstanding |
                MapStudioAiCapability
                    .BuildingReferenceAnalysis |
                MapStudioAiCapability
                    .RoadReferenceAnalysis |
                MapStudioAiCapability
                    .StructuredOutput,
                isLocal);

        public Task<
            MapStudioBuildingReferenceAnalysis>
            AnalyzeBuildingReferenceAsync(
                MapStudioBuildingReferenceRequest request,
                CancellationToken cancellationToken =
                    default) =>
            Task.FromResult(
                new MapStudioBuildingReferenceAnalysis(
                    10,
                    9,
                    14,
                    3,
                    MapStudioBuildingRoofType
                        .Flat,
                    1,
                    null,
                    null,
                    null,
                    null,
                    null,
                    0.9));

        public Task<
            MapStudioRoadReferenceAnalysis>
            AnalyzeRoadReferenceAsync(
                MapStudioRoadReferenceRequest request,
                CancellationToken cancellationToken =
                    default) =>
            Task.FromResult(
                new MapStudioRoadReferenceAnalysis(
                    [],
                    null,
                    0.9));
    }
}
