using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Splines;
using MapStudio.Core.Omsi.Timetables;
using MapStudio.Core.ProtonBus;
using Xunit;

namespace MapStudio.ProtonBus.Tests;

public sealed class ProtonBusOmsiGpsRouteBuilderTests
{
    [Fact]
    public void BuilderCreatesGpsRibbonFromTrackReference()
    {
        var fixture =
            CreateFixture(
                pathIndex:
                    "0");

        var result =
            ProtonBusOmsiGpsRouteBuilder
                .BuildTrip(
                    fixture.TileOrder,
                    fixture.Timetable,
                    fixture.TileContents,
                    fixture.AssetsByTile,
                    "Trip Centro",
                    "10 Centro",
                    new(
                        Width:
                            1,
                        ElevationOffset:
                            0.05,
                        MaximumSampleLength:
                            5,
                        MaximumPartLength:
                            1000));

        Assert.True(
            result.IsComplete);

        Assert.Equal(
            1,
            result.ResolvedReferenceCount);

        Assert.Equal(
            1,
            result.TotalReferenceCount);

        var mesh =
            Assert.Single(
                result.Scene.Meshes);

        Assert.Equal(
            "_gps_10 Centro_",
            mesh.Name);

        Assert.DoesNotContain(
            ProtonBusMeshNameTags
                .Collider,
            mesh.Name,
            StringComparison.Ordinal);

        Assert.True(
            mesh.Vertices.Count >=
            10);

        Assert.True(
            mesh.Triangles.Count >=
            8);

        Assert.All(
            mesh.Vertices,
            vertex =>
                Assert.Equal(
                    0.15f,
                    vertex.Position.Y,
                    precision:
                        3));

        Assert.Empty(
            result.Issues);
    }

    [Fact]
    public void BuilderSplitsLongGpsRouteIntoNamedParts()
    {
        var fixture =
            CreateFixture(
                pathIndex:
                    "0");

        var result =
            ProtonBusOmsiGpsRouteBuilder
                .BuildTrip(
                    fixture.TileOrder,
                    fixture.Timetable,
                    fixture.TileContents,
                    fixture.AssetsByTile,
                    "Trip Centro",
                    "10 Centro",
                    new(
                        MaximumSampleLength:
                            2,
                        MaximumPartLength:
                            5));

        Assert.True(
            result.IsComplete);

        Assert.True(
            result.Scene.Meshes.Count >
            1);

        Assert.Equal(
            "_gps_10 Centro_",
            result
                .Scene
                .Meshes[0]
                .Name);

        Assert.Equal(
            "_gps_10 Centro_part.001",
            result
                .Scene
                .Meshes[1]
                .Name);

        Assert.All(
            result
                .Scene
                .Meshes,
            mesh =>
                Assert.True(
                    ProtonBusGpsRouteNamePlanner
                        .MatchesEntrypoint(
                            mesh.Name,
                            "10 Centro")));
    }

    [Fact]
    public void BuilderDoesNotEmitMisleadingMeshWhenRouteReferenceIsMissing()
    {
        var fixture =
            CreateFixture(
                pathIndex:
                    "9");

        var result =
            ProtonBusOmsiGpsRouteBuilder
                .BuildTrip(
                    fixture.TileOrder,
                    fixture.Timetable,
                    fixture.TileContents,
                    fixture.AssetsByTile,
                    "Trip Centro",
                    "10 Centro");

        Assert.False(
            result.IsComplete);

        Assert.Empty(
            result.Scene.Meshes);

        Assert.Contains(
            result.Issues,
            issue =>
                issue.Code ==
                "gpsSplinePathMissing");

        Assert.Contains(
            result.Issues,
            issue =>
                issue.Code ==
                "gpsRouteIncomplete");
    }

    private static Fixture CreateFixture(
        string pathIndex)
    {
        var tile =
            new OmsiTileReference(
                0,
                0,
                "tile.map");

        var spline =
            new OmsiPlacedSpline(
                HeaderValue:
                    "spline",
                SplinePath:
                    @"Splines\Test\road.sli",
                SplineId:
                    10,
                PreviousSplineId:
                    -1,
                NextSplineId:
                    -1,
                X:
                    0,
                Z:
                    0,
                Y:
                    0,
                Rotation:
                    0,
                Length:
                    20,
                Radius:
                    0,
                GradientStart:
                    0,
                GradientEnd:
                    0,
                IsHeightSpline:
                    false,
                ExtraValues:
                    []);

        var content =
            new OmsiTileContent(
                new(
                    true,
                    ObjectCount:
                        0,
                    SplineCount:
                        1,
                    SplineAttachmentCount:
                        0),
                [],
                [
                    spline
                ]);

        var definition =
            new OmsiSplineDefinition(
                true,
                [],
                [])
            {
                Paths =
                [
                    new(
                        Type:
                            0,
                        X:
                            0,
                        Z:
                            0.1,
                        Width:
                            3,
                        Direction:
                            0)
                ]
            };

        var assets =
            new ProtonBusOmsiAssetResolutionResult(
                new Dictionary<
                    string,
                    OmsiSplineDefinition>
                {
                    [
                        spline.SplinePath
                    ] =
                        definition
                },
                new Dictionary<
                    string,
                    ProtonBusResolvedSceneryAsset>(),
                [],
                []);

        var track =
            new OmsiTimetableTrack(
                FilePath:
                    "TrackA.ttr",
                RelativePath:
                    "TTData/TrackA.ttr",
                Name:
                    "TrackA",
                Comment1:
                    string.Empty,
                Comment2:
                    string.Empty,
                Entries:
                [
                    new(
                        Comment:
                            string.Empty,
                        Id:
                            10,
                        Line2:
                            pathIndex,
                        TileIndex:
                            0,
                        Line4:
                            string.Empty,
                        Length:
                            20,
                        Line6:
                            string.Empty,
                        Line7:
                            null)
                ]);

        var trip =
            new OmsiTimetableTrip(
                FilePath:
                    "trip.ttp",
                RelativePath:
                    "TTData/trip.ttp",
                Name:
                    "Trip Centro",
                Comment1:
                    string.Empty,
                Comment2:
                    string.Empty,
                TrackName:
                    "TrackA",
                Destination:
                    "Centro",
                Line:
                    "10",
                TrainReverse:
                    false,
                Stations:
                    [],
                ProfileLines:
                    []);

        var timetable =
            new OmsiTimetableCatalog(
                [
                    track
                ],
                [
                    trip
                ]);

        return new(
            [
                tile
            ],
            timetable,
            new Dictionary<
                (int X, int Y),
                OmsiTileContent>
            {
                [
                    (
                        0,
                        0
                    )
                ] =
                    content
            },
            new Dictionary<
                (int X, int Y),
                ProtonBusOmsiAssetResolutionResult>
            {
                [
                    (
                        0,
                        0
                    )
                ] =
                    assets
            });
    }

    private sealed record Fixture(
        IReadOnlyList<OmsiTileReference> TileOrder,
        OmsiTimetableCatalog Timetable,
        IReadOnlyDictionary<(int X, int Y), OmsiTileContent> TileContents,
        IReadOnlyDictionary<(int X, int Y), ProtonBusOmsiAssetResolutionResult> AssetsByTile);
}
