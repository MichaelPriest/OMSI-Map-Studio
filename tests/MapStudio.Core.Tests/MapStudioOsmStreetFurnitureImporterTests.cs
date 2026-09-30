using MapStudio.Core.Generation.Scene;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioOsmStreetFurnitureImporterTests
{
    [Fact]
    public void ImporterRecognizesSupportedPointFurniture()
    {
        const string xml =
            """
            <osm version="0.6">
              <node id="1" lat="-23.55" lon="-46.63">
                <tag k="highway" v="street_lamp"/>
              </node>
              <node id="2" lat="-23.5501" lon="-46.6301">
                <tag k="power" v="pole"/>
              </node>
              <node id="3" lat="-23.5502" lon="-46.6302">
                <tag k="traffic_sign" v="BR:R-1"/>
                <tag k="direction" v="90"/>
              </node>
              <node id="4" lat="-23.5503" lon="-46.6303">
                <tag k="highway" v="bus_stop"/>
                <tag k="shelter" v="yes"/>
                <tag k="operator" v="Transit"/>
              </node>
            </osm>
            """;

        var result =
            new MapStudioOsmStreetFurnitureImporter()
                .Parse(xml);

        Assert.Equal(
            4,
            result.Points.Count);

        Assert.Contains(
            result.Points,
            point =>
                point.Kind ==
                MapStudioOsmStreetFurnitureKind.StreetLight);

        Assert.Contains(
            result.Points,
            point =>
                point.Kind ==
                MapStudioOsmStreetFurnitureKind.UtilityPole);

        Assert.Contains(
            result.Points,
            point =>
                point.Kind ==
                MapStudioOsmStreetFurnitureKind.TrafficSign);

        Assert.Contains(
            result.Points,
            point =>
                point.Kind ==
                MapStudioOsmStreetFurnitureKind.BusShelter);

        var sign =
            Assert.Single(
                result.Points,
                point =>
                    point.Kind ==
                    MapStudioOsmStreetFurnitureKind.TrafficSign);

        Assert.Equal(
            90,
            sign.DirectionDegrees);
    }

    [Fact]
    public void AdapterMapsFurnitureToSceneFeatures()
    {
        var candidates =
            new MapStudioOsmStreetFurnitureReconstructionAdapter()
                .BuildCandidates(
                    [
                        new MapStudioGeoStreetFurniturePoint(
                            "lamp-1",
                            -23.55,
                            -46.63,
                            MapStudioOsmStreetFurnitureKind.StreetLight,
                            null,
                            null,
                            null,
                            null),
                        new MapStudioGeoStreetFurniturePoint(
                            "sign-1",
                            -23.55,
                            -46.63,
                            MapStudioOsmStreetFurnitureKind.TrafficSign,
                            null,
                            null,
                            null,
                            null)
                    ]);

        Assert.Contains(
            candidates,
            item =>
                item.Kind ==
                MapStudioSceneFeatureKind.StreetLight);

        Assert.Contains(
            candidates,
            item =>
                item.Kind ==
                MapStudioSceneFeatureKind.TrafficSign);
    }

    [Fact]
    public void ExplicitLampEvidenceCanAutoGenerate()
    {
        var candidate =
            Assert.Single(
                new MapStudioOsmStreetFurnitureReconstructionAdapter()
                    .BuildCandidates(
                        [
                            new MapStudioGeoStreetFurniturePoint(
                                "lamp-1",
                                -23.55,
                                -46.63,
                                MapStudioOsmStreetFurnitureKind.StreetLight,
                                null,
                                null,
                                null,
                                null)
                        ]));

        var decision =
            Assert.Single(
                new MapStudioSceneReconstructionPlanBuilder()
                    .Build(
                        [candidate])
                    .Features);

        Assert.False(
            decision.AutoGenerate);

        Assert.True(
            decision.NeedsReview);
    }

    [Fact]
    public void IndependentStreetImageEvidenceRaisesFurnitureConfidence()
    {
        var osmCandidate =
            Assert.Single(
                new MapStudioOsmStreetFurnitureReconstructionAdapter()
                    .BuildCandidates(
                        [
                            new MapStudioGeoStreetFurniturePoint(
                                "shelter-1",
                                -23.55,
                                -46.63,
                                MapStudioOsmStreetFurnitureKind.BusShelter,
                                null,
                                "Transit",
                                "1001",
                                "public_transport")
                        ]));

        var fused =
            osmCandidate with
            {
                Evidence =
                    [
                        ..osmCandidate.Evidence,
                        new MapStudioSceneEvidence(
                            MapStudioSceneEvidenceSource.StreetLevelImagery,
                            0.90,
                            "street-image-1")
                    ]
            };

        var decision =
            Assert.Single(
                new MapStudioSceneReconstructionPlanBuilder()
                    .Build(
                        [fused])
                    .Features);

        Assert.True(
            decision.AutoGenerate);
    }
}
