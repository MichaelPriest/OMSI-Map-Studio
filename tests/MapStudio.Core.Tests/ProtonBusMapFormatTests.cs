using MapStudio.Core.ProtonBus;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class ProtonBusMapFormatTests
{
    [Fact]
    public void WriterSerializesPhase3MapDefinition()
    {
        var definition =
            new ProtonBusMapDefinition(
                "Mapa Teste",
                "Mapa Teste",
                "Rota 1");

        var text =
            ProtonBusMapDefinitionWriter
                .Serialize(
                    definition);

        var expected =
            string.Join(
                Environment.NewLine,
                [
                    "[map]",
                    "baseDir=Mapa Teste",
                    "modelsDir=Rota 1",
                    "textures=textures",
                    "mapModVersion=3",
                    "preview=preview"
                ]) +
            Environment.NewLine;

        Assert.Equal(
            expected,
            text);
    }

    [Fact]
    public void LayoutMatchesDocumentedMapPackageStructure()
    {
        var definition =
            new ProtonBusMapDefinition(
                "Mapa Teste",
                "Mapa Teste",
                "Rota 1");

        var layout =
            ProtonBusMapPackageLayout
                .From(
                    definition);

        Assert.Equal(
            "maps/Mapa Teste.map",
            layout.MapDefinitionPath);

        Assert.Equal(
            "maps/Mapa Teste/tiles/Rota 1",
            layout.ModelsDirectoryPath);

        Assert.Equal(
            "maps/Mapa Teste/textures",
            layout.TexturesDirectoryPath);

        Assert.Equal(
            "maps/Mapa Teste/tiles/Rota 1/busstops",
            layout.BusStopsDirectoryPath);

        Assert.Equal(
            "maps/Mapa Teste/tiles/Rota 1/trafficlights",
            layout.TrafficLightsDirectoryPath);

        Assert.Equal(
            "maps/Mapa Teste/tiles/Rota 1/streetlights",
            layout.StreetLightsDirectoryPath);
    }

    [Fact]
    public void ValidatorRejectsAccentsAndTraversal()
    {
        var definition =
            new ProtonBusMapDefinition(
                "São Paulo",
                "../Mapa",
                "Rota 1");

        var issues =
            ProtonBusMapDefinitionValidator
                .Validate(
                    definition);

        Assert.Contains(
            issues,
            issue =>
                issue.Code ==
                "PBMAP_MAP_NAME_UNSAFE_CHARS" &&
                issue.Severity ==
                ProtonBusValidationSeverity
                    .Error);

        Assert.Contains(
            issues,
            issue =>
                issue.Code ==
                "PBMAP_BASE_DIR_NOT_RELATIVE" &&
                issue.Severity ==
                ProtonBusValidationSeverity
                    .Error);
    }

    [Fact]
    public void ValidatorWarnsForUnverifiedFutureMapVersion()
    {
        var definition =
            new ProtonBusMapDefinition(
                "Mapa",
                "Mapa",
                "Rota",
                MapModVersion: 4);

        var issues =
            ProtonBusMapDefinitionValidator
                .Validate(
                    definition);

        Assert.Contains(
            issues,
            issue =>
                issue.Code ==
                "PBMAP_VERSION_EXPERIMENTAL" &&
                issue.Severity ==
                ProtonBusValidationSeverity
                    .Warning);
    }

    [Fact]
    public void MapFileNameKeepsExistingMapExtension()
    {
        Assert.Equal(
            "Cidade.map",
            new ProtonBusMapDefinition(
                "Cidade.map",
                "Cidade",
                "Rota")
                .MapFileName);
    }
}
