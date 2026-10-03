using MapStudio.Core.Generation.Scene;
using MapStudio.Core.Generation.Vegetation;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioOsmVegetationReconstructionAdapterTests
{
    [Fact]
    public void PointAdapterBuildsStrongTreeEvidenceWhenSpeciesKnown()
    {
        var candidate =
            Assert.Single(
                new MapStudioOsmVegetationReconstructionAdapter()
                    .BuildPointCandidates(
                        [
                            new MapStudioGeoVegetationPoint(
                                "osm-tree-1",
                                -23.55,
                                -46.63,
                                MapStudioOsmVegetationKind.Tree,
                                "Tipuana tipu",
                                "Tipuana",
                                "broadleaved",
                                null)
                        ]));

        Assert.Equal(
            MapStudioSceneFeatureKind.Tree,
            candidate.Kind);

        var evidence =
            Assert.Single(
                candidate.Evidence);

        Assert.True(
            evidence.Confidence >=
            MapStudioSceneReconstructionPlanBuilder
                .AutoGenerateThreshold);
    }

    [Fact]
    public void ShrubPointRemainsShrubFeature()
    {
        var candidate =
            Assert.Single(
                new MapStudioOsmVegetationReconstructionAdapter()
                    .BuildPointCandidates(
                        [
                            new MapStudioGeoVegetationPoint(
                                "osm-shrub-1",
                                -23.55,
                                -46.63,
                                MapStudioOsmVegetationKind.Shrub,
                                null,
                                null,
                                null,
                                null)
                        ]));

        Assert.Equal(
            MapStudioSceneFeatureKind.Shrub,
            candidate.Kind);
    }

    [Fact]
    public void HedgeLineMapsToFenceSceneFeature()
    {
        var candidate =
            Assert.Single(
                new MapStudioOsmVegetationReconstructionAdapter()
                    .BuildLineCandidates(
                        [
                            new MapStudioGeoVegetationLine(
                                "osm-hedge-1",
                                [
                                    new(-23.55, -46.63),
                                    new(-23.5501, -46.6301),
                                    new(-23.5502, -46.6302)
                                ],
                                MapStudioOsmVegetationLineKind.Hedge,
                                null,
                                null,
                                null,
                                null)
                        ]));

        Assert.Equal(
            MapStudioSceneFeatureKind.Fence,
            candidate.Kind);

        Assert.Contains(
            "hedge",
            Assert.Single(
                candidate.Evidence)
            .Notes);
    }

    [Fact]
    public void TreeRowCanAutoGenerateFromGeometryAndMetadata()
    {
        var candidate =
            Assert.Single(
                new MapStudioOsmVegetationReconstructionAdapter()
                    .BuildLineCandidates(
                        [
                            new MapStudioGeoVegetationLine(
                                "osm-tree-row-1",
                                [
                                    new(-23.55, -46.63),
                                    new(-23.5501, -46.6301),
                                    new(-23.5502, -46.6302)
                                ],
                                MapStudioOsmVegetationLineKind.TreeRow,
                                "Ligustrum lucidum",
                                "Ligustrum",
                                "broadleaved",
                                null)
                        ]));

        var decision =
            Assert.Single(
                new MapStudioSceneReconstructionPlanBuilder()
                    .Build(
                        [candidate])
                    .Features);

        Assert.True(
            decision.AutoGenerate);
    }
}
