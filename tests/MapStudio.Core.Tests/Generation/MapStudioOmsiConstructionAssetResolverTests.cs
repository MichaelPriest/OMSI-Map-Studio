using MapStudio.Core.Omsi.Indexing;
using MapStudio.Core.Omsi.Splines;
using MapStudio.Core.Omsi.Structures;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioOmsiConstructionAssetResolverTests
{
    [Fact]
    public void ClassifierFindsRoadsJunctionsSignalsAndCrosswalks()
    {
        var catalog =
            MapStudioOmsiConstructionAssetClassifier
                .Build(
                    [
                        new OmsiAssetIndexEntry(
                            @"Splines\Berlin\road_2lane.sli",
                            OmsiAssetKind.Spline,
                            100,
                            1),
                        new OmsiAssetIndexEntry(
                            @"Sceneryobjects\Kreuzungen\Kreuzung_4arm.sco",
                            OmsiAssetKind.SceneryObject,
                            100,
                            1),
                        new OmsiAssetIndexEntry(
                            @"Sceneryobjects\Traffic\Ampel_3fach.sco",
                            OmsiAssetKind.SceneryObject,
                            100,
                            1),
                        new OmsiAssetIndexEntry(
                            @"Splines\Markings\Zebrastreifen.sli",
                            OmsiAssetKind.Spline,
                            100,
                            1)
                    ]);

        Assert.Single(
            catalog.RoadSplines);

        Assert.Single(
            catalog.JunctionObjects);

        Assert.Single(
            catalog.TrafficSignalObjects);

        Assert.Single(
            catalog.CrosswalkAssets);

        Assert.Equal(
            4,
            catalog.TotalRecognized);
    }

    [Fact]
    public void ClassifierSeparatesOriginalOmsiConstructionFamilies()
    {
        var catalog =
            MapStudioOmsiConstructionAssetClassifier
                .Build(
                    [
                        new OmsiAssetIndexEntry(
                            @"Splines\Marcel\str_2spur_11m_SeeburgerStr1.sli",
                            OmsiAssetKind.Spline,
                            100,
                            1),
                        new OmsiAssetIndexEntry(
                            @"Sceneryobjects\Kreuz_MC\Einm_Altonaer.sco",
                            OmsiAssetKind.SceneryObject,
                            100,
                            1),
                        new OmsiAssetIndexEntry(
                            @"Sceneryobjects\Verkehrszeichen_MC\Ampel_Kfz_1.sco",
                            OmsiAssetKind.SceneryObject,
                            100,
                            1),
                        new OmsiAssetIndexEntry(
                            @"Sceneryobjects\Kreuz_MC\Zebra_falks.sco",
                            OmsiAssetKind.SceneryObject,
                            100,
                            1)
                    ]);

        Assert.Single(
            catalog.RoadSplines);

        Assert.Single(
            catalog.JunctionObjects);

        Assert.Single(
            catalog.TrafficSignalObjects);

        Assert.Single(
            catalog.CrosswalkAssets);

        Assert.DoesNotContain(
            catalog.JunctionObjects,
            item =>
                item.RelativePath.Contains(
                    "Zebra",
                    StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ResolverPrefersOriginalOmsiRoadOverExactAddonAndRoadKitFallback()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-original-road-priority-" +
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            await new MapStudioRoadKitGenerator()
                .InstallOrUpdateAsync(
                    root);

            var exactAddonSource =
                Path.Combine(
                    root,
                    MapStudioStandardRoadCatalog
                        .RoadTwoLane
                        .RelativePath
                        .Replace(
                            '\\',
                            Path.DirectorySeparatorChar));

            var originalSource =
                exactAddonSource;

            var addonRelativePath =
                @"Splines\Addon\road_exact_7m.sli";

            var addonPath =
                Path.Combine(
                    root,
                    addonRelativePath.Replace(
                        '\\',
                        Path.DirectorySeparatorChar));

            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    addonPath)!);

            File.Copy(
                exactAddonSource,
                addonPath,
                overwrite:
                    true);

            var originalRelativePath =
                @"Splines\Marcel\str_2spur_7m_Test.sli";

            var originalPath =
                Path.Combine(
                    root,
                    originalRelativePath.Replace(
                        '\\',
                        Path.DirectorySeparatorChar));

            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    originalPath)!);

            File.Copy(
                originalSource,
                originalPath,
                overwrite:
                    true);

            OmsiAssetIndexEntry Entry(
                string relativePath,
                string fullPath) =>
                new(
                    relativePath,
                    OmsiAssetKind.Spline,
                    new FileInfo(fullPath).Length,
                    File.GetLastWriteTimeUtc(
                        fullPath)
                        .Ticks);

            var matches =
                await new MapStudioOmsiRoadSplineResolver()
                    .ResolveAsync(
                        root,
                        [
                            Entry(
                                addonRelativePath,
                                addonPath),
                            Entry(
                                originalRelativePath,
                                originalPath)
                        ],
                        [
                            MapStudioStandardRoadCatalog
                                .RoadTwoLane
                        ]);

            var match =
                Assert.Single(
                    matches);

            Assert.Equal(
                originalRelativePath,
                match.Value.RelativePath,
                ignoreCase:
                    true);

            Assert.DoesNotContain(
                "Addon",
                match.Value.RelativePath,
                StringComparison.OrdinalIgnoreCase);

            Assert.DoesNotContain(
                MapStudioRoadKitGenerator
                    .PackFolderName,
                match.Value.RelativePath,
                StringComparison.OrdinalIgnoreCase);
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
    public async Task ResolverRejectsMateriallyOversizedOriginalOmsiRoadForExactInstalledAddon()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-road-physical-fit-" +
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            await new MapStudioRoadKitGenerator()
                .InstallOrUpdateAsync(
                    root);

            var exactSource =
                Path.Combine(
                    root,
                    MapStudioStandardRoadCatalog
                        .RoadTwoLane
                        .RelativePath
                        .Replace(
                            '\\',
                            Path.DirectorySeparatorChar));

            var oversizedSource =
                Path.Combine(
                    root,
                    MapStudioStandardRoadCatalog
                        .RoadTwoLaneWithSidewalk
                        .RelativePath
                        .Replace(
                            '\\',
                            Path.DirectorySeparatorChar));

            var addonRelativePath =
                @"Splines\Addon\road_exact_7m.sli";

            var addonPath =
                Path.Combine(
                    root,
                    addonRelativePath.Replace(
                        '\\',
                        Path.DirectorySeparatorChar));

            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    addonPath)!);

            File.Copy(
                exactSource,
                addonPath,
                overwrite:
                    true);

            var originalRelativePath =
                @"Splines\Marcel\str_2spur_11m_SeeburgerStr1.sli";

            var originalPath =
                Path.Combine(
                    root,
                    originalRelativePath.Replace(
                        '\\',
                        Path.DirectorySeparatorChar));

            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    originalPath)!);

            File.Copy(
                oversizedSource,
                originalPath,
                overwrite:
                    true);

            OmsiAssetIndexEntry Entry(
                string relativePath,
                string fullPath) =>
                new(
                    relativePath,
                    OmsiAssetKind.Spline,
                    new FileInfo(fullPath).Length,
                    File.GetLastWriteTimeUtc(
                        fullPath)
                        .Ticks);

            var matches =
                await new MapStudioOmsiRoadSplineResolver()
                    .ResolveAsync(
                        root,
                        [
                            Entry(
                                originalRelativePath,
                                originalPath),
                            Entry(
                                addonRelativePath,
                                addonPath)
                        ],
                        [
                            MapStudioStandardRoadCatalog
                                .RoadTwoLane
                        ]);

            var match =
                Assert.Single(
                    matches);

            Assert.Equal(
                addonRelativePath,
                match.Value.RelativePath,
                ignoreCase:
                    true);

            Assert.DoesNotContain(
                "str_2spur_11m",
                match.Value.RelativePath,
                StringComparison.OrdinalIgnoreCase);
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
    public async Task ResolverSelectsCompatibleInstalledSplineByPhysicalProfile()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-installed-road-resolver-" +
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
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

            var addonRelativePath =
                @"Splines\Addon\road_2lane_11m.sli";

            var addonPath =
                Path.Combine(
                    root,
                    addonRelativePath.Replace(
                        '\\',
                        Path.DirectorySeparatorChar));

            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    addonPath)!);

            File.Copy(
                sourcePath,
                addonPath,
                overwrite:
                    true);

            var addonEntry =
                new OmsiAssetIndexEntry(
                    addonRelativePath,
                    OmsiAssetKind.Spline,
                    new FileInfo(
                        addonPath)
                        .Length,
                    File.GetLastWriteTimeUtc(
                        addonPath)
                        .Ticks);

            var catalog =
                MapStudioOmsiConstructionAssetClassifier
                    .Build(
                        [
                            addonEntry,
                            entry
                        ]);

            Assert.Equal(
                2,
                catalog.RoadSplines.Count);

            Assert.Contains(
                catalog.RoadSplines,
                candidate =>
                    string.Equals(
                        candidate.RelativePath,
                        installedRelativePath,
                        StringComparison.OrdinalIgnoreCase));

            var matches =
                await new MapStudioOmsiRoadSplineResolver()
                    .ResolveAsync(
                        root,
                        catalog.RoadSplines,
                        [
                            MapStudioStandardRoadCatalog
                                .RoadTwoLaneWithSidewalk
                        ]);

            var match =
                Assert.Single(
                    matches);

            Assert.Equal(
                MapStudioStandardRoadCatalog
                    .RoadTwoLaneWithSidewalk
                    .Key,
                match.Key);

            Assert.Equal(
                installedRelativePath,
                match.Value.RelativePath);

            Assert.DoesNotContain(
                MapStudioRoadKitGenerator
                    .PackFolderName,
                match.Value.RelativePath,
                StringComparison.OrdinalIgnoreCase);

            Assert.True(
                match.Value.TrafficPathCount >
                0);
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

}
