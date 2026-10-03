using System.Globalization;
using System.Text;
using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Splines;
using MapStudio.Core.Omsi.Structures;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioRoadCurveAndTerrainTests
{
    [Fact]
    public async Task RoadRunnerWritesContinuousOmsiArcForDegreeTwoBend()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-road-curve-" +
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            var mapDirectory =
                await CreateMapAsync(
                    root,
                    includeTerrain:
                        false);

            var anchor =
                new MapStudioGeographicAnchor(
                    0,
                    0,
                    0,
                    0);

            var start =
                new MapStudioRoadPoint(
                    20,
                    30);

            var control =
                new MapStudioRoadPoint(
                    80,
                    30);

            var end =
                new MapStudioRoadPoint(
                    120,
                    50);

            var osm =
                BuildSingleWayOsm(
                    anchor,
                    start,
                    control,
                    end);

            var result =
                await new MapStudioRoadBatchReconstructionRunner()
                    .RunAsync(
                        root,
                        mapDirectory,
                        osm,
                        anchor);

            var placement =
                Assert.Single(
                    result.Placements);

            Assert.True(
                Math.Abs(
                    placement.RadiusMeters) >
                    3.0);

            var tile =
                await new OmsiTileReader()
                    .ReadContentAsync(
                        Path.Combine(
                            mapDirectory,
                            "tile_0_0.map"));

            var spline =
                Assert.Single(
                    tile.Splines);

            Assert.True(
                Math.Abs(
                    spline.Radius) >
                    3.0);

            var worldStart =
                new MapStudioRoadPoint(
                    OmsiTileGrid.GetOriginX(
                        placement.TileX) +
                        spline.X,
                    OmsiTileGrid.GetOriginZ(
                        placement.TileY) +
                        spline.Y);

            var calculatedEnd =
                MapStudioRoadArcGeometry
                    .ResolveEndPoint(
                        worldStart,
                        spline.Rotation,
                        spline.Length,
                        spline.Radius);

            Assert.InRange(
                calculatedEnd.DistanceTo(
                    end),
                0,
                0.05);
        }
        finally
        {
            if (Directory.Exists(
                    root))
            {
                Directory.Delete(
                    root,
                    recursive:
                        true);
            }
        }
    }

    [Fact]
    public async Task RoadTerrainConformBatchFollowsRoadAndLeavesFarTerrainUntouched()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-road-terrain-" +
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            var mapDirectory =
                await CreateMapAsync(
                    root,
                    includeTerrain:
                        true,
                    terrainHeight:
                        25);

            var tilePath =
                Path.Combine(
                    mapDirectory,
                    "tile_0_0.map");

            var placement =
                new MapStudioRoadBatchPlacement(
                    1,
                    "terrain-road",
                    1,
                    tilePath,
                    0,
                    0,
                    150,
                    50,
                    0,
                    100,
                    MapStudioStandardRoadCatalog
                        .RoadTwoLane
                        .RelativePath,
                    false,
                    false)
                {
                    RadiusMeters =
                        0,
                    StartHeightMeters =
                        5,
                    GradientStartPercent =
                        0,
                    GradientEndPercent =
                        0,
                    PhysicalWidthMeters =
                        7
                };

            var result =
                await new MapStudioRoadTerrainConformBatchApplier()
                    .ApplyAsync(
                        root,
                        mapDirectory,
                        [placement]);

            Assert.Equal(
                1,
                result.ModifiedTileCount);

            Assert.True(
                result.ChangedSamples >
                    0);

            var terrain =
                await new OmsiTerrainReader()
                    .ReadAsync(
                        tilePath +
                        ".terrain");

            var sampleCount =
                terrain.CellCount +
                1;

            var onRoadIndex =
                20 *
                    sampleCount +
                30;

            Assert.InRange(
                terrain.Heights[
                    onRoadIndex],
                4.879f,
                4.881f);

            Assert.Equal(
                25f,
                terrain.Heights[0]);

            Assert.True(
                File.Exists(
                    Assert.Single(
                        result.Tiles)
                        .BackupPath));
        }
        finally
        {
            if (Directory.Exists(
                    root))
            {
                Directory.Delete(
                    root,
                    recursive:
                        true);
            }
        }
    }

    [Fact]
    public void RoadArcGeometryRoundTripsCurvedEndpoint()
    {
        var start =
            new MapStudioRoadPoint(
                20,
                30);

        var control =
            new MapStudioRoadPoint(
                80,
                30);

        var end =
            new MapStudioRoadPoint(
                120,
                50);

        Assert.True(
            MapStudioRoadArcGeometry
                .TryCreate(
                    start,
                    control,
                    end,
                    out var arc));

        Assert.NotNull(
            arc);

        Assert.InRange(
            arc!.SweepDegrees,
            50,
            56);

        var calculated =
            MapStudioRoadArcGeometry
                .ResolveEndPoint(
                    start,
                    arc.RotationDegrees,
                    arc.LengthMeters,
                    arc.RadiusMeters);

        Assert.InRange(
            calculated.DistanceTo(
                end),
            0,
            0.0001);
    }

    private static async Task<string> CreateMapAsync(
        string root,
        bool includeTerrain,
        float terrainHeight = 0)
    {
        var mapDirectory =
            Path.Combine(
                root,
                "maps",
                "CurveTerrainTest");

        Directory.CreateDirectory(
            mapDirectory);

        await File.WriteAllTextAsync(
            Path.Combine(
                mapDirectory,
                "global.cfg"),
            "[name]\r\nCurve Terrain Test\r\n" +
            "[map]\r\n0\r\n0\r\ntile_0_0.map\r\n",
            Encoding.UTF8);

        var tilePath =
            Path.Combine(
                mapDirectory,
                "tile_0_0.map");

        await File.WriteAllTextAsync(
            tilePath,
            includeTerrain
                ? "[terrain]\r\n"
                : "[version]\r\n14\r\n",
            Encoding.UTF8);

        if (includeTerrain)
        {
            await File.WriteAllBytesAsync(
                tilePath +
                    ".terrain",
                OmsiTerrainWriter
                    .Write(
                        new OmsiTerrainGrid(
                            60,
                            Enumerable
                                .Repeat(
                                    terrainHeight,
                                    61 *
                                        61)
                                .ToArray())));
        }

        return mapDirectory;
    }

    private static string BuildSingleWayOsm(
        MapStudioGeographicAnchor anchor,
        params MapStudioRoadPoint[] points)
    {
        var nodes =
            points
                .Select(
                    (point, index) =>
                    {
                        var geographic =
                            MapStudioGeographicProjection
                                .Unproject(
                                    anchor,
                                    point);

                        return string.Create(
                            CultureInfo.InvariantCulture,
                            $"  <node id=\"{index + 1}\" lat=\"{geographic.Latitude:R}\" lon=\"{geographic.Longitude:R}\"/>");
                    });

        var refs =
            string.Join(
                string.Empty,
                points
                    .Select(
                        (_, index) =>
                            $"<nd ref=\"{index + 1}\"/>"));

        return
            "<osm version=\"0.6\">\n" +
            string.Join(
                "\n",
                nodes) +
            "\n  <way id=\"100\">\n    " +
            refs +
            "\n    <tag k=\"highway\" v=\"residential\"/>\n" +
            "    <tag k=\"lanes\" v=\"2\"/>\n" +
            "    <tag k=\"width\" v=\"7\"/>\n" +
            "  </way>\n</osm>";
    }
}
