using MapStudio.Core.AI;
using MapStudio.Core.Generation.Buildings;
using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Scene;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioOsmBuildingReconstructionAdapterTests
{
    [Fact]
    public void AdapterBuildsMeasuredBuildingCandidateFromFootprint()
    {
        var building =
            new MapStudioProjectedBuildingFootprint(
                "osm-building-7",
                [
                    new(10, 20),
                    new(22, 20),
                    new(22, 29),
                    new(10, 29)
                ],
                new(16, 24.5),
                "apartments",
                "Edificio",
                4,
                12,
                MapStudioBuildingRoofType.Hip,
                2,
                "Rua Teste",
                "10");

        var candidate =
            Assert.Single(
                new MapStudioOsmBuildingReconstructionAdapter()
                    .BuildCandidates(
                        [building]));

        Assert.Equal(
            MapStudioSceneFeatureKind.Building,
            candidate.Kind);

        Assert.Equal(
            12,
            candidate.WidthMeters);

        Assert.Equal(
            9,
            candidate.DepthMeters);

        Assert.Equal(
            14,
            candidate.HeightMeters);

        var evidence =
            Assert.Single(
                candidate.Evidence);

        Assert.Equal(
            MapStudioSceneEvidenceSource.Osm,
            evidence.Source);

        Assert.True(
            evidence.Confidence >
            0.85);
    }

    [Fact]
    public void StrongOsmBuildingEvidenceCanAutoGenerate()
    {
        var building =
            new MapStudioProjectedBuildingFootprint(
                "osm-building-9",
                [
                    new(0, 0),
                    new(16, 0),
                    new(16, 10),
                    new(0, 10)
                ],
                new(8, 5),
                "commercial",
                null,
                3,
                9,
                MapStudioBuildingRoofType.Gable,
                2,
                "Avenida",
                "100");

        var candidate =
            Assert.Single(
                new MapStudioOsmBuildingReconstructionAdapter()
                    .BuildCandidates(
                        [building]));

        var decision =
            Assert.Single(
                new MapStudioSceneReconstructionPlanBuilder()
                    .Build(
                        [candidate])
                    .Features);

        Assert.True(
            decision.AutoGenerate);
    }

    [Fact]
    public void StreetLevelRefinerPreservesFootprintAndRefinesShape()
    {
        var building =
            new MapStudioProjectedBuildingFootprint(
                "osm-building-10",
                [
                    new(0, 0),
                    new(10, 0),
                    new(10, 8),
                    new(0, 8)
                ],
                new(5, 4),
                "house",
                null,
                1,
                3,
                MapStudioBuildingRoofType.Flat,
                0,
                null,
                null);

        var refined =
            new MapStudioStreetLevelBuildingRefiner()
                .Refine(
                    building,
                    new MapStudioBuildingReferenceAnalysis(
                        10,
                        8,
                        8,
                        2,
                        MapStudioBuildingRoofType.Gable,
                        2,
                        null,
                        "stucco",
                        "tile",
                        "residential",
                        null,
                        0.94));

        Assert.Equal(
            building.Points,
            refined.Points);

        Assert.Equal(
            building.Center,
            refined.Center);

        Assert.Equal(
            2,
            refined.FloorCount);

        Assert.Equal(
            6,
            refined.WallHeightMeters);

        Assert.Equal(
            MapStudioBuildingRoofType.Gable,
            refined.RoofType);

        Assert.Equal(
            2,
            refined.RoofHeightMeters);

        Assert.Equal(
            "stucco",
            refined.FacadeMaterial);

        Assert.Equal(
            "tile",
            refined.RoofMaterial);
    }

    [Fact]
    public void LowConfidenceStreetLevelAnalysisCannotOverwriteOsmShape()
    {
        var building =
            new MapStudioProjectedBuildingFootprint(
                "osm-building-11",
                [
                    new(0, 0),
                    new(10, 0),
                    new(10, 8),
                    new(0, 8)
                ],
                new(5, 4),
                "house",
                null,
                2,
                6,
                MapStudioBuildingRoofType.Hip,
                2,
                null,
                null);

        var refined =
            new MapStudioStreetLevelBuildingRefiner()
                .Refine(
                    building,
                    new MapStudioBuildingReferenceAnalysis(
                        10,
                        20,
                        8,
                        6,
                        MapStudioBuildingRoofType.Gable,
                        4,
                        null,
                        null,
                        null,
                        null,
                        null,
                        0.40));

        Assert.Equal(
            building,
            refined);
    }
}
