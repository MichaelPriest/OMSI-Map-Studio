using MapStudio.Core.Generation.Scene;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioSceneReconstructionPlanBuilderTests
{
    [Fact]
    public void BuilderAutoGeneratesFeatureWithIndependentStrongEvidence()
    {
        var plan =
            new MapStudioSceneReconstructionPlanBuilder()
                .Build(
                    [
                        new MapStudioSceneFeatureCandidate(
                            "building-1",
                            MapStudioSceneFeatureKind.Building,
                            [
                                new MapStudioSceneEvidence(
                                    MapStudioSceneEvidenceSource.Osm,
                                    0.92,
                                    "way/42"),
                                new MapStudioSceneEvidence(
                                    MapStudioSceneEvidenceSource.StreetLevelImagery,
                                    0.88,
                                    "image/abc")
                            ],
                            SourceBuildingId:
                                "osm-building-42",
                            WidthMeters:
                                12,
                            HeightMeters:
                                9,
                            DepthMeters:
                                8)
                    ]);

        var feature =
            Assert.Single(
                plan.Features);

        Assert.True(
            feature.AutoGenerate);

        Assert.False(
            feature.NeedsReview);

        Assert.True(
            feature.Confidence >=
            MapStudioSceneReconstructionPlanBuilder
                .AutoGenerateThreshold);

        Assert.Equal(
            1,
            plan.AutoGenerateCount);
    }

    [Fact]
    public void BuilderKeepsMediumConfidenceFeatureForReview()
    {
        var plan =
            new MapStudioSceneReconstructionPlanBuilder()
                .Build(
                    [
                        new MapStudioSceneFeatureCandidate(
                            "tree-1",
                            MapStudioSceneFeatureKind.Tree,
                            [
                                new MapStudioSceneEvidence(
                                    MapStudioSceneEvidenceSource.AerialImagery,
                                    0.75)
                            ])
                    ]);

        var feature =
            Assert.Single(
                plan.Features);

        Assert.False(
            feature.AutoGenerate);

        Assert.True(
            feature.NeedsReview);

        Assert.Equal(
            1,
            plan.ReviewCount);
    }

    [Fact]
    public void BuilderDoesNotAutoGenerateUnsupportedLowConfidenceGuess()
    {
        var plan =
            new MapStudioSceneReconstructionPlanBuilder()
                .Build(
                    [
                        new MapStudioSceneFeatureCandidate(
                            "pole-1",
                            MapStudioSceneFeatureKind.UtilityPole,
                            [
                                new MapStudioSceneEvidence(
                                    MapStudioSceneEvidenceSource.AerialImagery,
                                    0.30)
                            ])
                    ]);

        var feature =
            Assert.Single(
                plan.Features);

        Assert.False(
            feature.AutoGenerate);

        Assert.False(
            feature.NeedsReview);

        Assert.True(
            feature.Confidence <
            MapStudioSceneReconstructionPlanBuilder
                .ReviewThreshold);
    }

    [Fact]
    public void BuilderNormalizesInvalidDimensionsAndEvidence()
    {
        var plan =
            new MapStudioSceneReconstructionPlanBuilder()
                .Build(
                    [
                        new MapStudioSceneFeatureCandidate(
                            " house ",
                            MapStudioSceneFeatureKind.Building,
                            [
                                new MapStudioSceneEvidence(
                                    MapStudioSceneEvidenceSource.UserImagery,
                                    double.NaN,
                                    "  ref  ")
                            ],
                            WidthMeters:
                                -1,
                            HeightMeters:
                                double.PositiveInfinity,
                            DepthMeters:
                                7)
                    ]);

        var feature =
            Assert.Single(
                plan.Features);

        Assert.Equal(
            "house",
            feature.Id);

        Assert.Null(
            feature.WidthMeters);

        Assert.Null(
            feature.HeightMeters);

        Assert.Equal(
            7,
            feature.DepthMeters);

        Assert.Empty(
            feature.Evidence);
    }
}
