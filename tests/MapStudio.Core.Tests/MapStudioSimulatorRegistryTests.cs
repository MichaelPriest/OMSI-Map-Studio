using MapStudio.Core.Simulators;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioSimulatorRegistryTests
{
    [Fact]
    public void OmsiAdapterCanBeRegisteredWithoutRegisteringPlannedSimulators()
    {
        var registry =
            new MapStudioSimulatorRegistry();

        registry.Register(
            new Omsi2SimulatorAdapter());

        Assert.True(
            registry.TryGet(
                MapStudioSimulatorIds
                    .Omsi2,
                out var omsi));

        Assert.NotNull(
            omsi);

        Assert.True(
            omsi!
                .Descriptor
                .Capabilities
                .HasFlag(
                    MapStudioSimulatorCapability
                        .MapEditing));

        Assert.False(
            registry.TryGet(
                MapStudioSimulatorIds
                    .ProtonBus,
                out _));

        Assert.False(
            registry.TryGet(
                MapStudioSimulatorIds
                    .Lotus,
                out _));
    }

    [Fact]
    public void RegistryAcceptsFutureSimulatorAdapterWithoutCoreChanges()
    {
        var registry =
            new MapStudioSimulatorRegistry();

        registry.Register(
            new TestAdapter());

        Assert.True(
            registry.TryGet(
                MapStudioSimulatorIds
                    .ProtonBus,
                out var adapter));

        Assert.Equal(
            "Proton Bus",
            adapter!
                .Descriptor
                .DisplayName);
    }

    private sealed class TestAdapter
        : IMapStudioSimulatorAdapter
    {
        public MapStudioSimulatorDescriptor
            Descriptor { get; } =
            new(
                MapStudioSimulatorIds
                    .ProtonBus,
                "Proton Bus",
                MapStudioSimulatorCapability
                    .MapDiscovery |
                MapStudioSimulatorCapability
                    .MapEditing);
    }
}
