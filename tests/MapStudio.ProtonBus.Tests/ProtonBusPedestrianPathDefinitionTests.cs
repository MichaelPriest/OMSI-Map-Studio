using MapStudio.Core.ProtonBus;
using Xunit;

namespace MapStudio.ProtonBus.Tests;

public sealed class ProtonBusPedestrianPathDefinitionTests
{
    [Fact]
    public void WriterSerializesConfirmedPedestrianSections()
    {
        var definition =
            new ProtonBusPedestrianPathDefinition(
                Prefix:
                    "xxSidewalk1",
                Reverse:
                    false,
                Loop:
                    true,
                MaxPathsToCheck:
                    999,
                IsSpawner:
                    true,
                SpawnIntervalSeconds:
                    5,
                AllowBicycle:
                    false);

        var text =
            ProtonBusPedestrianPathDefinitionWriter
                .Serialize(
                    definition);

        Assert.Contains(
            "[automatic_setup]",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "enabled=1",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "reverse=0",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "loop=1",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "[from_3d]",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "readFrom3D=1",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "prefix=xxSidewalk1",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "maxPathsToCheck=999",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "[defaults]",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "isSpawner=1",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "spawnInterval=5",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "allowBicycle=0",
            text,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DefinitionBuildsWaypointMarkerNames()
    {
        var definition =
            new ProtonBusPedestrianPathDefinition(
                "peoplePath");

        Assert.Equal(
            "peoplePath.000",
            definition
                .GetWaypointObjectName(
                    0));

        Assert.Equal(
            "peoplePath.125",
            definition
                .GetWaypointObjectName(
                    125));

        Assert.Equal(
            "peoplePath.txt",
            definition
                .SuggestedFileName);
    }

    [Fact]
    public void WriterRejectsUnsafePrefixAndInvalidInterval()
    {
        Assert.Throws<
            ArgumentException>(
                () =>
                    ProtonBusPedestrianPathDefinitionWriter
                        .Serialize(
                            new(
                                "calçada 1")));

        Assert.Throws<
            ArgumentOutOfRangeException>(
                () =>
                    ProtonBusPedestrianPathDefinitionWriter
                        .Serialize(
                            new(
                                "path1",
                                SpawnIntervalSeconds:
                                    0)));
    }

    [Fact]
    public void PackageWriterPlacesPedestrianPathUnderAiPeople()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudioProtonBusPedestrianTests",
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            var result =
                ProtonBusMapPackageWriter
                    .Write(
                        root,
                        new(
                            new(
                                "Mapa",
                                "Mapa",
                                "Rota"),
                            [])
                        {
                            PedestrianPaths =
                            [
                                new(
                                    "xxSidewalk1")
                            ]
                        });

            var path =
                Assert.Single(
                    result
                        .PedestrianPathPaths);

            Assert.True(
                File.Exists(
                    path));

            Assert.EndsWith(
                Path.Combine(
                    "maps",
                    "Mapa",
                    "tiles",
                    "Rota",
                    "aipeople",
                    "xxSidewalk1.txt"),
                path,
                StringComparison
                    .OrdinalIgnoreCase);
        }
        finally
        {
            if (
                Directory.Exists(
                    root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }

    [Fact]
    public void PackageWriterRejectsDuplicatePedestrianPrefix()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudioProtonBusPedestrianTests",
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            Assert.Throws<
                ArgumentException>(
                    () =>
                        ProtonBusMapPackageWriter
                            .Write(
                                root,
                                new(
                                    new(
                                        "Mapa",
                                        "Mapa",
                                        "Rota"),
                                    [])
                                {
                                    PedestrianPaths =
                                    [
                                        new(
                                            "path1"),
                                        new(
                                            "PATH1")
                                    ]
                                }));
        }
        finally
        {
            if (
                Directory.Exists(
                    root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }
}
