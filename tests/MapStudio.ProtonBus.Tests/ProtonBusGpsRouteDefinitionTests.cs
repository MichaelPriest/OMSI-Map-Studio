using MapStudio.Core.ProtonBus;
using Xunit;

namespace MapStudio.ProtonBus.Tests;

public sealed class ProtonBusGpsRouteDefinitionTests
{
    [Fact]
    public void PlannerBuildsDocumentedGpsObjectName()
    {
        Assert.Equal(
            "_gps_309T-22 TP_",
            ProtonBusGpsRouteNamePlanner
                .BuildObjectName(
                    "309T-22 TP"));

        Assert.Equal(
            "_gps_309T-22 TP_part.001",
            ProtonBusGpsRouteNamePlanner
                .BuildObjectName(
                    "309T-22 TP",
                    "part.001"));
    }

    [Fact]
    public void PlannerMatchesAdditionalGpsPieces()
    {
        Assert.True(
            ProtonBusGpsRouteNamePlanner
                .MatchesEntrypoint(
                    "_gps_Linha 1 Ida_seta.002",
                    "Linha 1 Ida"));

        Assert.False(
            ProtonBusGpsRouteNamePlanner
                .MatchesEntrypoint(
                    "_gps_Linha 2 Ida_",
                    "Linha 1 Ida"));
    }

    [Fact]
    public void PlannerRejectsUnsafeEntrypointName()
    {
        Assert.Throws<
            ArgumentException>(
                () =>
                    ProtonBusGpsRouteNamePlanner
                        .BuildObjectName(
                            "São Paulo"));
    }

    [Fact]
    public void PlannerRejectsUnsafePartSuffix()
    {
        Assert.Throws<
            ArgumentException>(
                () =>
                    ProtonBusGpsRouteNamePlanner
                        .BuildObjectName(
                            "Linha-01",
                            "../part"));
    }
}
