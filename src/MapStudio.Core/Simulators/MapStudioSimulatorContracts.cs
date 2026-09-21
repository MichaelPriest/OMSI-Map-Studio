namespace MapStudio.Core.Simulators;

[Flags]
public enum MapStudioSimulatorCapability
{
    None = 0,
    MapDiscovery = 1 << 0,
    MapEditing = 1 << 1,
    Terrain = 1 << 2,
    Roads = 1 << 3,
    SceneryObjects = 1 << 4,
    AssetLibrary = 1 << 5,
    Traffic = 1 << 6,
    Transit = 1 << 7,
    Signals = 1 << 8,
    Rail = 1 << 9,
    BuildingAssets = 1 << 10,
    ProceduralGeneration = 1 << 11,
    Validation = 1 << 12
}

public static class MapStudioSimulatorIds
{
    public const string Omsi2 =
        "omsi2";

    public const string ProtonBus =
        "proton-bus";

    public const string Lotus =
        "lotus";

    public const string Custom =
        "custom";
}

public sealed record MapStudioSimulatorDescriptor(
    string Id,
    string DisplayName,
    MapStudioSimulatorCapability Capabilities);

public interface IMapStudioSimulatorAdapter
{
    MapStudioSimulatorDescriptor Descriptor
    {
        get;
    }
}

public sealed class MapStudioSimulatorRegistry
{
    private readonly Dictionary<
        string,
        IMapStudioSimulatorAdapter>
        _adapters =
            new(
                StringComparer
                    .OrdinalIgnoreCase);

    public IReadOnlyList<
        MapStudioSimulatorDescriptor>
        Adapters =>
        _adapters
            .Values
            .Select(
                adapter =>
                    adapter.Descriptor)
            .OrderBy(
                descriptor =>
                    descriptor.DisplayName,
                StringComparer
                    .OrdinalIgnoreCase)
            .ToArray();

    public void Register(
        IMapStudioSimulatorAdapter adapter)
    {
        ArgumentNullException.ThrowIfNull(
            adapter);

        var descriptor =
            adapter.Descriptor;

        if (
            string.IsNullOrWhiteSpace(
                descriptor.Id) ||
            string.IsNullOrWhiteSpace(
                descriptor.DisplayName))
        {
            throw new ArgumentException(
                "Simulator adapter descriptor must contain an id and display name.",
                nameof(adapter));
        }

        _adapters[
            descriptor.Id.Trim()] =
            adapter;
    }

    public bool TryGet(
        string simulatorId,
        out IMapStudioSimulatorAdapter?
            adapter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            simulatorId);

        return _adapters.TryGetValue(
            simulatorId.Trim(),
            out adapter);
    }
}

public sealed class Omsi2SimulatorAdapter
    : IMapStudioSimulatorAdapter
{
    public MapStudioSimulatorDescriptor
        Descriptor { get; } =
        new(
            MapStudioSimulatorIds
                .Omsi2,
            "OMSI 2",
            MapStudioSimulatorCapability
                .MapDiscovery |
            MapStudioSimulatorCapability
                .MapEditing |
            MapStudioSimulatorCapability
                .Terrain |
            MapStudioSimulatorCapability
                .Roads |
            MapStudioSimulatorCapability
                .SceneryObjects |
            MapStudioSimulatorCapability
                .AssetLibrary |
            MapStudioSimulatorCapability
                .Traffic |
            MapStudioSimulatorCapability
                .Transit |
            MapStudioSimulatorCapability
                .Signals |
            MapStudioSimulatorCapability
                .Rail |
            MapStudioSimulatorCapability
                .BuildingAssets |
            MapStudioSimulatorCapability
                .ProceduralGeneration |
            MapStudioSimulatorCapability
                .Validation);
}
