using MapStudio.Core.ProtonBus;
using Xunit;

namespace MapStudio.ProtonBus.Tests;

public sealed class ProtonBusBusStopDefinitionTests
{
    [Fact]
    public void WriterSerializesConfirmedPhase3BusStopFields()
    {
        var definition =
            CreateDefinition();

        var text =
            ProtonBusBusStopDefinitionWriter
                .Serialize(
                    definition);

        Assert.Contains(
            "[busstop]",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "name=Central Stop",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "isFirstStop=1",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "isLatestStop=0",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "isLeft=1",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "radius=1",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "paxAmount=12",
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
            "prefix=zzStopCentral",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "maxPathsToCheck=40",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "defPaxRotY=-90",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "[005]",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "rotY=90",
            text,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DefinitionBuildsDocumented3dMarkerNames()
    {
        var definition =
            CreateDefinition();

        Assert.Equal(
            "zzStopCentral_trigger",
            definition.TriggerObjectName);

        Assert.Equal(
            "zzStopCentral.000",
            definition
                .GetPassengerPositionObjectName(
                    0));

        Assert.Equal(
            "zzStopCentral.029",
            definition
                .GetPassengerPositionObjectName(
                    29));

        Assert.Equal(
            "zzStopCentral.txt",
            definition
                .SuggestedFileName);
    }

    [Fact]
    public void WriterRejectsUnsafePrefix()
    {
        var definition =
            CreateDefinition() with
            {
                Prefix =
                    "Ponto São Paulo"
            };

        Assert.Throws<
            ArgumentException>(
                () =>
                    ProtonBusBusStopDefinitionWriter
                        .Serialize(
                            definition));
    }

    [Fact]
    public void WriterRejectsIndividualRotationOutsideCheckedSlots()
    {
        var definition =
            CreateDefinition() with
            {
                MaxPathsToCheck =
                    5,
                IndividualPassengerRotations =
                    new Dictionary<
                        int,
                        double>
                    {
                        [5] =
                            90
                    }
            };

        Assert.Throws<
            ArgumentOutOfRangeException>(
                () =>
                    ProtonBusBusStopDefinitionWriter
                        .Serialize(
                            definition));
    }

    [Fact]
    public void PackageWriterPlacesBusStopUnderModelsDirectory()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudioProtonBusBusStopTests",
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
                            BusStops =
                            [
                                CreateDefinition()
                            ]
                        });

            var busStopPath =
                Assert.Single(
                    result.BusStopPaths);

            Assert.True(
                File.Exists(
                    busStopPath));

            Assert.EndsWith(
                Path.Combine(
                    "maps",
                    "Mapa",
                    "tiles",
                    "Rota",
                    "busstops",
                    "zzStopCentral.txt"),
                busStopPath,
                StringComparison
                    .OrdinalIgnoreCase);

            Assert.Contains(
                "prefix=zzStopCentral",
                File.ReadAllText(
                    busStopPath),
                StringComparison.Ordinal);
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
    public void PackageWriterRejectsDuplicateBusStopPrefix()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudioProtonBusBusStopTests",
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
                                    BusStops =
                                    [
                                        CreateDefinition(),
                                        CreateDefinition() with
                                        {
                                            FriendlyName =
                                                "Another"
                                        }
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

    private static ProtonBusBusStopDefinition
        CreateDefinition() =>
        new(
            Prefix:
                "zzStopCentral",
            FriendlyName:
                "Central Stop",
            PassengerAmount:
                12,
            DefaultPassengerRotationY:
                -90,
            IsFirstStop:
                true,
            IsLatestStop:
                false,
            IsLeft:
                true,
            Radius:
                1,
            MaxPathsToCheck:
                40,
            IndividualPassengerRotations:
                new Dictionary<
                    int,
                    double>
                {
                    [5] =
                        90,
                    [6] =
                        180
                });
}
