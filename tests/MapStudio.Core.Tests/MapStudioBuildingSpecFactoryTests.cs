using MapStudio.Core.AI;
using MapStudio.Core.Omsi.Buildings;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioBuildingSpecFactoryTests
{
    [Fact]
    public void FactoryUsesStructuredAnalysisWhenAvailable()
    {
        var spec =
            MapStudioBuildingSpecFactory
                .FromAnalysis(
                    "Predio",
                    new MapStudioBuildingReferenceAnalysis(
                        18,
                        12,
                        10,
                        4,
                        MapStudioBuildingRoofType
                            .Gable,
                        2.5,
                        null,
                        "concrete",
                        "tile",
                        "modern",
                        null,
                        0.92));

        Assert.Equal(
            18,
            spec.WidthMeters);

        Assert.Equal(
            10,
            spec.DepthMeters);

        Assert.Equal(
            12,
            spec.WallHeightMeters);

        Assert.Equal(
            4,
            spec.FloorCount);

        Assert.Equal(
            MapStudioBuildingRoofType
                .Gable,
            spec.RoofType);

        Assert.Equal(
            2.5,
            spec.RoofHeightMeters);
    }

    [Fact]
    public void FactoryBuildsSafeFallbacksFromPartialAnalysis()
    {
        var spec =
            MapStudioBuildingSpecFactory
                .FromAnalysis(
                    "Casa",
                    new MapStudioBuildingReferenceAnalysis(
                        null,
                        6,
                        null,
                        null,
                        MapStudioBuildingRoofType
                            .Unknown,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        0.4));

        Assert.Equal(
            2,
            spec.FloorCount);

        Assert.Equal(
            10,
            spec.WidthMeters);

        Assert.Equal(
            7.5,
            spec.DepthMeters);

        Assert.Equal(
            MapStudioBuildingRoofType
                .Flat,
            spec.RoofType);
    }

    [Fact]
    public async Task ServiceUsesAnyProviderImplementingCoreContract()
    {
        var provider =
            new TestProvider();

        var request =
            new MapStudioBuildingReferenceRequest(
                [
                    new MapStudioAiImageReference(
                        new byte[]
                        {
                            1,
                            2,
                            3
                        },
                        "image/png",
                        "building.png")
                ]);

        var spec =
            await new MapStudioAiBuildingSpecService()
                .AnalyzeAsync(
                    provider,
                    request,
                    "Generated");

        Assert.Equal(
            9,
            spec.WidthMeters);

        Assert.Equal(
            3,
            spec.FloorCount);
    }

    private sealed class TestProvider
        : IMapStudioAiProvider
    {
        public MapStudioAiProviderDescriptor
            Descriptor { get; } =
            new(
                "test",
                "Test",
                MapStudioAiCapability
                    .BuildingReferenceAnalysis |
                MapStudioAiCapability
                    .StructuredOutput);

        public Task<
            MapStudioBuildingReferenceAnalysis>
            AnalyzeBuildingReferenceAsync(
                MapStudioBuildingReferenceRequest request,
                CancellationToken cancellationToken =
                    default) =>
            Task.FromResult(
                new MapStudioBuildingReferenceAnalysis(
                    9,
                    9,
                    8,
                    3,
                    MapStudioBuildingRoofType
                        .Flat,
                    null,
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
            throw new NotSupportedException();
    }
}
