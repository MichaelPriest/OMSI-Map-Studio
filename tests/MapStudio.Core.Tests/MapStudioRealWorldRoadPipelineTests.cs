using System.Net;
using System.Net.Http;
using System.Text;
using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Scene;
using MapStudio.Core.Generation.Terrain;
using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Indexing;
using MapStudio.Core.Omsi.Junctions;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Splines;
using MapStudio.Core.Omsi.Structures;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioRealWorldRoadPipelineTests
{
    [Fact]
    public async Task RoadRunnerWritesSelectedRoadKitSplinesInOneTileBatch()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-road-batch-" +
                Guid.NewGuid().ToString("N"));

        try
        {
            var mapDirectory =
                await CreateMapAsync(
                    root);

            var result =
                await new MapStudioRoadBatchReconstructionRunner()
                    .RunAsync(
                        root,
                        mapDirectory,
                        RoadXml,
                        new MapStudioGeographicAnchor(
                            -23.55000,
                            -46.63000,
                            50,
                            50));

            Assert.Equal(
                2,
                result.PlacedSplineCount);

            Assert.Equal(
                1,
                result.ModifiedTileCount);

            Assert.Equal(
                0,
                result.SkippedOutsideMapSegmentCount);

            Assert.Single(
                result.BackupPaths);

            Assert.All(
                result.Placements,
                placement =>
                    Assert.EndsWith(
                        ".sli",
                        placement.SplinePath,
                        StringComparison.OrdinalIgnoreCase));

            Assert.Contains(
                result.Placements,
                placement =>
                    placement.SplinePath.Contains(
                        "sidewalk",
                        StringComparison.OrdinalIgnoreCase));

            Assert.True(
                Directory.Exists(
                    result.RoadKit.PackDirectory));

            var document =
                await OmsiConfigParser
                    .ParseFileAsync(
                        Path.Combine(
                            mapDirectory,
                            "tile_0_0.map"));

            Assert.Equal(
                2,
                document
                    .FindSections(
                        "spline")
                    .Count());

            var tileContent =
                await new OmsiTileReader()
                    .ReadContentAsync(
                        Path.Combine(
                            mapDirectory,
                            "tile_0_0.map"));

            foreach (var placement in result.Placements)
            {
                var spline =
                    Assert.Single(
                        tileContent.Splines,
                        item =>
                            item.SplineId ==
                            placement.SplineId);

                Assert.Equal(
                    placement.LocalX,
                    spline.X,
                    6);

                Assert.Equal(
                    0,
                    spline.Z,
                    6);

                Assert.Equal(
                    placement.LocalZ,
                    spline.Y,
                    6);
            }

            var ordered =
                result.Placements
                    .OrderBy(
                        placement =>
                            placement.GraphSegmentId)
                    .ToArray();

            var first =
                ordered[0];

            var second =
                ordered[1];

            var yaw =
                first.Rotation *
                Math.PI /
                180.0;

            var firstEndX =
                OmsiTileGrid.GetOriginX(
                    first.TileX) +
                first.LocalX +
                Math.Sin(
                    yaw) *
                first.LengthMeters;

            var firstEndZ =
                OmsiTileGrid.GetOriginZ(
                    first.TileY) +
                first.LocalZ +
                Math.Cos(
                    yaw) *
                first.LengthMeters;

            var secondStartX =
                OmsiTileGrid.GetOriginX(
                    second.TileX) +
                second.LocalX;

            var secondStartZ =
                OmsiTileGrid.GetOriginZ(
                    second.TileY) +
                second.LocalZ;

            Assert.InRange(
                Math.Abs(
                    firstEndX -
                    secondStartX),
                0,
                0.001);

            Assert.InRange(
                Math.Abs(
                    firstEndZ -
                    secondStartZ),
                0,
                0.001);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }

    [Fact]
    public async Task RoadRunnerClipsCrossingRoadToCreatedMapBounds()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-road-clip-" +
                Guid.NewGuid().ToString("N"));

        try
        {
            var mapDirectory =
                await CreateMapAsync(
                    root);

            var result =
                await new MapStudioRoadBatchReconstructionRunner()
                    .RunAsync(
                        root,
                        mapDirectory,
                        CrossingRoadXml,
                        new MapStudioGeographicAnchor(
                            -23.55000,
                            -46.63000,
                            150,
                            150));

            var placement =
                Assert.Single(
                    result.Placements);

            Assert.Equal(
                0,
                placement.TileX);

            Assert.Equal(
                0,
                placement.TileY);

            Assert.InRange(
                placement.LocalX,
                0.0,
                300.0);

            Assert.InRange(
                placement.LocalZ,
                0.0,
                300.0);

            Assert.InRange(
                placement.LengthMeters,
                295,
                300);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }

    [Fact]
    public async Task RoadRunnerPrefersInstalledOmsiSplineOverRoadKitFallback()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-road-installed-omsi-" +
                Guid.NewGuid().ToString("N"));

        try
        {
            var mapDirectory =
                await CreateMapAsync(
                    root);

            await new MapStudioRoadKitGenerator()
                .InstallOrUpdateAsync(
                    root);

            var sourceRelativePath =
                MapStudioStandardRoadCatalog
                    .RoadTwoLaneWithSidewalk
                    .RelativePath;

            var sourcePath =
                Path.Combine(
                    root,
                    sourceRelativePath.Replace(
                        '\\',
                        Path.DirectorySeparatorChar));

            var installedRelativePath =
                @"Splines\Marcel\str_2spur_11m.sli";

            var installedPath =
                Path.Combine(
                    root,
                    installedRelativePath.Replace(
                        '\\',
                        Path.DirectorySeparatorChar));

            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    installedPath)!);

            File.Copy(
                sourcePath,
                installedPath,
                overwrite:
                    true);

            var entry =
                new OmsiAssetIndexEntry(
                    installedRelativePath,
                    OmsiAssetKind.Spline,
                    new FileInfo(
                        installedPath)
                        .Length,
                    File.GetLastWriteTimeUtc(
                        installedPath)
                        .Ticks);

            var catalog =
                MapStudioOmsiConstructionAssetClassifier
                    .Build(
                        [entry]);

            var result =
                await new MapStudioRoadBatchReconstructionRunner()
                    .RunAsync(
                        root,
                        mapDirectory,
                        RoadXml,
                        new MapStudioGeographicAnchor(
                            -23.55000,
                            -46.63000,
                            50,
                            50),
                        installedRoadSplines:
                            catalog.RoadSplines);

            Assert.Equal(
                2,
                result.PlacedSplineCount);

            Assert.Equal(
                2,
                result.InstalledOmsiSplineCount);

            Assert.Equal(
                0,
                result.RoadKitFallbackSplineCount);

            Assert.All(
                result.Placements,
                placement =>
                    Assert.Equal(
                        installedRelativePath,
                        placement.SplinePath,
                        ignoreCase:
                            true));

            var tileContent =
                await new OmsiTileReader()
                    .ReadContentAsync(
                        Path.Combine(
                            mapDirectory,
                            "tile_0_0.map"));

            Assert.All(
                tileContent.Splines,
                spline =>
                    Assert.Equal(
                        installedRelativePath,
                        spline.SplinePath,
                        ignoreCase:
                            true));
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
    public async Task RoadRunnerUsesDemHeightAndGradientForSplinePlacement()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-road-elevation-" +
                Guid.NewGuid().ToString("N"));

        try
        {
            var mapDirectory =
                await CreateMapAsync(
                    root);

            var anchor =
                new MapStudioGeographicAnchor(
                    -23.55000,
                    -46.63000,
                    50,
                    50);

            var elevation =
                new MapStudioGeoreferencedElevationSurface(
                    new MapStudioElevationGrid(
                        2,
                        2,
                        [
                            100,
                            100,
                            103,
                            103
                        ],
                        100,
                        103,
                        "test"),
                    south: -23.55020,
                    west: -46.63010,
                    north: -23.54990,
                    east: -46.62990);

            var result =
                await new MapStudioRoadBatchReconstructionRunner()
                    .RunAsync(
                        root,
                        mapDirectory,
                        ElevationRoadXml,
                        anchor,
                        CancellationToken.None,
                        elevation);

            var placement =
                Assert.Single(
                    result.Placements);

            var content =
                await new OmsiTileReader()
                    .ReadContentAsync(
                        Path.Combine(
                            mapDirectory,
                            "tile_0_0.map"));

            var spline =
                Assert.Single(
                    content.Splines);

            Assert.Equal(
                placement.LocalZ,
                spline.Y,
                6);

            Assert.InRange(
                spline.Z,
                0.8,
                1.2);

            Assert.InRange(
                spline.GradientStart,
                7.0,
                11.0);

            Assert.Equal(
                spline.GradientStart,
                spline.GradientEnd,
                6);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }

    [Fact]
    public async Task RoadRunnerAppliesBridgeAndTunnelVerticalSeparation()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-road-structures-" +
                Guid.NewGuid().ToString("N"));

        try
        {
            var mapDirectory =
                await CreateMapAsync(
                    root);

            var anchor =
                new MapStudioGeographicAnchor(
                    -23.55000,
                    -46.63000,
                    50,
                    100);

            var elevation =
                new MapStudioGeoreferencedElevationSurface(
                    new MapStudioElevationGrid(
                        2,
                        2,
                        [
                            100,
                            100,
                            100,
                            100
                        ],
                        100,
                        100,
                        "flat-test"),
                    south: -23.55030,
                    west: -46.63010,
                    north: -23.54990,
                    east: -46.62830);

            var result =
                await new MapStudioRoadBatchReconstructionRunner()
                    .RunAsync(
                        root,
                        mapDirectory,
                        StructuralRoadXml,
                        anchor,
                        CancellationToken.None,
                        elevation);

            var bridgePlacements =
                result.Placements
                    .Where(
                        placement =>
                            placement.Bridge)
                    .OrderBy(
                        placement =>
                            placement.SplineId)
                    .ToArray();

            var tunnelPlacements =
                result.Placements
                    .Where(
                        placement =>
                            placement.Tunnel)
                    .OrderBy(
                        placement =>
                            placement.SplineId)
                    .ToArray();

            Assert.Equal(
                2,
                bridgePlacements.Length);

            Assert.Equal(
                2,
                tunnelPlacements.Length);

            Assert.All(
                bridgePlacements,
                placement =>
                    Assert.EndsWith(
                        "_bridge.sli",
                        placement.SplinePath,
                        StringComparison
                            .OrdinalIgnoreCase));

            Assert.All(
                tunnelPlacements,
                placement =>
                    Assert.EndsWith(
                        "_tunnel.sli",
                        placement.SplinePath,
                        StringComparison
                            .OrdinalIgnoreCase));

            var content =
                await new OmsiTileReader()
                    .ReadContentAsync(
                        Path.Combine(
                            mapDirectory,
                            "tile_0_0.map"));

            var bridgeSecond =
                Assert.Single(
                    content.Splines,
                    spline =>
                        spline.SplineId ==
                        bridgePlacements[1]
                            .SplineId);

            var tunnelSecond =
                Assert.Single(
                    content.Splines,
                    spline =>
                        spline.SplineId ==
                        tunnelPlacements[1]
                            .SplineId);

            Assert.InRange(
                bridgeSecond.Z,
                4.79,
                4.81);

            Assert.InRange(
                tunnelSecond.Z,
                -4.81,
                -4.79);

            Assert.True(
                bridgeSecond.GradientStart <
                0);

            Assert.True(
                tunnelSecond.GradientStart >
                0);

            Assert.NotNull(
                result.StructurePlacements);

            var structures =
                result.StructurePlacements!;

            var bridgePiers =
                structures
                    .Where(
                        placement =>
                            placement.Id.StartsWith(
                                "osm-bridge-pier-",
                                StringComparison.Ordinal))
                    .ToArray();

            var tunnelPortals =
                structures
                    .Where(
                        placement =>
                            placement.Id.StartsWith(
                                "osm-tunnel-portal-",
                                StringComparison.Ordinal))
                    .ToArray();

            Assert.NotEmpty(
                bridgePiers);

            Assert.Equal(
                2,
                tunnelPortals.Length);

            Assert.Equal(
                structures.Count,
                result.GeneratedStructureCount);

            Assert.All(
                bridgePiers,
                placement =>
                {
                    Assert.Contains(
                        "MS_BridgePier_",
                        placement.SceneryObjectPath,
                        StringComparison.OrdinalIgnoreCase);

                    Assert.True(
                        File.Exists(
                            Path.Combine(
                                root,
                                placement.SceneryObjectPath
                                    .Replace(
                                        '\\',
                                        Path.DirectorySeparatorChar))));
                });

            Assert.All(
                tunnelPortals,
                placement =>
                {
                    Assert.Contains(
                        "MS_TunnelPortal_",
                        placement.SceneryObjectPath,
                        StringComparison.OrdinalIgnoreCase);

                    Assert.True(
                        File.Exists(
                            Path.Combine(
                                root,
                                placement.SceneryObjectPath
                                    .Replace(
                                        '\\',
                                        Path.DirectorySeparatorChar))));
                });

            Assert.Equal(
                structures.Count,
                content.Objects.Count);

            Assert.Contains(
                content.Objects,
                item =>
                    item.SceneryObjectPath.Contains(
                        "MS_BridgePier_",
                        StringComparison.OrdinalIgnoreCase));

            Assert.Equal(
                2,
                content.Objects.Count(
                    item =>
                        item.SceneryObjectPath.Contains(
                            "MS_TunnelPortal_",
                            StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }

    [Fact]
    public async Task RoadRunnerGeneratesPhysicalJunctionAtSharedOsmNode()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-road-junction-" +
                Guid.NewGuid().ToString("N"));

        try
        {
            var mapDirectory =
                await CreateMapAsync(
                    root);

            var result =
                await new MapStudioRoadBatchReconstructionRunner()
                    .RunAsync(
                        root,
                        mapDirectory,
                        JunctionXml,
                        new MapStudioGeographicAnchor(
                            -23.55000,
                            -46.63000,
                            50,
                            50));

            Assert.Equal(
                1,
                result.JunctionCandidateCount);

            var junction =
                Assert.Single(
                    result.JunctionPlacements);

            Assert.Equal(
                "osm-junction-3",
                junction.Id);

            Assert.Equal(
                1,
                result.GeneratedJunctionCount);

            Assert.True(
                File.Exists(
                    Path.Combine(
                        root,
                        junction.SceneryObjectPath.Replace(
                            '\\',
                            Path.DirectorySeparatorChar))));

            var tile =
                await OmsiConfigParser
                    .ParseFileAsync(
                        Path.Combine(
                            mapDirectory,
                            "tile_0_0.map"));

            Assert.NotEmpty(
                tile.FindSections(
                    "spline"));

            Assert.Single(
                tile.FindSections(
                    "object"));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }

    [Fact]
    public async Task RoadRunnerUsesOriginalOmsiJunctionAndTrimsConnectedSplines()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-road-stock-junction-" +
                Guid.NewGuid().ToString("N"));

        try
        {
            var mapDirectory =
                await CreateMapAsync(
                    root);

            var relativePath =
                @"Sceneryobjects\Kreuz_MC\Kreuz_Test_4arm.sco";

            MapStudioOmsiJunctionResolverTests
                .WriteStockJunction(
                    root,
                    relativePath,
                    [0, 90, 180, 270],
                    8.0);

            var fullPath =
                Path.Combine(
                    root,
                    relativePath.Replace(
                        '\\',
                        Path.DirectorySeparatorChar));

            var entry =
                new OmsiAssetIndexEntry(
                    relativePath,
                    OmsiAssetKind.SceneryObject,
                    new FileInfo(fullPath).Length,
                    File.GetLastWriteTimeUtc(fullPath).Ticks);

            var catalog =
                MapStudioOmsiConstructionAssetClassifier
                    .Build(
                        [entry]);

            var result =
                await new MapStudioRoadBatchReconstructionRunner()
                    .RunAsync(
                        root,
                        mapDirectory,
                        WideJunctionXml,
                        new MapStudioGeographicAnchor(
                            -23.55000,
                            -46.63000,
                            150,
                            150),
                        installedJunctionObjects:
                            catalog.JunctionObjects);

            Assert.Equal(
                1,
                result.InstalledOmsiJunctionCount);

            Assert.Equal(
                0,
                result.GeneratedJunctionCount);

            var junction =
                Assert.Single(
                    result.JunctionPlacements);

            Assert.Equal(
                relativePath,
                junction.SceneryObjectPath,
                ignoreCase:
                    true);

            var content =
                await new OmsiTileReader()
                    .ReadContentAsync(
                        Path.Combine(
                            mapDirectory,
                            "tile_0_0.map"));

            var placedObject =
                Assert.Single(
                    content.Objects);

            Assert.Equal(
                relativePath,
                placedObject.SceneryObjectPath,
                ignoreCase:
                    true);

            Assert.Equal(
                4,
                content.Splines.Count);

            Assert.All(
                content.Splines,
                spline =>
                    Assert.InRange(
                        spline.Length,
                        10.0,
                        18.0));

            Assert.DoesNotContain(
                content.Objects,
                item =>
                    item.SceneryObjectPath.Contains(
                        MapStudioJunctionAssetGenerator
                            .RootFolderName,
                        StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive:
                        true);
            }
        }
    }

    [Fact]
    public async Task RoadRunnerPlacesStockSignalsAndCrosswalkFromOsmControls()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-road-stock-controls-" +
                Guid.NewGuid().ToString("N"));

        try
        {
            var mapDirectory =
                await CreateMapAsync(
                    root);

            var junctionRelativePath =
                @"Sceneryobjects\Kreuz_MC\Kreuz_Test_4arm.sco";

            MapStudioOmsiJunctionResolverTests
                .WriteStockJunction(
                    root,
                    junctionRelativePath,
                    [0, 90, 180, 270],
                    8.0);

            var junctionFullPath =
                Path.Combine(
                    root,
                    junctionRelativePath.Replace(
                        '\\',
                        Path.DirectorySeparatorChar));

            var assets =
                new List<OmsiAssetIndexEntry>
                {
                    new(
                        junctionRelativePath,
                        OmsiAssetKind.SceneryObject,
                        new FileInfo(
                            junctionFullPath)
                            .Length,
                        File.GetLastWriteTimeUtc(
                            junctionFullPath)
                            .Ticks)
                };

            AddAsset(
                root,
                assets,
                @"Sceneryobjects\Verkehrszeichen_MC\Ampel_Test.sco");

            AddAsset(
                root,
                assets,
                @"Sceneryobjects\Kreuz_MC\Zebra_Test.sco");

            var catalog =
                MapStudioOmsiConstructionAssetClassifier
                    .Build(
                        assets);

            var result =
                await new MapStudioRoadBatchReconstructionRunner()
                    .RunAsync(
                        root,
                        mapDirectory,
                        ControlledWideJunctionXml,
                        new MapStudioGeographicAnchor(
                            -23.55000,
                            -46.63000,
                            150,
                            150),
                        installedJunctionObjects:
                            catalog.JunctionObjects,
                        installedTrafficSignalObjects:
                            catalog.TrafficSignalObjects,
                        installedCrosswalkAssets:
                            catalog.CrosswalkAssets);

            Assert.Equal(
                1,
                result.InstalledOmsiJunctionCount);

            Assert.Equal(
                4,
                result.InstalledTrafficSignalCount);

            Assert.Equal(
                1,
                result.InstalledCrosswalkCount);

            var content =
                await new OmsiTileReader()
                    .ReadContentAsync(
                        Path.Combine(
                            mapDirectory,
                            "tile_0_0.map"));

            Assert.Equal(
                6,
                content.Objects.Count);

            Assert.Equal(
                4,
                content.Objects.Count(
                    item =>
                        item.SceneryObjectPath.Contains(
                            "Ampel_Test.sco",
                            StringComparison.OrdinalIgnoreCase)));

            Assert.Single(
                content.Objects,
                item =>
                    item.SceneryObjectPath.Contains(
                        "Zebra_Test.sco",
                        StringComparison.OrdinalIgnoreCase));

            Assert.DoesNotContain(
                content.Objects,
                item =>
                    item.SceneryObjectPath.Contains(
                        MapStudioJunctionAssetGenerator
                            .RootFolderName,
                        StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive:
                        true);
            }
        }
    }

    [Fact]
    public async Task FullMapPipelineDownloadsOnceAndCreatesRoadsAndScene()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-full-map-pipeline-" +
                Guid.NewGuid().ToString("N"));

        try
        {
            var mapDirectory =
                await CreateMapAsync(
                    root);

            var assets =
                new List<OmsiAssetIndexEntry>();

            AddAsset(
                root,
                assets,
                @"Sceneryobjects\Vegetation\Tipuana\tipuana_tree.sco");

            using var httpClient =
                new HttpClient(
                    new DelegateHandler(
                        _ =>
                            XmlResponse(
                                FullSceneXml)))
                {
                    Timeout =
                        TimeSpan.FromSeconds(5)
                };

            var client =
                new MapStudioOverpassSceneClient(
                    httpClient,
                    [
                        new Uri(
                            "https://scene.test/api/interpreter")
                    ]);

            var progress =
                new List<MapStudioRealWorldMapPipelineProgress>();

            var result =
                await new MapStudioRealWorldMapPipeline(
                        client)
                    .RunAsync(
                        root,
                        mapDirectory,
                        -23.551,
                        -46.631,
                        -23.549,
                        -46.629,
                        new MapStudioGeographicAnchor(
                            -23.55000,
                            -46.63000,
                            50,
                            50),
                        assets,
                        progress:
                            new Progress<MapStudioRealWorldMapPipelineProgress>(
                                item =>
                                    progress.Add(
                                        item)));

            Assert.True(
                result.Roads.PlacedSplineCount >
                    0);

            Assert.True(
                result.Scene.PlacedObjectCount >
                    0);

            Assert.True(
                result.PlacedElementCount >=
                    2);

            Assert.True(
                Directory.Exists(
                    result.SessionBackupDirectory));

            var tile =
                await OmsiConfigParser
                    .ParseFileAsync(
                        Path.Combine(
                            mapDirectory,
                            "tile_0_0.map"));

            Assert.NotEmpty(
                tile.FindSections(
                    "spline"));

            Assert.NotEmpty(
                tile.FindSections(
                    "object"));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }

    [Fact]
    public async Task FullMapPipelineExpandsThinSelectionToPhysicalTileCoverage()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-full-map-physical-coverage-" +
                Guid.NewGuid().ToString("N"));

        try
        {
            var mapDirectory =
                await CreateMapAsync(
                    root);

            string? query =
                null;

            using var httpClient =
                new HttpClient(
                    new DelegateHandler(
                        request =>
                        {
                            query =
                                ReadQuery(
                                    request);

                            return XmlResponse(
                                FullSceneXml);
                        }))
                {
                    Timeout =
                        TimeSpan.FromSeconds(5)
                };

            var client =
                new MapStudioOverpassSceneClient(
                    httpClient,
                    [
                        new Uri(
                            "https://scene.test/api/interpreter")
                    ]);

            var result =
                await new MapStudioRealWorldMapPipeline(
                        client)
                    .RunAsync(
                        root,
                        mapDirectory,
                        -23.55001,
                        -46.63001,
                        -23.54999,
                        -46.62999,
                        new MapStudioGeographicAnchor(
                            -23.55000,
                            -46.63000,
                            150,
                            150),
                        Array.Empty<
                            OmsiAssetIndexEntry>());

            Assert.NotNull(
                query);

            var marker =
                "way[\"highway\"](";

            var start =
                query!.IndexOf(
                    marker,
                    StringComparison.Ordinal);

            Assert.True(
                start >= 0);

            start +=
                marker.Length;

            var end =
                query.IndexOf(
                    ");",
                    start,
                    StringComparison.Ordinal);

            Assert.True(
                end >
                start);

            var parts =
                query[start..end]
                    .Split(
                        ',',
                        StringSplitOptions
                            .TrimEntries);

            Assert.Equal(
                4,
                parts.Length);

            var south =
                double.Parse(
                    parts[0],
                    System.Globalization
                        .CultureInfo
                        .InvariantCulture);

            var west =
                double.Parse(
                    parts[1],
                    System.Globalization
                        .CultureInfo
                        .InvariantCulture);

            var north =
                double.Parse(
                    parts[2],
                    System.Globalization
                        .CultureInfo
                        .InvariantCulture);

            var east =
                double.Parse(
                    parts[3],
                    System.Globalization
                        .CultureInfo
                        .InvariantCulture);

            Assert.True(
                north -
                    south >
                0.002);

            Assert.True(
                east -
                    west >
                0.002);

            Assert.True(
                result.PlacedElementCount >
                0);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }

    [Fact]
    public async Task FullMapPipelineRejectsZeroPlacementSuccessAndRestoresTile()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-full-map-zero-placement-" +
                Guid.NewGuid().ToString("N"));

        try
        {
            var mapDirectory =
                await CreateMapAsync(
                    root);

            var tilePath =
                Path.Combine(
                    mapDirectory,
                    "tile_0_0.map");

            var original =
                await File.ReadAllBytesAsync(
                    tilePath);

            using var httpClient =
                new HttpClient(
                    new DelegateHandler(
                        _ =>
                            XmlResponse(
                                """
                                <osm version="0.6">
                                  <node id="1" lat="-23.55000" lon="-46.63000"/>
                                </osm>
                                """)))
                {
                    Timeout =
                        TimeSpan.FromSeconds(5)
                };

            var exception =
                await Assert.ThrowsAsync<
                    InvalidDataException>(
                    () =>
                        new MapStudioRealWorldMapPipeline(
                                new MapStudioOverpassSceneClient(
                                    httpClient,
                                    [
                                        new Uri(
                                            "https://scene.test/api/interpreter")
                                    ]))
                            .RunAsync(
                                root,
                                mapDirectory,
                                -23.551,
                                -46.631,
                                -23.549,
                                -46.629,
                                new MapStudioGeographicAnchor(
                                    -23.55000,
                                    -46.63000,
                                    150,
                                    150),
                                Array.Empty<
                                    OmsiAssetIndexEntry>()));

            Assert.Contains(
                "nenhuma via",
                exception.Message,
                StringComparison
                    .OrdinalIgnoreCase);

            Assert.Equal(
                original,
                await File.ReadAllBytesAsync(
                    tilePath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }

    [Fact]
    public void SplineBatchInserterAppendsMultipleSplinesInOnePass()
    {
        var document =
            OmsiConfigParser
                .Parse(
                    "[version]\r\n14\r\n");

        var result =
            OmsiTileSplineInserter
                .AppendMany(
                    document,
                    [
                        CreateSpline(
                            10,
                            0,
                            0,
                            20,
                            90),
                        CreateSpline(
                            11,
                            20,
                            0,
                            20,
                            90)
                    ]);

        var text =
            Encoding.UTF8
                .GetString(
                    result.Bytes);

        Assert.Equal(
            2,
            result.SourceSectionOrdinals.Count);

        Assert.Equal(
            2,
            text.Split(
                    "[spline]",
                    StringSplitOptions.None)
                .Length -
                1);
    }

    private static OmsiNewPlacedSpline CreateSpline(
        int id,
        double x,
        double z,
        double length,
        double rotation) =>
        new(
            "0",
            @"Splines\MapStudio_RoadKit\ms_road_2lane_7m.sli",
            id,
            -1,
            -1,
            x,
            0,
            z,
            rotation,
            length,
            0,
            0,
            0,
            false,
            Array.Empty<string>());

    private static async Task<string> CreateMapAsync(
        string root)
    {
        var mapDirectory =
            Path.Combine(
                root,
                "maps",
                "RoadTest");

        Directory.CreateDirectory(
            mapDirectory);

        await File.WriteAllTextAsync(
            Path.Combine(
                mapDirectory,
                "global.cfg"),
            "[name]\r\nRoad Test\r\n" +
            "[map]\r\n0\r\n0\r\ntile_0_0.map\r\n",
            Encoding.UTF8);

        await File.WriteAllTextAsync(
            Path.Combine(
                mapDirectory,
                "tile_0_0.map"),
            "[version]\r\n14\r\n",
            Encoding.UTF8);

        return mapDirectory;
    }

    private static void AddAsset(
        string root,
        ICollection<OmsiAssetIndexEntry> assets,
        string relativePath)
    {
        var fullPath =
            Path.Combine(
                root,
                relativePath.Replace(
                    '\\',
                    Path.DirectorySeparatorChar));

        Directory.CreateDirectory(
            Path.GetDirectoryName(
                fullPath)!);

        File.WriteAllText(
            fullPath,
            "[friendlyname]\r\nAsset\r\n",
            Encoding.ASCII);

        assets.Add(
            new OmsiAssetIndexEntry(
                relativePath,
                OmsiAssetKind.SceneryObject,
                1,
                1));
    }

    private static string ReadQuery(
        HttpRequestMessage request)
    {
        var body =
            request.Content!
                .ReadAsStringAsync()
                .GetAwaiter()
                .GetResult();

        var encoded =
            body.StartsWith(
                "data=",
                StringComparison.Ordinal)
                ? body[5..]
                : body;

        return Uri.UnescapeDataString(
            encoded.Replace(
                '+',
                ' '));
    }

    private static HttpResponseMessage XmlResponse(
        string xml) =>
        new(
            HttpStatusCode.OK)
        {
            Content =
                new StringContent(
                    xml,
                    Encoding.UTF8,
                    "application/xml")
        };

    private sealed class DelegateHandler
        : HttpMessageHandler
    {
        private readonly Func<
            HttpRequestMessage,
            HttpResponseMessage>
            _handler;

        public DelegateHandler(
            Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler =
                handler;
        }

        protected override Task<HttpResponseMessage>
            SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken) =>
            Task.FromResult(
                _handler(
                    request));
    }

    private const string RoadXml =
        """
        <osm version="0.6">
          <node id="1" lat="-23.55000" lon="-46.63000"/>
          <node id="2" lat="-23.55000" lon="-46.62990"/>
          <node id="3" lat="-23.55000" lon="-46.62980"/>
          <way id="100">
            <nd ref="1"/><nd ref="2"/><nd ref="3"/>
            <tag k="highway" v="residential"/>
            <tag k="lanes" v="2"/>
            <tag k="width" v="7"/>
          </way>
        </osm>
        """;

    private const string CrossingRoadXml =
        """
        <osm version="0.6">
          <node id="1" lat="-23.55000" lon="-46.63200"/>
          <node id="2" lat="-23.55000" lon="-46.62800"/>
          <way id="125">
            <nd ref="1"/><nd ref="2"/>
            <tag k="highway" v="primary"/>
            <tag k="lanes" v="2"/>
            <tag k="width" v="7"/>
          </way>
        </osm>
        """;

    private const string ElevationRoadXml =
        """
        <osm version="0.6">
          <node id="1" lat="-23.55000" lon="-46.63000"/>
          <node id="2" lat="-23.55010" lon="-46.63000"/>
          <way id="150">
            <nd ref="1"/><nd ref="2"/>
            <tag k="highway" v="residential"/>
            <tag k="lanes" v="2"/>
            <tag k="width" v="7"/>
          </way>
        </osm>
        """;

    private const string StructuralRoadXml =
        """
        <osm version="0.6">
          <node id="1" lat="-23.55000" lon="-46.63000"/>
          <node id="2" lat="-23.55000" lon="-46.62920"/>
          <node id="3" lat="-23.55000" lon="-46.62840"/>
          <node id="4" lat="-23.55020" lon="-46.63000"/>
          <node id="5" lat="-23.55020" lon="-46.62920"/>
          <node id="6" lat="-23.55020" lon="-46.62840"/>

          <way id="300">
            <nd ref="1"/><nd ref="2"/><nd ref="3"/>
            <tag k="highway" v="primary"/>
            <tag k="lanes" v="2"/>
            <tag k="width" v="7"/>
            <tag k="bridge" v="yes"/>
          </way>

          <way id="400">
            <nd ref="4"/><nd ref="5"/><nd ref="6"/>
            <tag k="highway" v="primary"/>
            <tag k="lanes" v="2"/>
            <tag k="width" v="7"/>
            <tag k="tunnel" v="yes"/>
          </way>
        </osm>
        """;

    private const string JunctionXml =
        """
        <osm version="0.6">
          <node id="1" lat="-23.55000" lon="-46.63010"/>
          <node id="2" lat="-23.55000" lon="-46.63005"/>
          <node id="3" lat="-23.55000" lon="-46.63000"/>
          <node id="4" lat="-23.55000" lon="-46.62995"/>
          <node id="5" lat="-23.55000" lon="-46.62990"/>
          <node id="6" lat="-23.55010" lon="-46.63000"/>
          <node id="7" lat="-23.55005" lon="-46.63000"/>
          <node id="8" lat="-23.54995" lon="-46.63000"/>
          <node id="9" lat="-23.54990" lon="-46.63000"/>

          <way id="100">
            <nd ref="1"/><nd ref="2"/><nd ref="3"/><nd ref="4"/><nd ref="5"/>
            <tag k="highway" v="residential"/>
            <tag k="lanes" v="2"/>
            <tag k="width" v="7"/>
          </way>

          <way id="200">
            <nd ref="6"/><nd ref="7"/><nd ref="3"/><nd ref="8"/><nd ref="9"/>
            <tag k="highway" v="secondary"/>
            <tag k="lanes" v="2"/>
            <tag k="width" v="7"/>
          </way>
        </osm>
        """;

    private const string WideJunctionXml =
        """
        <osm version="0.6">
          <node id="1" lat="-23.55000" lon="-46.63020"/>
          <node id="3" lat="-23.55000" lon="-46.63000"/>
          <node id="5" lat="-23.55000" lon="-46.62980"/>
          <node id="6" lat="-23.55020" lon="-46.63000"/>
          <node id="9" lat="-23.54980" lon="-46.63000"/>

          <way id="100">
            <nd ref="1"/><nd ref="3"/><nd ref="5"/>
            <tag k="highway" v="residential"/>
            <tag k="lanes" v="2"/>
            <tag k="width" v="7"/>
          </way>

          <way id="200">
            <nd ref="6"/><nd ref="3"/><nd ref="9"/>
            <tag k="highway" v="secondary"/>
            <tag k="lanes" v="2"/>
            <tag k="width" v="7"/>
          </way>
        </osm>
        """;

    private const string ControlledWideJunctionXml =
        """
        <osm version="0.6">
          <node id="1" lat="-23.55000" lon="-46.63020"/>
          <node id="3" lat="-23.55000" lon="-46.63000">
            <tag k="highway" v="traffic_signals"/>
          </node>
          <node id="5" lat="-23.55000" lon="-46.62980"/>
          <node id="6" lat="-23.55020" lon="-46.63000"/>
          <node id="9" lat="-23.54980" lon="-46.63000"/>
          <node id="10" lat="-23.55000" lon="-46.62990">
            <tag k="highway" v="crossing"/>
            <tag k="crossing" v="marked"/>
          </node>

          <way id="100">
            <nd ref="1"/><nd ref="3"/><nd ref="5"/>
            <tag k="highway" v="residential"/>
            <tag k="lanes" v="2"/>
            <tag k="width" v="7"/>
          </way>

          <way id="200">
            <nd ref="6"/><nd ref="3"/><nd ref="9"/>
            <tag k="highway" v="secondary"/>
            <tag k="lanes" v="2"/>
            <tag k="width" v="7"/>
          </way>
        </osm>
        """;

    private const string FullSceneXml =
        """
        <osm version="0.6">
          <node id="1" lat="-23.55000" lon="-46.63000"/>
          <node id="2" lat="-23.55000" lon="-46.62990"/>
          <node id="3" lat="-23.55000" lon="-46.62980"/>
          <node id="10" lat="-23.55003" lon="-46.62996">
            <tag k="natural" v="tree"/>
            <tag k="species" v="Tipuana tipu"/>
            <tag k="genus" v="Tipuana"/>
          </node>
          <way id="100">
            <nd ref="1"/><nd ref="2"/><nd ref="3"/>
            <tag k="highway" v="residential"/>
            <tag k="lanes" v="2"/>
            <tag k="width" v="7"/>
          </way>
        </osm>
        """;
}
