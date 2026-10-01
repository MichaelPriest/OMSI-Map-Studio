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

            var catalog =
                MapStudioOmsiConstructionAssetClassifier
                    .Build(
                        [entry]);

            var candidate =
                Assert.Single(
                    catalog.RoadSplines);

            Assert.Equal(
                installedRelativePath,
                candidate.RelativePath);

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
