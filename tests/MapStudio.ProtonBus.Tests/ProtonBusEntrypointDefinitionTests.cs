using System.Numerics;
using MapStudio.Core.ProtonBus;
using Xunit;

namespace MapStudio.ProtonBus.Tests;

public sealed class ProtonBusEntrypointDefinitionTests
{
    [Fact]
    public void WriterSerializesIncrementalEntrypoints()
    {
        var entrypoints =
            new[]
            {
                new ProtonBusEntrypointDefinition(
                    "351F-10 TP",
                    new(
                        -40.9988f,
                        0.086769f,
                        49.1041f),
                    new(
                        0,
                        180,
                        0)),
                new ProtonBusEntrypointDefinition(
                    "351F-10 TS",
                    new(
                        100.5f,
                        1.25f,
                        -20.75f),
                    new(
                        0,
                        -90,
                        0))
            };

        var text =
            ProtonBusEntrypointDefinitionWriter
                .SerializeDefinitions(
                    entrypoints);

        Assert.Contains(
            "[entrypoint_1]",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "name=351F-10 TP",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "posX=-40.9988",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "posY=0.086769",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "posZ=49.1041",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "rotY=180",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "[entrypoint_2]",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "rotY=-90",
            text,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ListWriterKeepsOneEntrypointPerLine()
    {
        var text =
            ProtonBusEntrypointDefinitionWriter
                .SerializeList(
                    [
                        CreateEntrypoint(
                            "Linha-01"),
                        CreateEntrypoint(
                            "Linha-02")
                    ]);

        Assert.Equal(
            string.Join(
                Environment.NewLine,
                [
                    "Linha-01",
                    "Linha-02",
                    string.Empty
                ]),
            text);
    }

    [Fact]
    public void WriterRejectsUnsafeOrDuplicateNames()
    {
        Assert.Throws<
            ArgumentException>(
                () =>
                    ProtonBusEntrypointDefinitionWriter
                        .SerializeDefinitions(
                            [
                                CreateEntrypoint(
                                    "São Paulo")
                            ]));

        Assert.Throws<
            ArgumentException>(
                () =>
                    ProtonBusEntrypointDefinitionWriter
                        .SerializeDefinitions(
                            [
                                CreateEntrypoint(
                                    "Linha-01"),
                                CreateEntrypoint(
                                    "linha-01")
                            ]));
    }

    [Fact]
    public void PackageWriterCreatesEntrypointFilesAndDestinations()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudioProtonBusEntrypointTests",
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            var request =
                new ProtonBusMapPackageRequest(
                    new(
                        "Mapa",
                        "Mapa",
                        "Rota"),
                    [])
                {
                    Entrypoints =
                    [
                        CreateEntrypoint(
                            "Linha-01") with
                        {
                            IsIntercity =
                                true
                        },
                        CreateEntrypoint(
                            "Garagem") with
                        {
                            IsOutOfService =
                                true
                        }
                    ]
                };

            var result =
                ProtonBusMapPackageWriter
                    .Write(
                        root,
                        request);

            Assert.NotNull(
                result.EntrypointsPath);

            Assert.NotNull(
                result.EntrypointsListPath);

            Assert.True(
                File.Exists(
                    result
                        .EntrypointsPath!));

            Assert.True(
                File.Exists(
                    result
                        .EntrypointsListPath!));

            Assert.Contains(
                "Linha-01",
                File.ReadAllText(
                    result
                        .EntrypointsListPath!),
                StringComparison.Ordinal);

            Assert.Equal(
                2,
                result
                    .DestinationDirectories
                    .Count);

            var intercity =
                Path.Combine(
                    root,
                    "maps",
                    "Mapa",
                    "dest",
                    "Linha-01",
                    "intercity.txt");

            var outOfService =
                Path.Combine(
                    root,
                    "maps",
                    "Mapa",
                    "dest",
                    "Garagem",
                    "outofservice.txt");

            Assert.True(
                File.Exists(
                    intercity));

            Assert.True(
                File.Exists(
                    outOfService));

            Assert.Equal(
                0,
                new FileInfo(
                    intercity)
                    .Length);

            Assert.Equal(
                0,
                new FileInfo(
                    outOfService)
                    .Length);
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

    private static ProtonBusEntrypointDefinition
        CreateEntrypoint(
            string name) =>
        new(
            name,
            Vector3.Zero,
            Vector3.Zero);
}
