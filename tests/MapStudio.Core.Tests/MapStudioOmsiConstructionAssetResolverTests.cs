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
            var generated =
                await new MapStudioRoadKitGenerator()
                    .InstallOrUpdateAsync(
                        root);

            var relativePath =
                MapStudioStandardRoadCatalog
                    .RoadTwoLane
                    .RelativePath;

            var fullPath =
                Path.Combine(
                    root,
                    relativePath.Replace(
                        '\\',
                        Path.DirectorySeparatorChar));

            Assert.True(
                File.Exists(
                    fullPath));

            var entry =
                new OmsiAssetIndexEntry(
                    relativePath,
                    OmsiAssetKind.Spline,
                    new FileInfo(
                        fullPath)
                        .Length,
                    File.GetLastWriteTimeUtc(
                        fullPath)
                        .Ticks);

            var matches =
                await new MapStudioOmsiRoadSplineResolver()
                    .ResolveAsync(
                        root,
                        [entry],
                        [
                            MapStudioStandardRoadCatalog
                                .RoadTwoLane
                        ]);

            var match =
                Assert.Single(
                    matches);

            Assert.Equal(
                MapStudioStandardRoadCatalog
                    .RoadTwoLane
                    .Key,
                match.Key);

            Assert.Equal(
                relativePath,
                match.Value.RelativePath);

            Assert.InRange(
                match.Value.WidthMeters,
                6.99,
                7.01);

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
