using MapStudio.Core.Generation.Scene;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioOpenAerialMapCatalogClientTests
{
    [Fact]
    public void ParseResponseNormalizesLegacyBoundsAndMetadata()
    {
        const string json =
            """
            {
              "meta": {
                "page": 1,
                "limit": 100,
                "found": 1
              },
              "results": [
                {
                  "uuid": "image-1",
                  "title": "Cidade ortho",
                  "bbox": [-46.64, -23.53, -46.62, -23.56],
                  "gsd": 0.08,
                  "acquisition_end": "2026-08-20T12:00:00Z",
                  "platform": "uav",
                  "provider": "Example",
                  "properties": {
                    "license": "CC-BY 4.0",
                    "thumbnail": "https://images.example/thumb.png",
                    "tms": "https://tiles.example/image-1/{z}/{x}/{y}",
                    "url": "https://images.example/image-1.tif"
                  }
                }
              ]
            }
            """;

        var page =
            MapStudioOpenAerialMapCatalogClient
                .ParseResponse(
                    json);

        var image =
            Assert.Single(
                page.Images);

        Assert.Equal(
            -46.64,
            image.West,
            6);

        Assert.Equal(
            -46.62,
            image.East,
            6);

        Assert.Equal(
            -23.56,
            image.South,
            6);

        Assert.Equal(
            -23.53,
            image.North,
            6);

        Assert.Equal(
            0.08,
            image.GroundSampleDistanceMeters);

        Assert.True(
            image.IsAutomaticDerivationAllowed);

        Assert.True(
            image.Contains(
                -23.55,
                -46.63));
    }

    [Theory]
    [InlineData("CC-BY 4.0", true)]
    [InlineData("CC BY 4.0", true)]
    [InlineData("CC-BY-4.0", true)]
    [InlineData("CC-BY-SA 4.0", false)]
    [InlineData("CC-BY-NC 4.0", false)]
    [InlineData("", false)]
    public void AutomaticDerivationUsesConservativeLicenseAllowList(
        string license,
        bool expected)
    {
        Assert.Equal(
            expected,
            MapStudioOpenAerialMapCatalogClient
                .IsAutomaticDerivationLicense(
                    license));
    }

    [Fact]
    public void SelectBestForPointPrefersFinerGroundResolution()
    {
        var coarse =
            new MapStudioOpenAerialMapImage(
                "coarse",
                null,
                -47,
                -24,
                -46,
                -23,
                0.50,
                DateTimeOffset.Parse(
                    "2026-09-01T00:00:00Z"),
                "satellite",
                "A",
                "CC-BY 4.0",
                "https://tiles.example/coarse/{z}/{x}/{y}",
                null,
                null);

        var fine =
            new MapStudioOpenAerialMapImage(
                "fine",
                null,
                -47,
                -24,
                -46,
                -23,
                0.10,
                DateTimeOffset.Parse(
                    "2025-09-01T00:00:00Z"),
                "uav",
                "B",
                "CC-BY 4.0",
                "https://tiles.example/fine/{z}/{x}/{y}",
                null,
                null);

        var selected =
            MapStudioOpenAerialMapCatalogClient
                .SelectBestForPoint(
                    [
                        coarse,
                        fine
                    ],
                    -23.5,
                    -46.5);

        Assert.NotNull(
            selected);

        Assert.Equal(
            "fine",
            selected.Id);
    }
}
