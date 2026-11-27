using MapStudio.Core.Generation.Scene;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioOsmInfrastructureImporterTests
{
    [Fact]
    public void ImporterRecognizesUrbanInfrastructureWays()
    {
        const string xml =
            """
            <osm version="0.6">
              <node id="1" lat="-23.55" lon="-46.63"/>
              <node id="2" lat="-23.5501" lon="-46.6301"/>
              <node id="3" lat="-23.5502" lon="-46.6302"/>
              <node id="4" lat="-23.5503" lon="-46.6303"/>

              <way id="10">
                <nd ref="1"/><nd ref="2"/>
                <tag k="barrier" v="wall"/>
              </way>
              <way id="11">
                <nd ref="1"/><nd ref="2"/>
                <tag k="barrier" v="fence"/>
              </way>
              <way id="12">
                <nd ref="1"/><nd ref="2"/>
                <tag k="barrier" v="guard_rail"/>
              </way>
              <way id="13">
                <nd ref="1"/><nd ref="2"/>
                <tag k="highway" v="footway"/>
                <tag k="footway" v="sidewalk"/>
              </way>
              <way id="14">
                <nd ref="1"/><nd ref="2"/>
                <tag k="highway" v="service"/>
                <tag k="service" v="driveway"/>
              </way>
              <way id="15">
                <nd ref="1"/><nd ref="2"/><nd ref="3"/><nd ref="1"/>
                <tag k="amenity" v="parking"/>
                <tag k="surface" v="asphalt"/>
              </way>
            </osm>
            """;

        var result =
            new MapStudioOsmInfrastructureImporter()
                .Parse(xml);

        Assert.Equal(6, result.Features.Count);

        foreach (
            var kind in
                Enum.GetValues<MapStudioOsmInfrastructureKind>())
        {
            Assert.Contains(
                result.Features,
                feature =>
                    feature.Kind == kind);
        }
    }

    [Fact]
    public void ImporterPreservesWidthSurfaceAndAreaEvidence()
    {
        const string xml =
            """
            <osm version="0.6">
              <node id="1" lat="-23.55" lon="-46.63"/>
              <node id="2" lat="-23.5501" lon="-46.6301"/>
              <node id="3" lat="-23.5502" lon="-46.6302"/>
              <way id="20">
                <nd ref="1"/><nd ref="2"/><nd ref="3"/><nd ref="1"/>
                <tag k="amenity" v="parking"/>
                <tag k="surface" v="paving_stones"/>
                <tag k="width" v="12.5 m"/>
                <tag k="name" v="Terminal parking"/>
              </way>
            </osm>
            """;

        var feature =
            Assert.Single(
                new MapStudioOsmInfrastructureImporter()
                    .Parse(xml)
                    .Features);

        Assert.True(feature.IsArea);
        Assert.Equal("paving_stones", feature.Surface);
        Assert.Equal(12.5, feature.WidthMeters);
        Assert.Equal("Terminal parking", feature.Name);
    }

    [Fact]
    public void OpenParkingWayIsRejected()
    {
        const string xml =
            """
            <osm version="0.6">
              <node id="1" lat="-23.55" lon="-46.63"/>
              <node id="2" lat="-23.5501" lon="-46.6301"/>
              <node id="3" lat="-23.5502" lon="-46.6302"/>
              <way id="30">
                <nd ref="1"/><nd ref="2"/><nd ref="3"/>
                <tag k="amenity" v="parking"/>
              </way>
            </osm>
            """;

        var result =
            new MapStudioOsmInfrastructureImporter()
                .Parse(xml);

        Assert.Empty(result.Features);
        Assert.Equal(1, result.IgnoredWayCount);
    }

    [Fact]
    public void AdapterMapsAllSupportedInfrastructureKinds()
    {
        var features =
            Enum.GetValues<MapStudioOsmInfrastructureKind>()
                .Select(
                    (kind, index) =>
                        new MapStudioGeoInfrastructureFeature(
                            "feature-" + index,
                            kind,
                            [
                                new MapStudioGeoInfrastructurePoint(
                                    -23.55,
                                    -46.63),
                                new MapStudioGeoInfrastructurePoint(
                                    -23.5501,
                                    -46.6301)
                            ],
                            null,
                            null,
                            null,
                            kind == MapStudioOsmInfrastructureKind.Parking))
                .ToArray();

        var candidates =
            new MapStudioOsmInfrastructureReconstructionAdapter()
                .BuildCandidates(features);

        Assert.Contains(
            candidates,
            item => item.Kind == MapStudioSceneFeatureKind.Wall);
        Assert.Contains(
            candidates,
            item => item.Kind == MapStudioSceneFeatureKind.Fence);
        Assert.Contains(
            candidates,
            item => item.Kind == MapStudioSceneFeatureKind.GuardRail);
        Assert.Contains(
            candidates,
            item => item.Kind == MapStudioSceneFeatureKind.Sidewalk);
        Assert.Contains(
            candidates,
            item => item.Kind == MapStudioSceneFeatureKind.Driveway);
        Assert.Contains(
            candidates,
            item => item.Kind == MapStudioSceneFeatureKind.Parking);
    }

    [Fact]
    public void BareFenceNeedsReview()
    {
        var candidate =
            Assert.Single(
                new MapStudioOsmInfrastructureReconstructionAdapter()
                    .BuildCandidates(
                        [
                            new MapStudioGeoInfrastructureFeature(
                                "fence-1",
                                MapStudioOsmInfrastructureKind.Fence,
                                [
                                    new MapStudioGeoInfrastructurePoint(
                                        -23.55,
                                        -46.63),
                                    new MapStudioGeoInfrastructurePoint(
                                        -23.5501,
                                        -46.6301)
                                ],
                                null,
                                null,
                                null,
                                false)
                        ]));

        var decision =
            Assert.Single(
                new MapStudioSceneReconstructionPlanBuilder()
                    .Build([candidate])
                    .Features);

        Assert.False(decision.AutoGenerate);
        Assert.True(decision.NeedsReview);
    }

    [Fact]
    public void DetailedParkingCanAutoGenerate()
    {
        var candidate =
            Assert.Single(
                new MapStudioOsmInfrastructureReconstructionAdapter()
                    .BuildCandidates(
                        [
                            new MapStudioGeoInfrastructureFeature(
                                "parking-1",
                                MapStudioOsmInfrastructureKind.Parking,
                                [
                                    new MapStudioGeoInfrastructurePoint(
                                        -23.55,
                                        -46.63),
                                    new MapStudioGeoInfrastructurePoint(
                                        -23.5501,
                                        -46.6301),
                                    new MapStudioGeoInfrastructurePoint(
                                        -23.5502,
                                        -46.6302),
                                    new MapStudioGeoInfrastructurePoint(
                                        -23.55,
                                        -46.63)
                                ],
                                "Terminal parking",
                                "asphalt",
                                12.0,
                                true)
                        ]));

        var decision =
            Assert.Single(
                new MapStudioSceneReconstructionPlanBuilder()
                    .Build([candidate])
                    .Features);

        Assert.True(decision.AutoGenerate);
        Assert.False(decision.NeedsReview);
    }

    [Fact]
    public void IndependentAerialEvidenceRaisesSidewalkConfidence()
    {
        var osmCandidate =
            Assert.Single(
                new MapStudioOsmInfrastructureReconstructionAdapter()
                    .BuildCandidates(
                        [
                            new MapStudioGeoInfrastructureFeature(
                                "sidewalk-1",
                                MapStudioOsmInfrastructureKind.Sidewalk,
                                [
                                    new MapStudioGeoInfrastructurePoint(
                                        -23.55,
                                        -46.63),
                                    new MapStudioGeoInfrastructurePoint(
                                        -23.5501,
                                        -46.6301)
                                ],
                                null,
                                "concrete",
                                1.8,
                                false)
                        ]));

        var fused =
            osmCandidate with
            {
                Evidence =
                    [
                        ..osmCandidate.Evidence,
                        new MapStudioSceneEvidence(
                            MapStudioSceneEvidenceSource.AerialImagery,
                            0.88,
                            "aerial-1")
                    ]
            };

        var decision =
            Assert.Single(
                new MapStudioSceneReconstructionPlanBuilder()
                    .Build([fused])
                    .Features);

        Assert.True(decision.AutoGenerate);
    }
}
