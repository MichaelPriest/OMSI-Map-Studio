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
    public void ImporterRecognizesPrioritySignsAndPreservesExplicitReference()
    {
        const string xml =
            """
            <osm version="0.6">
              <node id="10" lat="-23.55" lon="-46.63">
                <tag k="highway" v="stop"/>
                <tag k="direction" v="90"/>
              </node>
              <node id="20" lat="-23.5501" lon="-46.6301">
                <tag k="highway" v="give_way"/>
                <tag k="direction" v="270"/>
              </node>
              <node id="30" lat="-23.5502" lon="-46.6302">
                <tag k="traffic_sign" v="DE:206"/>
                <tag k="direction" v="180"/>
              </node>
            </osm>
            """;

        var result =
            new MapStudioOsmStreetFurnitureImporter()
                .Parse(
                    xml);

        Assert.Equal(
            3,
            result.Points.Count);

        Assert.All(
            result.Points,
            point =>
                Assert.Equal(
                    MapStudioOsmStreetFurnitureKind
                        .TrafficSign,
                    point.Kind));

        Assert.Contains(
            result.Points,
            point =>
                point.Reference ==
                    "stop");

        Assert.Contains(
            result.Points,
            point =>
                point.Reference ==
                    "give_way");

        Assert.Contains(
            result.Points,
            point =>
                point.Reference ==
                    "DE:206");
    }

    [Fact]
    public void ExplicitTrafficSignEvidenceCanAutoGenerate()
    {
        var candidate =
            Assert.Single(
                new MapStudioOsmStreetFurnitureReconstructionAdapter()
                    .BuildCandidates(
                        [
                            new MapStudioGeoStreetFurniturePoint(
                                "sign-explicit",
                                -23.55,
                                -46.63,
                                MapStudioOsmStreetFurnitureKind
                                    .TrafficSign,
                                null,
                                null,
                                "DE:206",
                                null,
                                90)
                        ]));

        var decision =
            Assert.Single(
                new MapStudioSceneReconstructionPlanBuilder()
                    .Build(
                        [candidate])
                    .Features);

        Assert.True(
            decision.AutoGenerate);

        Assert.False(
            decision.NeedsReview);
    }

    [Fact]
    public void ImporterResolvesForwardAndBackwardTrafficSignDirectionFromRoadWay()
    {
        const string xml =
            """
            <osm version="0.6">
              <node id="1" lat="-23.5500" lon="-46.6302"/>
              <node id="10" lat="-23.5500" lon="-46.6300">
                <tag k="highway" v="stop"/>
                <tag k="stop:direction" v="forward"/>
              </node>
              <node id="20" lat="-23.5500" lon="-46.6298">
                <tag k="highway" v="give_way"/>
                <tag k="give_way:direction" v="backward"/>
              </node>
              <node id="30" lat="-23.5500" lon="-46.6296"/>
              <way id="100">
                <nd ref="1"/>
                <nd ref="10"/>
                <nd ref="20"/>
                <nd ref="30"/>
                <tag k="highway" v="residential"/>
              </way>
            </osm>
            """;

        var points =
            new MapStudioOsmStreetFurnitureImporter()
                .Parse(
                    xml)
                .Points;

        var stop =
            Assert.Single(
                points,
                point =>
                    point.Reference ==
                        "stop");

        var giveWay =
            Assert.Single(
                points,
                point =>
                    point.Reference ==
                        "give_way");

        Assert.NotNull(
            stop.DirectionDegrees);

        Assert.NotNull(
            giveWay.DirectionDegrees);

        Assert.InRange(
            stop.DirectionDegrees!.Value,
            89.0,
            91.0);

        Assert.InRange(
            giveWay.DirectionDegrees!.Value,
            269.0,
            271.0);
    }

    [Fact]
    public void ImporterKeepsAmbiguousWayRelativeTrafficSignDirectionUnresolved()
    {
        const string xml =
            """
            <osm version="0.6">
              <node id="1" lat="-23.5500" lon="-46.6302"/>
              <node id="2" lat="-23.5500" lon="-46.6298"/>
              <node id="3" lat="-23.5502" lon="-46.6300"/>
              <node id="4" lat="-23.5498" lon="-46.6300"/>
              <node id="10" lat="-23.5500" lon="-46.6300">
                <tag k="highway" v="stop"/>
                <tag k="direction" v="forward"/>
              </node>
              <way id="100">
                <nd ref="1"/>
                <nd ref="10"/>
                <nd ref="2"/>
                <tag k="highway" v="residential"/>
              </way>
              <way id="200">
                <nd ref="3"/>
                <nd ref="10"/>
                <nd ref="4"/>
                <tag k="highway" v="secondary"/>
              </way>
            </osm>
            """;

        var sign =
            Assert.Single(
                new MapStudioOsmStreetFurnitureImporter()
                    .Parse(
                        xml)
                    .Points);

        Assert.Equal(
            "stop",
            sign.Reference);

        Assert.Null(
            sign.DirectionDegrees);
    }

    [Fact]
    public void ExplicitTrafficSignWithoutResolvedDirectionNeedsReview()
    {
        var candidate =
            Assert.Single(
                new MapStudioOsmStreetFurnitureReconstructionAdapter()
                    .BuildCandidates(
                        [
                            new MapStudioGeoStreetFurniturePoint(
                                "sign-unresolved-direction",
                                -23.55,
                                -46.63,
                                MapStudioOsmStreetFurnitureKind
                                    .TrafficSign,
                                null,
                                null,
                                "stop",
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
